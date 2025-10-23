using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [MaxLength(30, ErrorMessage = "Số điện thoại quá dài.")]
        public string Phone { get; set; } = string.Empty;

        // Optional; nếu null sẽ mặc định 1, 10
        public int? Page { get; set; }
        public int? Limit { get; set; }
    }

    private static string NormalizePhone(string phone)
    {
        // Lấy số, đổi +84xxxx -> 0xxxx
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length >= 10) digits = "0" + digits[2..];
        return digits;
    }

    private static bool IsValidVnMobile(string normalized)
    {
        // 0 + (3/5/7/8/9) + 8 số = 10 số
        return Regex.IsMatch(normalized, @"^0(3|5|7|8|9)\d{8}$");
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
                DoctorName = e.Doctor.User!.FullName,
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

        return Ok(new { success = true, message = "Lấy hồ sơ thành công", data = dto });
    }

    // ============ 2) Tra cứu theo SỐ ĐIỆN THOẠI (+ DOB) ============
    [HttpPost("by-phone")]
    [SwaggerOperation(
    Summary = "Tra cứu danh sách hồ sơ theo số điện thoại",
    Description = "Nhập số điện thoại. Page + Limit để mặc định"
)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupByPhone([FromBody] LookupByPhoneRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

        // Chuẩn hóa + validate phone
        var phone = NormalizePhone(req.Phone);
        if (!IsValidVnMobile(phone))
            return BadRequest(new { success = false, message = "Số điện thoại không đúng định dạng Việt Nam." });

        // Tìm bệnh nhân theo phone
        var patientId = await _db.Patients.AsNoTracking()
            .Where(p => p.IsActive && p.Phone == phone)
            .Select(p => (long?)p.PatientId)
            .FirstOrDefaultAsync(ct);

        if (patientId is null)
            return NotFound(new { success = false, message = "Không tìm thấy bệnh nhân phù hợp." });

        // Phân trang: mặc định 1,10 nếu không gửi
        var page = (req.Page ?? 1) <= 0 ? 1 : req.Page!.Value;
        var limit = req.Limit is null ? 10 : Math.Clamp(req.Limit.Value, 1, 200);

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
                DoctorName = e.Doctor != null ? e.Doctor.User!.FullName : "—",
                ServiceName = e.Service != null ? e.Service.Name : null,
                Status = e.Status
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Lấy hồ sơ thành công",
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
