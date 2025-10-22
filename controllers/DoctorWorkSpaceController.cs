using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace FoMed.Api.Features.Doctor.Workspace;

[ApiController]
[Route("api/doctor-workspace")]
[Authorize(Roles = "DOCTOR")]
public sealed class DoctorPatientsController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorPatientsController(FoMedContext db) => _db = db;

    // ========= Helpers =========

    private async Task<(int doctorId, string? error)> GetDoctorIdAsync()
    {
        // Lấy userId từ JWT, tuỳ token của bạn có thể là "sub" hoặc "userId"
        var userIdStr = User.FindFirstValue("userId")
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(userIdStr, out var userId))
            return (0, "Không xác định được người dùng từ token.");

        var doctorId = await _db.Doctors
            .Where(d => d.UserId == userId)
            .Select(d => d.DoctorId)
            .FirstOrDefaultAsync();

        if (doctorId <= 0)
            return (0, "Tài khoản hiện tại không gắn với bác sĩ.");

        return (doctorId, null);
    }

    private static IActionResult ApiOk(object? data = null, string? message = null)
        => new OkObjectResult(new { success = true, message = message ?? "OK", data });

    private static IActionResult ApiError(int code, string message)
        => new ObjectResult(new { success = false, message }) { StatusCode = code };

    private static IActionResult Bad(string message) => ApiError(StatusCodes.Status400BadRequest, message);
    private static IActionResult ForbidApi(string message) => ApiError(StatusCodes.Status403Forbidden, message);
    private static IActionResult NotFoundApi(string message) => ApiError(StatusCodes.Status404NotFound, message);

    // ========= DTOs =========

    public sealed record StartEncounterReq(int AppointmentId);

    public sealed record DiagnosisReq(
        int AppointmentId,
        string Symptoms,
        string Diagnosis,
        string? Note
    );

    public sealed record LabOrderReq(
        int AppointmentId,
        List<int> TestIds,
        string? Note,
        string Priority = "normal" // normal | urgent
    );

    public sealed record RxLineReq(
        int MedicineId,
        string Dose,
        string Frequency,
        string Duration,
        string? Note
    );

    public sealed record PrescriptionReq(
        int AppointmentId,
        List<RxLineReq> Lines,
        string? Advice
    );

    // ========= Catalogs (phục vụ UI dropdown) =========

    [HttpGet("lab-tests")]
    [SwaggerOperation(Summary = "Danh mục xét nghiệm")]
    public async Task<IActionResult> GetLabTests()
    {
        var items = await _db.LabTests
            .OrderBy(x => x.Name)
            .Select(x => new { x.LabTestId, x.Code, x.Name })
            .ToListAsync();

        return ApiOk(items);
    }

    [HttpGet("medicines")]
    [SwaggerOperation(Summary = "Danh mục thuốc")]
    public async Task<IActionResult> GetMedicines()
    {
        var items = await _db.Medicines
            .OrderBy(x => x.Name)
            .Select(x => new { x.MedicineId, x.Name, x.Unit })
            .ToListAsync();

        return ApiOk(items);
    }

    // ========= 1) Bắt đầu khám (chuyển trạng thái lịch) =========

    [HttpPost("encounters/start")]
    [SwaggerOperation(Summary = "Bắt đầu khám: chuyển trạng thái lịch sang 'waiting'→'in_progress' (nếu sử dụng) hoặc bỏ qua")]
    public async Task<IActionResult> StartEncounter([FromBody] StartEncounterReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        // Không ép buộc trạng thái nếu bạn chưa có cột; nếu có: chỉ cho waiting/booked mới bắt đầu
        if (appt.Status is "cancelled" or "no_show" or "done")
            return Bad("Trạng thái lịch hẹn hiện tại không thể bắt đầu khám.");

        // appt.Status = "in_progress"; // nếu bạn có thêm trạng thái này
        await _db.SaveChangesAsync();

        return ApiOk(new { appt.AppointmentId, appt.Status }, "Đã sẵn sàng khám.");
    }

    // ========= 2) Lưu chẩn đoán =========

    [HttpPost("encounters/diagnosis")]
    [SwaggerOperation(Summary = "Lưu chẩn đoán, tạo Encounter nếu chưa có")]
    public async Task<IActionResult> SaveDiagnosis([FromBody] DiagnosisReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");
        if (string.IsNullOrWhiteSpace(req.Symptoms)) return Bad("Vui lòng nhập 'Triệu chứng'.");
        if (string.IsNullOrWhiteSpace(req.Diagnosis)) return Bad("Vui lòng nhập 'Chẩn đoán'.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");
        if (appt.Status is "cancelled" or "no_show") return Bad("Lịch hẹn đã hủy hoặc vắng mặt.");

        // Tạo Encounter nếu chưa có (1-1 theo lịch)
        var enc = await _db.Encounters
            .FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);

        if (enc == null)
        {
            enc = new Encounter
            {
                AppointmentId = appt.AppointmentId,
                PatientId = appt.PatientId,
                DoctorId = appt.DoctorId,
                ServiceId = appt.ServiceId
            };
            _db.Encounters.Add(enc);
        }

        // Các cột dưới đây giả định tồn tại (Symptoms/Diagnosis/Note); nếu tên khác, đổi theo entity của bạn
        enc.Symptoms = req.Symptoms.Trim();
        enc.DiagnosisText = req.Diagnosis.Trim();
        enc.DoctorNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        await _db.SaveChangesAsync();
        return ApiOk(new { enc.EncounterId }, "Đã lưu chẩn đoán.");
    }

    // ========= 3) Tạo chỉ định xét nghiệm =========

    [HttpPost("encounters/lab-orders")]
    [SwaggerOperation(Summary = "Tạo chỉ định xét nghiệm cho Encounter của lịch")]
    public async Task<IActionResult> CreateLabOrder([FromBody] LabOrderReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");
        if (req.TestIds == null || req.TestIds.Count == 0) return Bad("Vui lòng chọn ít nhất 1 xét nghiệm.");
        if (req.Priority is not ("normal" or "urgent")) return Bad("Giá trị 'Priority' không hợp lệ (normal|urgent).");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        var enc = await _db.Encounters
            .FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);

        if (enc == null) return Bad("Chưa có Encounter. Vui lòng lưu chẩn đoán trước khi chỉ định xét nghiệm.");

        // Kiểm tra test id hợp lệ
        var validTests = await _db.LabTests
            .Where(t => req.TestIds.Contains(t.LabTestId))
            .Select(t => new { t.LabTestId, t.Name, t.Code })
            .ToListAsync();

        var missing = req.TestIds.Except(validTests.Select(x => x.LabTestId)).ToArray();
        if (missing.Length > 0) return Bad($"Các xét nghiệm không tồn tại: {string.Join(", ", missing)}.");

        using var tx = await _db.Database.BeginTransactionAsync();

        // Tạo LabOrder
        var code = $"LO{DateTime.UtcNow:yyMMddHHmmssfff}";
        var order = new LabOrder
        {
            EncounterId = enc.EncounterId,
            PatientId = enc.PatientId,
            Code = code,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note!.Trim(),
            SampleType = null,
            Status = LabStatus.Processing
        };
        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync();

        // Thêm item + link EncounterLabTest
        foreach (var t in validTests)
        {
            var oli = new LabOrderItem
            {
                LabOrderId = order.LabOrderId,
                TestName = t.Name,
                ResultValue = "-",       // chưa có
                Unit = "",
                ReferenceText = "",
                Note = "",
                DisplayOrder = 0
            };
            _db.LabOrderItems.Add(oli);

            _db.EncounterLabTests.Add(new EncounterLabTest
            {
                EncounterId = enc.EncounterId,
                LabTestId = t.LabTestId,
                Status = "ordered"
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ApiOk(new { order.LabOrderId, order.Code }, "Đã tạo chỉ định xét nghiệm.");
    }

    // ========= 4) Kê toa =========

    [HttpPost("encounters/prescriptions")]
    [SwaggerOperation(Summary = "Tạo toa thuốc cho Encounter của lịch")]
    public async Task<IActionResult> CreatePrescription([FromBody] PrescriptionReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");
        if (req.Lines == null || req.Lines.Count == 0) return Bad("Vui lòng thêm ít nhất 1 thuốc.");
        if (req.Lines.Any(l => l.MedicineId <= 0 || string.IsNullOrWhiteSpace(l.Dose) ||
                                string.IsNullOrWhiteSpace(l.Frequency) || string.IsNullOrWhiteSpace(l.Duration)))
            return Bad("Mỗi thuốc phải có MedicineId, Dose, Frequency, Duration.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        var enc = await _db.Encounters
            .FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);

        if (enc == null) return Bad("Chưa có Encounter. Vui lòng lưu chẩn đoán trước khi kê toa.");

        // validate thuốc tồn tại
        var medIds = req.Lines.Select(x => x.MedicineId).Distinct().ToList();
        var existedMeds = await _db.Medicines.Where(m => medIds.Contains(m.MedicineId))
                            .Select(m => m.MedicineId).ToListAsync();
        var missMeds = medIds.Except(existedMeds).ToArray();
        if (missMeds.Length > 0) return Bad($"Các thuốc không tồn tại: {string.Join(", ", missMeds)}.");

        using var tx = await _db.Database.BeginTransactionAsync();

        var rx = new EncounterPrescription
        {
            EncounterId = enc.EncounterId,
            Advice = string.IsNullOrWhiteSpace(req.Advice) ? null : req.Advice!.Trim()
        };
        _db.EncounterPrescriptions.Add(rx);
        await _db.SaveChangesAsync();

        foreach (var line in req.Lines)
        {
            _db.PrescriptionItems.Add(new PrescriptionItem
            {
                PrescriptionId = rx.PrescriptionId,
                MedicineId = line.MedicineId,
                DoseText = line.Dose.Trim(),
                FrequencyText = line.Frequency.Trim(),
                DurationText = line.Duration.Trim(),
                Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note!.Trim()
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ApiOk(new { rx.PrescriptionId }, "Đã lưu toa thuốc.");
    }

    // ========= 5) Hoàn tất khám =========

    [HttpPost("encounters/complete")]
    [SwaggerOperation(Summary = "Hoàn tất khám: đổi trạng thái lịch 'done'")]
    public async Task<IActionResult> CompleteEncounter([FromBody] StartEncounterReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        appt.Status = "done";  // constraint đã có ('waiting','booked','done','cancelled','no_show')
        await _db.SaveChangesAsync();

        return ApiOk(new { appt.AppointmentId, appt.Status }, "Đã hoàn tất khám.");
    }
}
