using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/v1/lookup-result")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class LookupResultController : ControllerBase
{
    private readonly FoMedContext _db;
    public LookupResultController(FoMedContext db) => _db = db;

    // ============ DTOs ============
    public sealed class LookupByCodeRequest { public string Code { get; set; } = string.Empty; }

    public sealed class EncounterListItemDto
    {
        public string Code { get; init; } = string.Empty;       // HSFM-000123
        public DateTime VisitAt { get; init; }                  // e.CreatedAt
        public string DoctorName { get; init; } = string.Empty;
        public string? ServiceName { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    public sealed class LookupByPhoneRequest
    {
        public string Phone { get; set; } = string.Empty;
        public DateTime? Dob { get; set; }      // yyyy-MM-dd từ FE
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }


    // ============ 1) Tra cứu theo MÃ HỒ SƠ ============
    [HttpPost("by-code")]
    [SwaggerOperation(Summary = "Tra cứu chi tiết hồ sơ theo mã", Description = "Nhập HSFM-xxxxxx, trả chi tiết hồ sơ.")]
    public async Task<IActionResult> LookupEncounterByCode([FromBody] LookupByCodeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { success = false, message = "Vui lòng nhập mã hồ sơ." });

        var dto = await _db.Encounters
            .AsNoTracking()
            .Where(e => e.Code == req.Code.Trim())
            .Select(e => new
            {
                EncounterCode = e.Code!,
                VisitAt = e.CreatedAt,
                DoctorName = e.Doctor.FullName,
                ServiceName = e.Service != null ? e.Service.Name : null,
                Status = e.Status,
                // header bệnh nhân (ẩn bớt nếu cần)
                Patient = new
                {
                    FullName = e.Patient.FullName,
                    PatientCode = e.Patient.PatientCode,
                    Dob = e.Patient.DateOfBirth,
                    Gender = e.Patient.Gender == "M" ? "Nam" : (e.Patient.Gender == "F" ? "Nữ" : null)
                },
                Diagnosis = e.DiagnosisText,
                // đơn thuốc gần nhất
                Prescription = e.Prescriptions
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        Code = p.Code ?? ("DTFM-" + p.PrescriptionId),
                        Advice = p.Advice,
                        Warning = p.Warning,
                        Items = p.Items.OrderBy(i => i.ItemId).Select(i => new
                        {
                            MedicineName = i.Medicine != null ? i.Medicine.Name : (i.CustomName ?? "—"),
                            Strength = i.Medicine != null ? i.Medicine.Strength : null,
                            Form = i.Medicine != null ? i.Medicine.Form : null,
                            Dose = i.DoseText,
                            Frequency = i.FrequencyText,
                            Duration = i.DurationText,
                            Quantity = i.Quantity
                        }).ToList()
                    }).FirstOrDefault(),
                // xét nghiệm thuộc encounter
                Labs = e.EncounterLabTests
                    .OrderBy(l => l.EncLabTestId)
                    .Select(l => new
                    {
                        Name = l.LabTestId != null ? l.LabTest!.Name : l.CustomName,
                        Status = l.Status,
                        Result = l.LabResults
                            .OrderByDescending(r => r.ResultAt)
                            .Select(r => new { r.ResultAt, r.ResultStatus, r.ResultNote, r.FileUrl })
                            .FirstOrDefault()
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (dto == null)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });

        return Ok(new { success = true, message = "OK", data = dto });
    }

    // ============ 2) Tra cứu theo SỐ ĐIỆN THOẠI (+ DOB) ============
    [HttpPost("by-phone")]
    [SwaggerOperation(Summary = "Tra cứu danh sách hồ sơ theo số điện thoại",
        Description = "Nhập phone + ngày sinh (để xác thực nhẹ). Trả danh sách HSFM.*")]
    public async Task<IActionResult> LookupByPhone([FromBody] LookupByPhoneRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { success = false, message = "Vui lòng nhập số điện thoại." });

        var phone = req.Phone.Trim();

        // Xác định bệnh nhân theo phone (+ Dob nếu có)
        var patientQ = _db.Patients.AsNoTracking().Where(p => p.IsActive && p.Phone == phone);
        if (req.Dob.HasValue)
        {
            var from = req.Dob.Value.Date;
            var to = from.AddDays(1);
            patientQ = patientQ.Where(p =>
                p.DateOfBirth.HasValue &&
                p.DateOfBirth.Value >= from && p.DateOfBirth.Value < to);
        }

        var patientId = await patientQ.Select(p => (long?)p.PatientId).FirstOrDefaultAsync(ct);
        if (patientId is null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân phù hợp." });

        var page = Math.Max(1, req.Page);
        var limit = Math.Clamp(req.Limit, 1, 200);

        var q = _db.Encounters.AsNoTracking()
            .Where(e => e.PatientId == patientId.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.EncounterId)
            .Skip((page - 1) * limit).Take(limit)
            .Select(e => new EncounterListItemDto
            {
                Code = e.Code ?? ("HSFM-" + e.EncounterId),
                VisitAt = e.CreatedAt,
                DoctorName = e.Doctor.FullName,
                ServiceName = e.Service != null ? e.Service.Name : null,
                Status = e.Status
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
