using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoMed.Api.Models;
using FoMed.Api.ViewModels.Patients;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace FoMed.Api.Controllers;

[ApiController]
[Route("api/v1/admin/patients")]

public class PatientsController : ControllerBase
{
    private readonly FoMedContext _db;
    public PatientsController(FoMedContext db) => _db = db;

    // === LIST ===
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Lấy danh sách bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPatients(
        [FromQuery] string? query,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var q = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var s = query.Trim();
            q = q.Where(x => x.FullName.Contains(s) ||
                             x.Phone.Contains(s) ||
                             (x.PatientCode != null && x.PatientCode.Contains(s)));
        }

        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive.Value);

        var total = await q.CountAsync(ct);
        var items = await q
    .OrderByDescending(x => x.CreatedAt)
    .Skip((page - 1) * limit)
    .Take(limit)
    .Select(x => new PatientListVm(
        x.PatientId,
        x.PatientCode,
        x.FullName,
        x.Gender,
        x.DateOfBirth == null ? null : x.DateOfBirth.Value.ToString("yyyy-MM-dd"),
        x.Phone,
        x.Email,
        x.Address,
        x.District,
        x.City,
        x.Province,
        x.IdentityNo,
        x.IsActive,
        x.CreatedAt
    ))
    .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy danh sách bệnh nhân thành công.",
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

    // === DETAIL ===
    [HttpGet("{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Lấy chi tiết bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientById(long id, CancellationToken ct = default)
    {
        var p = await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientId == id, ct);
        if (p == null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân." });

        var vm = new PatientDetailVm(
            p.PatientId, p.PatientCode, p.FullName, p.Gender, p.DateOfBirth,
            p.Phone, p.Email, p.Address, p.City, p.Province, p.District,
            p.IdentityNo, p.InsuranceNo, p.Note, p.AllergyText,
            p.IsActive, p.CreatedAt, p.UpdatedAt
        );

        return Ok(new { success = true, message = "Lấy chi tiết bệnh nhân thành công.", data = vm });
    }

    // === BY PHONE ===
    [HttpGet("by-phone")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Tra cứu bệnh nhân theo SĐT", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPatientByPhone([FromQuery] string phone, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(new { success = false, message = "Thiếu số điện thoại." });

        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Phone == phone, ct);

        return Ok(new
        {
            success = true,
            message = p == null ? "Không tìm thấy bệnh nhân với SĐT này." : "Tìm thấy bệnh nhân.",
            data = p
        });
    }

    // === CREATE ===
    [HttpPost("create")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Thêm bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePatient([FromBody] PatientCreateReq req, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors = ModelState });

        if (await _db.Patients.AnyAsync(x => x.Phone == req.Phone, ct))
            return Conflict(new { success = false, message = "Số điện thoại đã tồn tại." });

        var p = new Patient
        {
            FullName = req.FullName.Trim(),
            Gender = string.IsNullOrWhiteSpace(req.Gender) ? null : req.Gender!.Trim().ToUpperInvariant(),
            DateOfBirth = req.DateOfBirth,
            Phone = req.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email!.Trim(),
            Address = req.Address,
            District = req.District,
            City = req.City,
            Province = req.Province,
            IdentityNo = req.IdentityNo,
            InsuranceNo = req.InsuranceNo,
            Note = req.Note,
            AllergyText = req.AllergyText,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Patients.Add(p);
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Thêm bệnh nhân thành công.", data = new { p.PatientId } });
    }

    // === UPSERT BY PHONE ===
    [HttpPost("upsert-by-phone")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Tạo hoặc trả về nếu SĐT đã tồn tại", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertByPhone([FromBody] PatientCreateReq req, CancellationToken ct = default)
    {
        var existing = await _db.Patients.FirstOrDefaultAsync(x => x.Phone == req.Phone, ct);
        if (existing != null)
            return Ok(new { success = true, message = "Số điện thoại đã tồn tại, trả về bệnh nhân hiện có.", data = new { patientId = existing.PatientId, isNew = false } });

        var created = await CreatePatient(req, ct) as OkObjectResult;
        dynamic data = (created!.Value as dynamic)!;
        return Ok(new { success = true, message = "Tạo bệnh nhân mới thành công.", data = new { patientId = (long)data.data.PatientId, isNew = true } });
    }

    // === UPDATE ===
    [HttpPut("update/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Cập nhật bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long id, [FromBody] PatientUpdateReq req, CancellationToken ct = default)
    {
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.PatientId == id, ct);
        if (p == null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân." });

        // nếu đổi phone → kiểm tra trùng
        if (!string.Equals(p.Phone, req.Phone, StringComparison.OrdinalIgnoreCase))
        {
            var dup = await _db.Patients.AnyAsync(x => x.Phone == req.Phone && x.PatientId != id, ct);
            if (dup) return Conflict(new { success = false, message = "Số điện thoại đã tồn tại." });
        }

        p.FullName = req.FullName.Trim();
        p.Gender = string.IsNullOrWhiteSpace(req.Gender) ? null : req.Gender!.Trim().ToUpperInvariant();
        p.DateOfBirth = req.DateOfBirth;
        p.Phone = req.Phone.Trim();
        p.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email!.Trim();
        p.Address = req.Address;
        p.District = req.District;
        p.City = req.City;
        p.Province = req.Province;
        p.IdentityNo = req.IdentityNo;
        p.InsuranceNo = req.InsuranceNo;
        p.Note = req.Note;
        p.AllergyText = req.AllergyText;
        p.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Cập nhật bệnh nhân thành công." });
    }

    // === TOGGLE STATUS ===
    [HttpPatch("status/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Bật/Tắt bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleStatus(long id, [FromBody] ToggleStatusReq req, CancellationToken ct = default)
    {
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.PatientId == id, ct);
        if (p == null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân." });

        p.IsActive = req.IsActive;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = req.IsActive ? "Đã kích hoạt bệnh nhân." : "Đã vô hiệu hóa bệnh nhân." });
    }

    // === DELETE (soft) ===
    [HttpDelete("delete/{id:long}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Xoá (ẩn) bệnh nhân", Description = "EMPLOYEE/ADMIN")]
    [Authorize(Roles = "EMPLOYEE,ADMIN")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct = default)
    {
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.PatientId == id, ct);
        if (p == null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân." });

        p.IsActive = false;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã ẩn bệnh nhân." });
    }

    [HttpGet("/api/v1/user/my-patient-id")]
    [Authorize(Roles = "PATIENT")]
    [SwaggerOperation(
    Summary = "Lấy patientId của user đang đăng nhập",
    Description = "Tự động tạo patient nếu chưa có",
    Tags = new[] { "Patients - User" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPatientId(CancellationToken ct)
    {
        // Lấy userId từ token
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdStr, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        // Tìm patient theo userId
        var patient = await _db.Patients
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => new { p.PatientId, p.FullName, p.Phone })
            .FirstOrDefaultAsync(ct);

        // Nếu đã có patient, trả về
        if (patient != null)
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    patientId = patient.PatientId,
                    fullName = patient.FullName,
                    phone = patient.Phone,
                    isNew = false
                }
            });
        }

        // Nếu chưa có, tạo mới
        var user = await _db.Users
            .Where(u => u.UserId == userId)
            .Select(u => new { u.FullName, u.Phone, u.Email })
            .FirstOrDefaultAsync(ct);

        if (user == null)
            return NotFound(new { success = false, message = "User không tồn tại" });

        if (string.IsNullOrWhiteSpace(user.Phone))
            return BadRequest(new { success = false, message = "User chưa có số điện thoại" });

        var newPatient = new Patient
        {
            UserId = userId,
            FullName = user.FullName ?? "N/A",
            Phone = user.Phone,
            Email = user.Email,
            Gender = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Patients.Add(newPatient);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Đã tạo hồ sơ bệnh nhân",
            data = new
            {
                patientId = newPatient.PatientId,
                fullName = newPatient.FullName,
                phone = newPatient.Phone,
                isNew = true
            }
        });
    }
}
