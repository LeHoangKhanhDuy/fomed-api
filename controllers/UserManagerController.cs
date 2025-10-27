using System.Security.Claims;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/admin/")]
[Authorize(Roles = "ADMIN")]
public class AdminController : ControllerBase
{
    private readonly FoMedContext _db;
    private readonly IConfiguration _cfg;

    public AdminController(FoMedContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    /* ============== LẤY DANH SÁCH NGƯỜI DÙNG ============== */
    [HttpGet("users")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Lấy danh sách người dùng",
        Description = "Chỉ ADMIN mới có thể Quản lý người dùng",
        Tags = new[] { "Users" })]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? role = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        // Base query
        var q = _db.Users.AsNoTracking();

        // ✅ Filter by keyword
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(u =>
                u.FullName.ToLower().Contains(kw) ||
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                (u.Phone != null && u.Phone.Contains(kw))
            );
        }

        // ✅ Filter by role
        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleCode = role.Trim().ToUpperInvariant();
            q = q.Where(u => u.UserRoles.Any(ur => ur.Role.Code == roleCode));
        }

        // Tổng số bản ghi sau filter
        var total = await q.CountAsync(ct);

        // ✅ Sắp xếp: mới nhất trước
        var items = await q
            .OrderByDescending(u => u.UserId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.CreatedAt,
                Roles = u.UserRoles.Select(r => r.Role.Code).ToArray(),
                Profile = u.Profile != null ? new
                {
                    u.Profile.AvatarUrl,
                    u.Profile.Address,
                    u.Profile.Bio
                } : null
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách người dùng thành công",
            data = new
            {
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ============== LẤY CHI TIẾT 1 NGƯỜI DÙNG ============== */
    [HttpGet("user-details/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Lấy chi tiết 1 người dùng",
        Description = "Chỉ ADMIN mới có thể xem chi tiết người dùng",
        Tags = new[] { "Users" })]
    public async Task<IActionResult> GetUserById([FromRoute] long id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == id)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.CreatedAt,
                gender = u.Profile!.Gender,
                dateOfBirth = u.Profile!.DateOfBirth.HasValue
                ? (DateTime?)u.Profile.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                : (DateTime?)null,

                Roles = u.UserRoles.Select(r => r.Role.Code).ToArray(),
                Profile = u.Profile != null ? new
                {
                    u.Profile.AvatarUrl,
                    u.Profile.Address,
                    u.Profile.Bio
                } : null
            })

            .FirstOrDefaultAsync(ct);

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy user" });

        return Ok(new { success = true, message = "Lấy thông tin thành công", data = user });
    }

    /* ============== CẬP NHẬT THÔNG TIN NGƯỜI DÙNG ============== */
    public sealed class UpdateUserRequest
    {
        public List<string> Roles { get; set; } = new();
    }

    [HttpPut("user-update/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Cập nhật vai trò người dùng",
        Description = "Chỉ ADMIN mới có thể cập nhật vai trò người dùng",
        Tags = new[] { "Users" })]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] long id,
        [FromBody] UpdateUserRequest req,
        CancellationToken ct = default)
    {
        if (req?.Roles == null || req.Roles.Count == 0)
            return BadRequest(new { success = false, message = "Danh sách role không được rỗng." });

        var newCodes = req.Roles
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id, ct);

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy user" });

        // ✅ Validate roles exist
        var dbRoles = await _db.Roles
            .Where(r => newCodes.Contains(r.Code))
            .ToListAsync(ct);

        var missing = newCodes.Except(dbRoles.Select(r => r.Code)).ToList();
        if (missing.Count > 0)
            return BadRequest(new { success = false, message = "Một số role không tồn tại.", invalid = missing });

        // ✅ Get current user ID
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ." });

        // ✅ Prevent self-demotion
        if (currentUserId == id && !newCodes.Contains("ADMIN"))
            return BadRequest(new { success = false, message = "Không thể tự bỏ quyền ADMIN của chính bạn." });

        // ✅ Ensure at least 1 active ADMIN remains
        if (!newCodes.Contains("ADMIN"))
        {
            var isTargetAdmin = user.UserRoles.Any(ur => ur.Role.Code == "ADMIN");
            if (isTargetAdmin && user.IsActive)
            {
                var otherActiveAdminCount = await _db.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.Role.Code == "ADMIN"
                        && ur.User.IsActive
                        && ur.UserId != id)
                    .Select(ur => ur.UserId)
                    .Distinct()
                    .CountAsync(ct);

                if (otherActiveAdminCount == 0)
                    return BadRequest(new { success = false, message = "Phải còn ít nhất 1 ADMIN hoạt động." });
            }
        }

        // ✅ Efficient diff-based update
        var currentRoles = user.UserRoles.ToDictionary(x => x.Role.Code, x => x);
        var targetRoles = dbRoles.ToDictionary(x => x.Code, x => x);

        // Remove old roles
        foreach (var code in currentRoles.Keys)
        {
            if (!targetRoles.ContainsKey(code))
                _db.UserRoles.Remove(currentRoles[code]);
        }

        // Add new roles
        foreach (var code in targetRoles.Keys)
        {
            if (!currentRoles.ContainsKey(code))
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = targetRoles[code].RoleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Cập nhật vai trò thành công",
            data = new { userId = user.UserId, roles = newCodes }
        });
    }

    /* ============== BẬT / TẮT TÀI KHOẢN ============== */
    [HttpPatch("user-status/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Cập nhật trạng thái hoạt động",
        Description = "ADMIN có thể khoá (IsActive=false) hoặc mở khoá (IsActive=true) tài khoản",
        Tags = new[] { "Users" })]
    public async Task<IActionResult> SetUserStatus(
        [FromRoute] long id,
        [FromBody] UserStatusRequest req,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id, ct);

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy user" });

        var currentUserId = GetCurrentUserId();

        // ✅ Prevent locking yourself
        if (!req.IsActive && currentUserId == id)
            return BadRequest(new { success = false, message = "Không thể tự vô hiệu hoá tài khoản của chính bạn." });

        // ✅ Ensure at least 1 active ADMIN remains when locking
        if (!req.IsActive)
        {
            var isTargetAdmin = user.UserRoles.Any(ur => ur.Role.Code == "ADMIN");
            if (isTargetAdmin && user.IsActive)
            {
                var otherActiveAdminCount = await _db.UserRoles
                    .AsNoTracking()
                    .Where(ur => ur.Role.Code == "ADMIN"
                        && ur.User.IsActive
                        && ur.UserId != id)
                    .Select(ur => ur.UserId)
                    .Distinct()
                    .CountAsync(ct);

                if (otherActiveAdminCount == 0)
                    return BadRequest(new { success = false, message = "Phải còn ít nhất 1 ADMIN hoạt động." });
            }
        }

        user.IsActive = req.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = req.IsActive ? "Đã mở khoá tài khoản." : "Đã khoá tài khoản."
        });
    }

    /* ============== HELPER METHODS ============== */
    private long? GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("uid");
        return long.TryParse(idStr, out var id) ? id : null;
    }
}

