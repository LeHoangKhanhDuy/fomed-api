using System.Security.Claims;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/lab-results")]
[Authorize]

public class MyLabResultsController : ControllerBase
{
    private readonly FoMedContext _db;
    public MyLabResultsController(FoMedContext db) => _db = db;

    private async Task<long?> GetSelfPatientIdAsync(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!long.TryParse(uidStr, out var userId)) return null;

        // Map User -> Patient
        return await _db.Patients.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => (long?)p.PatientId)
            .FirstOrDefaultAsync(ct);
    }

    private bool IsStaff() =>
        User.IsInRole("ADMIN") || User.IsInRole("DOCTOR");

    /* ========== DANH SÁCH PHIẾU ========== */
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách kết quả xét nghiệm theo bệnh nhân (phân trang)",
        Description = "PATIENT: chỉ xem của chính mình. ADMIN/DOCTOR: có thể chỉ định bệnh nhân qua patientId/patientCode.",
        Tags = new[] { "LabResults" })]
    public async Task<IActionResult> GetLabResults(
        [FromQuery] long? patientId, // dùng khi staff
        [FromQuery] string? patientCode,             // BN000567 (tuỳ chọn)
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 10,
        CancellationToken ct = default)
    {
        // Xác định PatientId mục tiêu
        long? targetPatientId = null;
        if (IsStaff())
        {
            if (patientId.HasValue)
            {
                targetPatientId = await _db.Patients.AsNoTracking()
                                   .Where(p => p.PatientId == patientId.Value && p.IsActive)
                                   .Select(p => (long?)p.PatientId)
                                   .FirstOrDefaultAsync(ct);
            }
            else if (!string.IsNullOrWhiteSpace(patientCode))
            {
                targetPatientId = await _db.Patients.AsNoTracking()
                                   .Where(p => p.PatientCode == patientCode && p.IsActive)
                                   .Select(p => (long?)p.PatientId)
                                   .FirstOrDefaultAsync(ct);
            }
        }

        // Nếu không phải staff hoặc không truyền vào -> xem của chính mình
        targetPatientId ??= await GetSelfPatientIdAsync(ct);

        if (targetPatientId is null)
            return Unauthorized(new { success = false, message = "Không xác định được bệnh nhân." });

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var q = _db.LabOrders.AsNoTracking().Where(x => x.PatientId == targetPatientId);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(o => o.SampleTakenAt).ThenByDescending(o => o.LabOrderId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(o => new LabResultListItemDto
            {
                Code = o.Code,
                SampleTakenAt = o.SampleTakenAt,
                ServiceName = o.Service.Name,
                Status = o.Status   // map Status (enum)
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "OK",
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
}