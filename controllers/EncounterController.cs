using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;

[ApiController]
[Route("api/v1/encounters")]
[Authorize]
public class EncounterController : ControllerBase
{
    private readonly FoMedContext _db;
    public EncounterController(FoMedContext db) => _db = db;

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

    /* --------- Lịch sử khám bệnh (phân trang) --------- */
    [HttpGet]
    [SwaggerOperation(Summary = "Lịch sử khám bệnh theo bệnh nhân (phân trang)",
        Description = "PATIENT: chỉ xem của mình. ADMIN/DOCTOR: chỉ định qua patientId/patientCode.",
        Tags = new[] { "Encounters" })]
    public async Task<IActionResult> GetHistory(
        [FromQuery] long? patientId,
        [FromQuery] string? patientCode,
        [FromQuery] int page = 1,
        [FromQuery(Name = "limit")] int limit = 20,
        CancellationToken ct = default)
    {
        long? targetPid = null;

        if (IsStaff())
        {
            if (patientId.HasValue)
                targetPid = await _db.Patients.AsNoTracking()
                    .Where(p => p.PatientId == patientId.Value && p.IsActive)
                    .Select(p => (long?)p.PatientId).FirstOrDefaultAsync(ct);
            else if (!string.IsNullOrWhiteSpace(patientCode) &&
                     _db.Model.FindEntityType(typeof(Patient))!.FindProperty("PatientCode") != null)
                targetPid = await _db.Patients.AsNoTracking()
                    .Where(p => p.PatientCode == patientCode && p.IsActive)
                    .Select(p => (long?)p.PatientId).FirstOrDefaultAsync(ct);
        }

        targetPid ??= await GetSelfPatientIdAsync(ct);
        if (targetPid is null)
            return Unauthorized(new { success = false, message = "Không xác định được bệnh nhân." });

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var q = _db.Encounters.AsNoTracking().Where(e => e.PatientId == targetPid);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.EncounterId)
            .Skip((page - 1) * limit).Take(limit)
            .Select(e => new EncounterListItemDto
            {
                Code = string.IsNullOrWhiteSpace(e.Code) ? ("HSFM-" + e.EncounterId) : e.Code!,
                VisitAt = e.CreatedAt,
                DoctorName = e.Doctor.User!.FullName,
                ServiceName = e.Service != null ? e.Service.Name : "",
                Status = e.Status
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "OK",
            data = new
            {
                patientId = targetPid,
                page,
                limit,
                totalItems = total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
                items
            }
        });
    }

    /* --------- Chi tiết hồ sơ khám --------- */
    [HttpGet("details/{codeOrId}")]
    [SwaggerOperation(Summary = "Chi tiết hồ sơ khám (theo ID hoặc Code)",
        Description = "Trả về thông tin bác sĩ, bệnh nhân, đơn thuốc, ghi chú & cảnh báo.",
        Tags = new[] { "Encounters" })]
    public async Task<IActionResult> GetEncounterDetail([FromRoute] string codeOrId, CancellationToken ct = default)
    {
        long? encounterId = null;

        if (long.TryParse(codeOrId, out var parsed))
            encounterId = parsed;
        else
            encounterId = await _db.Encounters.AsNoTracking()
                .Where(e => e.Code == codeOrId)
                .Select(e => (long?)e.EncounterId)
                .FirstOrDefaultAsync(ct);

        if (encounterId is null)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });

        // Chọn 1 đơn thuốc của encounter (mới nhất)
        var dto = await _db.Encounters.AsNoTracking()
            .Where(e => e.EncounterId == encounterId.Value)
            .Select(e => new EncounterDetailDto
            {
                EncounterCode = string.IsNullOrWhiteSpace(e.Code) ? ("HSFM-" + e.EncounterId) : e.Code!,
                VisitAt = e.CreatedAt,

                // Prescription header (lấy đơn mới nhất)
                PrescriptionCode = e.Prescriptions
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.Code ?? ("DTFM-" + p.PrescriptionId))
                .FirstOrDefault() ?? "",
                ExpiryAt = e.Prescriptions.OrderByDescending(p => p.CreatedAt).Select(p => p.ExpiryAt).FirstOrDefault(),
                ErxCode = e.Prescriptions.OrderByDescending(p => p.CreatedAt).Select(p => p.ErxCode).FirstOrDefault(),
                ErxStatus = e.Prescriptions.OrderByDescending(p => p.CreatedAt).Select(p => p.ErxStatus).FirstOrDefault(),

                // Doctor
                DoctorName = e.Doctor.User!.FullName,
                LicenseNo = e.Doctor.LicenseNo,
                ServiceName = e.Service != null ? e.Service.Name : null,
                SpecialtyName = e.Doctor.PrimarySpecialty != null ? e.Doctor.PrimarySpecialty.Name : null,

                // Patient
                PatientFullName = e.Patient.FullName,
                PatientCode = e.Patient.PatientCode, // nếu bạn đã thêm cột này
                PatientDob = e.Patient.DateOfBirth.HasValue
                      ? DateOnly.FromDateTime(e.Patient.DateOfBirth.Value)
                      : (DateOnly?)null,
                PatientGender = e.Patient.Gender == "M" ? "Nam" : (e.Patient.Gender == "F" ? "Nữ" : null),
                Diagnosis = e.DiagnosisText,
                Allergy = e.Patient.AllergyText,

                Advice = e.Prescriptions.OrderByDescending(p => p.CreatedAt).Select(p => p.Advice).FirstOrDefault(),
                Warning = e.Prescriptions.OrderByDescending(p => p.CreatedAt).Select(p => p.Warning).FirstOrDefault(),

                Items = e.Prescriptions.OrderByDescending(p => p.CreatedAt)
                .SelectMany(p => p.Items.OrderBy(i => i.ItemId))
                .Select(i => new EncounterDetailDrugDto
                {
                    MedicineName = i.Medicine != null ? i.Medicine.Name : (i.CustomName ?? "—"),
                    Strength = i.Medicine != null ? i.Medicine.Strength : null,
                    Form = i.Medicine != null ? i.Medicine.Form : null,
                    Dose = string.IsNullOrWhiteSpace(i.FrequencyText)
                            ? i.DoseText
                            : ((i.DoseText ?? "").Trim() + " x " + i.FrequencyText),
                    Duration = i.DurationText,
                    Quantity = i.Quantity ?? 0,
                    Instruction = i.Note
                }).ToList()
            }).FirstOrDefaultAsync(ct);

        if (dto is null)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });

        return Ok(new { success = true, message = "OK", data = dto });
    }
}
