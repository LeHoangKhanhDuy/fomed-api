using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/v1/encounters")]
[Authorize]
public class EncounterController : ControllerBase
{
    private readonly FoMedContext _db;
    private readonly ILogger<EncounterController> _logger;

    public EncounterController(FoMedContext db, ILogger<EncounterController> logger)
    {
        _db = db;
        _logger = logger;
    }

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
            .OrderByDescending(e => e.FinalizedAt ?? e.CreatedAt)
            .ThenByDescending(e => e.EncounterId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(e => new EncounterListItemDto
            {
                Code = string.IsNullOrWhiteSpace(e.Code) ? ("HSFM-" + e.EncounterId) : e.Code!,
                VisitAt = e.FinalizedAt ?? e.CreatedAt,
                DoctorName = e.Doctor.User!.FullName,
                ServiceName = e.Service != null ? e.Service.Name : string.Empty,
                Status = e.Status ?? "draft"
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
    public async Task<IActionResult> GetEncounterDetail(
        [FromRoute] string codeOrId,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("🔍 GetEncounterDetail called with codeOrId: {CodeOrId}", codeOrId);

            long? encounterId = null;

            // Try parse as ID first
            if (long.TryParse(codeOrId, out var parsed))
            {
                encounterId = parsed;
                _logger.LogInformation("✅ Parsed as ID: {EncounterId}", encounterId);
            }
            else
            {
                // Try find by Code
                encounterId = await _db.Encounters.AsNoTracking()
                    .Where(e => e.Code == codeOrId)
                    .Select(e => (long?)e.EncounterId)
                    .FirstOrDefaultAsync(ct);
                _logger.LogInformation("📝 Found by Code '{Code}': {EncounterId}", codeOrId, encounterId);
            }

            if (encounterId is null)
            {
                _logger.LogWarning("❌ Encounter not found: {CodeOrId}", codeOrId);
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });
            }

            // Check if encounter exists first (simple query)
            var encounterExists = await _db.Encounters
                .Where(e => e.EncounterId == encounterId.Value)
                .Select(e => new { e.EncounterId, e.PatientId, e.DoctorId })
                .FirstOrDefaultAsync(ct);

            if (encounterExists is null)
            {
                _logger.LogWarning("❌ Encounter {EncounterId} does not exist", encounterId);
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });
            }

            _logger.LogInformation("✅ Encounter exists: ID={EncounterId}, PatientId={PatientId}",
                encounterExists.EncounterId, encounterExists.PatientId);

            // Check authorization
            if (!IsStaff())
            {
                var selfPatientId = await GetSelfPatientIdAsync(ct);
                _logger.LogInformation("👤 User's PatientId: {SelfPatientId}, Encounter's PatientId: {EncounterPatientId}",
                    selfPatientId, encounterExists.PatientId);

                if (selfPatientId is null || selfPatientId != encounterExists.PatientId)
                {
                    _logger.LogWarning("🚫 Forbidden: User does not have access to encounter {EncounterId}", encounterId);
                    return Forbid();
                }
            }

            // Now load full encounter with all navigation properties
            _logger.LogInformation("📦 Loading full encounter data...");

            var encounter = await _db.Encounters.AsNoTracking()
                .Include(e => e.Patient)
                .Include(e => e.Doctor)
                    .ThenInclude(d => d.User)
                .Include(e => e.Doctor.PrimarySpecialty)
                .Include(e => e.Service)
                .Include(e => e.Prescriptions)
                    .ThenInclude(p => p.Items)
                        .ThenInclude(i => i.Medicine)
                .Where(e => e.EncounterId == encounterId.Value)
                .FirstOrDefaultAsync(ct);

            if (encounter is null)
            {
                _logger.LogError("💥 Failed to load encounter {EncounterId} with includes", encounterId);
                return NotFound(new { success = false, message = "Không thể tải thông tin hồ sơ." });
            }

            _logger.LogInformation("✅ Encounter loaded successfully. Prescriptions: {Count}",
                encounter.Prescriptions.Count);

            // Get latest prescription
            var latestPrescription = encounter.Prescriptions
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (latestPrescription != null)
            {
                _logger.LogInformation("💊 Latest prescription: ID={PrescriptionId}, Items={ItemCount}",
                    latestPrescription.PrescriptionId, latestPrescription.Items.Count);
            }
            else
            {
                _logger.LogInformation("⚠️ No prescriptions found for encounter {EncounterId}", encounterId);
            }

            // Build response DTO
            var dto = new EncounterDetailDto
            {
                EncounterCode = string.IsNullOrWhiteSpace(encounter.Code)
                    ? ("HSFM-" + encounter.EncounterId)
                    : encounter.Code!,
                VisitAt = encounter.FinalizedAt ?? encounter.CreatedAt,

                // Prescription header
                PrescriptionCode = latestPrescription != null
                    ? (latestPrescription.Code ?? ("DTFM-" + latestPrescription.PrescriptionId))
                    : string.Empty,
                ExpiryAt = latestPrescription?.ExpiryAt,
                ErxCode = latestPrescription?.ErxCode,
                ErxStatus = latestPrescription?.ErxStatus,

                // Doctor
                DoctorName = encounter.Doctor?.User?.FullName ?? "N/A",
                LicenseNo = encounter.Doctor?.LicenseNo,
                ServiceName = encounter.Service?.Name,
                SpecialtyName = encounter.Doctor?.PrimarySpecialty?.Name,

                // Patient
                PatientFullName = encounter.Patient?.FullName ?? "N/A",
                PatientCode = encounter.Patient?.PatientCode,
                PatientDob = encounter.Patient?.DateOfBirth.HasValue == true
                    ? DateOnly.FromDateTime(encounter.Patient.DateOfBirth.Value)
                    : null,
                PatientGender = encounter.Patient?.Gender == "M"
                    ? "Nam"
                    : (encounter.Patient?.Gender == "F" ? "Nữ" : null),
                Diagnosis = encounter.DiagnosisText,
                Allergy = encounter.Patient?.AllergyText,

                // Prescription details
                Advice = latestPrescription?.Advice,
                Warning = latestPrescription?.Warning,

                Items = latestPrescription?.Items
                    .OrderBy(i => i.ItemId)
                    .Select(i => new EncounterDetailDrugDto
                    {
                        MedicineName = i.Medicine != null
                            ? i.Medicine.Name
                            : (i.CustomName ?? "—"),
                        Strength = i.Medicine?.Strength,
                        Form = i.Medicine?.Form,
                        Dose = string.IsNullOrWhiteSpace(i.FrequencyText)
                            ? i.DoseText
                            : ((i.DoseText ?? "").Trim() + " x " + i.FrequencyText),
                        Duration = i.DurationText,
                        Quantity = i.Quantity ?? 0,
                        Instruction = i.Note
                    }).ToList() ?? new List<EncounterDetailDrugDto>()
            };

            _logger.LogInformation("✅ Response built successfully with {ItemCount} items", dto.Items.Count);

            return Ok(new { success = true, message = "OK", data = dto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error in GetEncounterDetail for {CodeOrId}", codeOrId);
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi tải hồ sơ: " + ex.Message
            });
        }
    }
}