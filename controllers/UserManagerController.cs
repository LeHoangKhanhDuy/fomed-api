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
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery(Name = "limit")] int limit = 10, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        // Base query
        var q = _db.Users.AsNoTracking();

        // Tổng số bản ghi
        var total = await q.CountAsync(ct);

        // Sắp xếp mặc định: mới nhất trước
        var items = await q
            .OrderBy(u => u.UserId)
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
                Profile = new
                {
                    AvatarUrl = u.Profile != null ? u.Profile.AvatarUrl : null,
                    Address = u.Profile != null ? u.Profile.Address : null,
                    Bio = u.Profile != null ? u.Profile.Bio : null
                }
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
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ============== LẤY CHI TIẾT 1 NGƯỜI DÙNG ============== */
    [HttpGet("user-details/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Lấy danh sách 1 người dùng",
    Description = "Chỉ ADMIN mới có thể Quản lý người dùng",
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
                Roles = u.UserRoles.Select(r => r.Role.Code).ToArray(),
                Profile = new
                {
                    AvatarUrl = u.Profile != null ? u.Profile.AvatarUrl : null,
                    Address = u.Profile != null ? u.Profile.Address : null,
                    Bio = u.Profile != null ? u.Profile.Bio : null
                }
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
    Summary = "Cập nhật thông tin người dùng",
    Description = "Chỉ ADMIN mới có thể Quản lý người dùng",
    Tags = new[] { "Users" })]
    public async Task<IActionResult> UpdateUser([FromRoute] long id, [FromBody] UpdateUserRequest req, CancellationToken ct = default)
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

        var dbRoles = await _db.Roles
            .Where(r => newCodes.Contains(r.Code))
            .ToListAsync(ct);

        var missing = newCodes.Except(dbRoles.Select(r => r.Code)).ToList();
        if (missing.Count > 0)
            return BadRequest(new { success = false, message = "Một số role không tồn tại.", invalid = missing });

        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!long.TryParse(idStr, out var currentUserId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ." });

        // Không tự bỏ quyền ADMIN của chính mình
        if (currentUserId == id && !newCodes.Contains("ADMIN"))
            return BadRequest(new { success = false, message = "Không thể tự bỏ quyền ADMIN của chính bạn." });

        // Đảm bảo còn ít nhất 1 ADMIN hoạt động
        if (!newCodes.Contains("ADMIN"))
        {
            var adminCount = await _db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.Role.Code == "ADMIN" && ur.User.IsActive)
                .CountAsync(ct);

            var isTargetAdmin = user.UserRoles.Any(ur => ur.Role.Code == "ADMIN");
            if (isTargetAdmin && adminCount <= 1)
                return BadRequest(new { success = false, message = "Phải còn ít nhất 1 ADMIN hoạt động." });
        }

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Diff thay vì Clear/Add toàn bộ (ít ghi DB hơn)
        var currentMap = user.UserRoles.ToDictionary(x => x.Role.Code, x => x);
        var targetMap = dbRoles.ToDictionary(x => x.Code, x => x);

        // Remove roles không còn
        foreach (var kv in currentMap)
        {
            if (!targetMap.ContainsKey(kv.Key))
                _db.UserRoles.Remove(kv.Value);
        }

        // Add roles mới
        foreach (var kv in targetMap)
        {
            if (!currentMap.ContainsKey(kv.Key))
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = kv.Value.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Cập nhật vai trò thành công",
            data = new { userId = user.UserId, roles = newCodes }
        });
    }

    /* ============== XÓA THÔNG TIN NGƯỜI DÙNG ============== */
    [HttpDelete("user-delete/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Vô hiệu hóa người dùng",
    Description = "Chỉ ADMIN mới có thể Quản lý người dùng",
    Tags = new[] { "Users" })]
    public async Task<IActionResult> DeleteUser([FromRoute] long id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id, ct);

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy user" });

        // Không tự vô hiệu hóa chính mình
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (long.TryParse(idStr, out var currentUserId) && currentUserId == id)
            return BadRequest(new { success = false, message = "Không thể tự vô hiệu hóa tài khoản của chính bạn." });

        // Nếu user là ADMIN cuối cùng đang active thì chặn
        var isTargetAdmin = user.UserRoles.Any(ur => ur.Role.Code == "ADMIN");
        if (isTargetAdmin)
        {
            var adminActiveCount = await _db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.Role.Code == "ADMIN" && ur.User.IsActive && ur.UserId != id)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync(ct);

            if (adminActiveCount == 0)
                return BadRequest(new { success = false, message = "Phải còn ít nhất 1 ADMIN hoạt động." });
        }

        user.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Vô hiệu hóa user thành công" });
    }
}