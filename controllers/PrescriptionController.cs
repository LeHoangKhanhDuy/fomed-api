using System.Security.Claims;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/prescriptions")]
[Authorize]
public class PrescriptionController : ControllerBase
{
    private readonly FoMedContext _db;
    public PrescriptionController(FoMedContext db) => _db = db;

    private async Task<long?> GetSelfPatientIdAsync(CancellationToken ct)
    {
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        if (!long.TryParse(uidStr, out var userId)) return null;

        return await _db.Patients.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => (long?)p.PatientId)
            .FirstOrDefaultAsync(ct);
    }

    private bool IsStaff() => User.IsInRole("ADMIN") || User.IsInRole("DOCTOR");

    /* ========== DANH SÁCH ĐƠN THUỐC THEO BỆNH NHÂN (PHÂN TRANG) ========== */
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách đơn thuốc theo bệnh nhân",
        Description = "PATIENT: chỉ xem của chính mình. ADMIN/DOCTOR: có thể chỉ định bệnh nhân qua patientId/patientCode.",
        Tags = new[] { "Prescriptions" })]
    public async Task<IActionResult> GetPrescriptions(
        [FromQuery] long? patientId,
        [FromQuery] string? patientCode,
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 20,
        CancellationToken ct = default)
    {
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
                // chỉ dùng nếu bạn đã thêm PatientCode vào bảng Patients
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

        // EncounterPrescriptions -> Encounters (để biết PatientId/Doctor/Diagnosis)
        var q = _db.EncounterPrescriptions
            .AsNoTracking()
            .Where(p => p.Encounter.PatientId == targetPatientId);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PrescriptionId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(p => new PrescriptionListItemDto
            {
                Code = p.Code ?? ("DTFM-" + p.PrescriptionId),
                PrescribedAt = p.CreatedAt,
                DoctorName = p.Encounter.Doctor.User!.FullName,
                Diagnosis = p.Encounter.DiagnosisText
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "OK",
            data = new
            {
                patientId = targetPatientId,
                page,
                limit,
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* ========== CHI TIẾT ĐƠN THUỐC ========== */
    [HttpGet("details/{idOrCode}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Chi tiết đơn thuốc (theo ID hoặc Code)",
        Description = "Trả về thông tin bác sĩ, chẩn đoán, danh sách thuốc, ghi chú và cảnh báo.",
        Tags = new[] { "Prescriptions" })]
    public async Task<IActionResult> GetPrescriptionDetail(string idOrCode, CancellationToken ct)
    {
        // Tìm ID từ code hoặc parse số
        long? presId = null;
        if (long.TryParse(idOrCode, out var id)) presId = id;
        else
            presId = await _db.EncounterPrescriptions.AsNoTracking()
                        .Where(p => p.Code == idOrCode)
                        .Select(p => (long?)p.PrescriptionId)
                        .FirstOrDefaultAsync(ct);

        if (presId is null)
            return NotFound(new { success = false, message = "Không tìm thấy đơn thuốc." });

        var dto = await _db.EncounterPrescriptions
            .AsNoTracking()
            .Where(p => p.PrescriptionId == presId.Value)
            .Select(p => new PrescriptionDetailDto
            {
                Code = string.IsNullOrWhiteSpace(p.Code) ? "DTFM-" + p.PrescriptionId : p.Code!,
                PrescribedAt = p.CreatedAt,
                DoctorName = p.Encounter.Doctor.User!.FullName,
                Diagnosis = p.Encounter.DiagnosisText ?? "(Chưa có)",
                Advice = p.Advice,
                Warning = p.Warning,
                Items = p.Items
                    .OrderBy(i => i.ItemId)
                    .Select(i => new PrescriptionDetailItemDto
                    {
                        MedicineName = i.Medicine != null ? i.Medicine.Name : (i.CustomName ?? "—"),
                        Strength = i.Medicine!.Strength,     // null-safe vì EF chỉ đọc nếu Medicine != null
                        Form = i.Medicine!.Form,
                        Dose = i.DoseText,
                        Duration = i.DurationText,
                        Quantity = i.Quantity ?? 0,           // DTO non-null
                        Instruction = i.Note                     // cột "Hướng dẫn" trên UI
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (dto is null)
            return NotFound(new { success = false, message = "Không tìm thấy đơn thuốc." });

        return Ok(new { success = true, message = "OK", data = dto });
    }
}
