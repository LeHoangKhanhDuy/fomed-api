using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoMed.Api.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace FoMed.Api.Controllers;

[ApiController]
[Route("api/v1/specialties")]
[Produces("application/json")]
public class SpecialtiesController : ControllerBase
{
    private readonly FoMedContext _db;
    private readonly ILogger<SpecialtiesController> _logger;

    public SpecialtiesController(FoMedContext db, ILogger<SpecialtiesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ===================== Danh sách chuyên khoa Public =====================
    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Danh sách chuyên khoa",
        Description = "Lấy danh sách tất cả chuyên khoa đang hoạt động (public, không cần auth).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> GetSpecialties(
        [FromQuery] bool? isActive = true,
        CancellationToken ct = default)
    {
        var query = _db.Specialties.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                specialtyId = s.SpecialtyId,
                code = s.Code,
                name = s.Name,
                description = s.Description,
                isActive = s.IsActive,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách chuyên khoa thành công.",
            data = items
        });
    }

    // ===================== Chi tiết chuyên khoa =====================
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Chi tiết chuyên khoa",
        Description = "Lấy thông tin chi tiết của một chuyên khoa.",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> GetSpecialtyById(int id, CancellationToken ct = default)
    {
        var specialty = await _db.Specialties
            .AsNoTracking()
            .Where(s => s.SpecialtyId == id)
            .Select(s => new
            {
                specialtyId = s.SpecialtyId,
                code = s.Code,
                name = s.Name,
                description = s.Description,
                isActive = s.IsActive,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt,
                doctorCount = s.DoctorSpecialties.Count
            })
            .FirstOrDefaultAsync(ct);

        if (specialty == null)
            return NotFound(new { success = false, message = "Không tìm thấy chuyên khoa." });

        return Ok(new
        {
            success = true,
            message = "Lấy thông tin chuyên khoa thành công.",
            data = specialty
        });
    }

    // ===================== Tạo chuyên khoa mới Admim =====================
    [HttpPost("admin/create")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(
        Summary = "Tạo chuyên khoa mới",
        Description = "Tạo chuyên khoa mới (chỉ ADMIN).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> CreateSpecialty(
        [FromBody] CreateSpecialtyRequest req,
        CancellationToken ct = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { success = false, message = "Tên chuyên khoa không được để trống." });

        if (req.Name.Length > 150)
            return BadRequest(new { success = false, message = "Tên chuyên khoa không được vượt quá 150 ký tự." });

        // Check trùng tên
        var existsByName = await _db.Specialties
            .AnyAsync(s => s.Name.ToLower() == req.Name.ToLower(), ct);

        if (existsByName)
            return BadRequest(new { success = false, message = "Tên chuyên khoa đã tồn tại." });

        // Check trùng code (nếu có)
        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var existsByCode = await _db.Specialties
                .AnyAsync(s => s.Code != null && s.Code.ToLower() == req.Code.ToLower(), ct);

            if (existsByCode)
                return BadRequest(new { success = false, message = "Mã chuyên khoa đã tồn tại." });
        }

        var now = DateTime.UtcNow;
        var specialty = new Specialty
        {
            Name = req.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim().ToUpper(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Specialties.Add(specialty);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Tạo chuyên khoa thành công.",
            data = new
            {
                specialtyId = specialty.SpecialtyId,
                code = specialty.Code,
                name = specialty.Name,
                description = specialty.Description,
                isActive = specialty.IsActive
            }
        });
    }

    // ===================== Cập nhật chuyên khoa Admin =====================
    [HttpPut("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(
        Summary = "Cập nhật chuyên khoa",
        Description = "Cập nhật thông tin chuyên khoa (chỉ ADMIN).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> UpdateSpecialty(
        int id,
        [FromBody] UpdateSpecialtyRequest req,
        CancellationToken ct = default)
    {
        var specialty = await _db.Specialties.FindAsync(new object[] { id }, ct);
        if (specialty == null)
            return NotFound(new { success = false, message = "Không tìm thấy chuyên khoa." });

        // Validation
        if (req.Name != null)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { success = false, message = "Tên chuyên khoa không được để trống." });

            if (req.Name.Length > 150)
                return BadRequest(new { success = false, message = "Tên chuyên khoa không được vượt quá 150 ký tự." });

            // Check trùng tên (khác ID hiện tại)
            var existsByName = await _db.Specialties
                .AnyAsync(s => s.SpecialtyId != id && s.Name.ToLower() == req.Name.ToLower(), ct);

            if (existsByName)
                return BadRequest(new { success = false, message = "Tên chuyên khoa đã tồn tại." });

            specialty.Name = req.Name.Trim();
        }

        // Update Code
        if (req.Code != null)
        {
            var newCode = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim().ToUpper();

            if (newCode != null)
            {
                var existsByCode = await _db.Specialties
                    .AnyAsync(s => s.SpecialtyId != id && s.Code != null && s.Code.ToLower() == newCode.ToLower(), ct);

                if (existsByCode)
                    return BadRequest(new { success = false, message = "Mã chuyên khoa đã tồn tại." });
            }

            specialty.Code = newCode;
        }

        // Update Description
        if (req.Description != null)
        {
            specialty.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        }

        // Update IsActive
        if (req.IsActive.HasValue)
        {
            specialty.IsActive = req.IsActive.Value;
        }

        specialty.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Cập nhật chuyên khoa thành công.",
            data = new
            {
                specialtyId = specialty.SpecialtyId,
                code = specialty.Code,
                name = specialty.Name,
                description = specialty.Description,
                isActive = specialty.IsActive,
                updatedAt = specialty.UpdatedAt
            }
        });
    }

    // ===================== Xóa chuyên khoa Admin =====================
    [HttpDelete("admin/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(
        Summary = "Xóa chuyên khoa",
        Description = "Xóa (vô hiệu hóa) chuyên khoa (chỉ ADMIN).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> DeleteSpecialty(int id, CancellationToken ct = default)
    {
        var specialty = await _db.Specialties.FindAsync(new object[] { id }, ct);
        if (specialty == null)
            return NotFound(new { success = false, message = "Không tìm thấy chuyên khoa." });

        // Check xem có bác sĩ nào đang dùng chuyên khoa này không
        var hasActiveDoctors = await _db.Doctors
            .AnyAsync(d => d.PrimarySpecialtyId == id && d.IsActive, ct);

        if (hasActiveDoctors)
            return BadRequest(new
            {
                success = false,
                message = "Không thể xóa chuyên khoa này vì có bác sĩ đang hoạt động sử dụng."
            });

        // Soft delete: chỉ vô hiệu hóa
        specialty.IsActive = false;
        specialty.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã vô hiệu hóa chuyên khoa."
        });
    }

    // ===================== Kích hoạt lại chuyên khoa Admin =====================
    [HttpPatch("admin/{id:int}/activate")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(
        Summary = "Kích hoạt lại chuyên khoa",
        Description = "Kích hoạt lại chuyên khoa đã bị vô hiệu hóa (chỉ ADMIN).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> ActivateSpecialty(int id, CancellationToken ct = default)
    {
        var specialty = await _db.Specialties.FindAsync(new object[] { id }, ct);
        if (specialty == null)
            return NotFound(new { success = false, message = "Không tìm thấy chuyên khoa." });

        if (specialty.IsActive)
            return BadRequest(new { success = false, message = "Chuyên khoa đã được kích hoạt." });

        specialty.IsActive = true;
        specialty.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã kích hoạt lại chuyên khoa."
        });
    }

    // ===================== Danh sách chuyên khoa Admin - bao gồm inactive =====================
    [HttpGet("admin/list")]
    [Authorize(Roles = "ADMIN")]
    [SwaggerOperation(
        Summary = "Danh sách chuyên khoa (Admin)",
        Description = "Lấy tất cả chuyên khoa kể cả inactive (chỉ ADMIN).",
        Tags = new[] { "Specialty" })]
    public async Task<IActionResult> GetSpecialtiesAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.Specialties.AsNoTracking();

        // Filter by IsActive
        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(searchLower) ||
                (s.Code != null && s.Code.ToLower().Contains(searchLower))
            );
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(s => new
            {
                specialtyId = s.SpecialtyId,
                code = s.Code,
                name = s.Name,
                description = s.Description,
                isActive = s.IsActive,
                doctorCount = s.DoctorSpecialties.Count,
                createdAt = s.CreatedAt,
                updatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách chuyên khoa thành công.",
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
}