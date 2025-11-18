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
public sealed class DoctorWorkspaceController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorWorkspaceController(FoMedContext db) => _db = db;

    // ========= Helpers =========
    private async Task<(int doctorId, string? error)> GetDoctorIdAsync()
    {
        // Lấy userId 
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
            return (0, "Không thể chọn lịch khám của bác sĩ khác!");

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
        string Priority = "normal"
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

    // ========= Catalogs =========
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
    [SwaggerOperation(Summary = "Danh mục thuốc (có tồn kho)")]
    public async Task<IActionResult> GetMedicines()
    {
        var now = DateTime.UtcNow;

        var items = await _db.Medicines
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                medicineId = x.MedicineId,
                name = x.Name,
                unit = x.Unit,
                isActive = x.IsActive,
                stock = x.Lots.Where(l => l.ExpiryDate == null || l.ExpiryDate > now).Sum(l => l.Quantity)
            })
            .ToListAsync();

        return ApiOk(items);
    }

    // =========  Bắt đầu khám  =========

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

        // Không ép buộc trạng thái 
        if (appt.Status is "cancelled" or "no_show" or "done")
            return Bad("Trạng thái lịch hẹn hiện tại không thể bắt đầu khám.");

        await _db.SaveChangesAsync();

        return ApiOk(new { appt.AppointmentId, appt.Status }, "Đã sẵn sàng khám.");
    }

    // ========= Lưu chẩn đoán =========

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

        // Các cột dưới đây giả định tồn tại
        enc.Symptoms = req.Symptoms.Trim();
        enc.DiagnosisText = req.Diagnosis.Trim();
        enc.DoctorNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        await _db.SaveChangesAsync();
        return ApiOk(new { enc.EncounterId }, "Đã lưu chẩn đoán.");
    }

    // ========= Tạo chỉ định xét nghiệm =========

    [HttpPost("encounters/lab-orders")]
    [SwaggerOperation(Summary = "Tạo chỉ định xét nghiệm cho Encounter của lịch")]
    public async Task<IActionResult> CreateLabOrder([FromBody] LabOrderReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");

        if ((req.TestIds == null || req.TestIds.Count == 0) && string.IsNullOrWhiteSpace(req.Note))
            return Bad("Vui lòng chọn ít nhất 1 xét nghiệm HOẶC ghi rõ lý do không xét nghiệm.");

        if (req.Priority is not ("normal" or "urgent"))
            return Bad("Giá trị 'Priority' không hợp lệ (normal|urgent).");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .AsTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);

        if (enc == null) return Bad("Chưa có Encounter. Vui lòng lưu chẩn đoán trước khi chỉ định xét nghiệm.");

        // Nếu không có xét nghiệm chỉ lưu ghi chú vào Encounter
        if (req.TestIds == null || req.TestIds.Count == 0)
        {
            await _db.SaveChangesAsync();
            return ApiOk(null, "Đã ghi nhận không có xét nghiệm.");
        }

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
            Status = LabStatus.Processing,
            DoctorId = doctorId,
            ServiceId = enc.ServiceId ?? 0,
            SampleTakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync();

        // Thêm item, link EncounterLabTest
        foreach (var t in validTests)
        {
            var oli = new LabOrderItem
            {
                LabOrderId = order.LabOrderId,
                LabTestId = t.LabTestId,
                TestName = t.Name,
                ResultValue = "-",
                Unit = "",
                ReferenceText = "",
                Note = "",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.LabOrderItems.Add(oli);

            _db.EncounterLabTests.Add(new EncounterLabTest
            {
                EncounterId = enc.EncounterId,
                LabTestId = t.LabTestId,
                Status = "ordered",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return ApiOk(new { order.LabOrderId, order.Code }, "Đã tạo chỉ định xét nghiệm.");
    }

    // ========= Kê toa =========
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

        // Validate medicines
        var medIds = req.Lines.Select(x => x.MedicineId).Distinct().ToList();
        var existed = await _db.Medicines
            .Where(m => medIds.Contains(m.MedicineId))
            .Select(m => m.MedicineId)
            .ToListAsync();

        var missing = medIds.Except(existed).ToArray();
        if (missing.Any()) return Bad($"Thuốc không tồn tại: {string.Join(", ", missing)}.");

        //
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var rxCode = $"RX{now:yyMMddHHmmssfff}";

                var rx = new EncounterPrescription
                {
                    EncounterId = enc.EncounterId,
                    Code = rxCode,
                    Advice = string.IsNullOrWhiteSpace(req.Advice) ? null : req.Advice.Trim(),
                    CreatedAt = now,
                    ExpiryAt = now.AddMonths(3),
                    ErxCode = null,
                    ErxStatus = null,
                    Warning = null
                };
                _db.EncounterPrescriptions.Add(rx);
                await _db.SaveChangesAsync();

                foreach (var line in req.Lines)
                {
                    var item = new PrescriptionItem
                    {
                        PrescriptionId = rx.PrescriptionId,
                        MedicineId = line.MedicineId,
                        CustomName = null,
                        DoseText = line.Dose.Trim(),
                        FrequencyText = line.Frequency.Trim(),
                        DurationText = line.Duration.Trim(),
                        Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim(),
                        Quantity = null,
                        UnitPrice = null,
                        CreatedAt = now
                    };
                    _db.PrescriptionItems.Add(item);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiOk(new { rx.PrescriptionId, rx.Code }, "Đã lưu toa thuốc.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiError(500, "Lỗi lưu toa thuốc: " + (ex.InnerException?.Message ?? ex.Message));
            }
        });
    }

    // ========= Hoàn tất khám =========
    [HttpPost("encounters/complete")]
    [SwaggerOperation(Summary = "Hoàn tất khám: đổi trạng thái lịch 'done' và (tùy cấu hình) tạo hóa đơn thanh toán")]
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
        if (appt.Status is "cancelled" or "no_show") return Bad("Lịch hẹn đã hủy hoặc vắng mặt.");

        // tránh tạo nhiều hóa đơn cho cùng appointment
        var existingInv = await _db.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appt.AppointmentId);
        if (existingInv != null)
        {
            return ApiOk(new { appt.AppointmentId, appt.Status, invoiceId = existingInv.InvoiceId, invoiceCode = existingInv.Code },
                         "Đã hoàn tất khám (hóa đơn đã tồn tại).");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                appt.Status = "done";
                var now = DateTime.UtcNow;

                // ensure Encounter exists
                var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);
                if (enc == null)
                {
                    enc = new Encounter
                    {
                        AppointmentId = appt.AppointmentId,
                        PatientId = appt.PatientId,
                        DoctorId = appt.DoctorId,
                        ServiceId = appt.ServiceId,
                        CreatedAt = now
                    };
                    _db.Encounters.Add(enc);
                    await _db.SaveChangesAsync();
                }

                // tạo Invoice
                var invoiceCode = $"INV{now:yyMMddHHmmssfff}";
                var invoice = new Invoice
                {
                    AppointmentId = appt.AppointmentId,
                    EncounterId = enc.EncounterId,
                    PatientId = appt.PatientId,
                    Code = invoiceCode,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Status = "unpaid",
                    Subtotal = 0m,
                    DiscountAmt = 0m,
                    TaxAmt = 0m,
                    TotalAmount = 0m
                };

                // LẤY TÊN BỆNH NHÂN 
                if (appt.PatientId > 0)
                {
                    var patient = await _db.Patients.AsNoTracking()
                                      .Where(p => p.PatientId == appt.PatientId)
                                      .Select(p => new { p.FullName })
                                      .FirstOrDefaultAsync();
                    invoice.PatientName = patient?.FullName ?? "";
                }
                else
                {
                    invoice.PatientName = "";
                }
                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync(); // để có InvoiceId

                var itemsToAdd = new List<InvoiceItem>();
                decimal subtotal = 0m;

                // 1) Service charge 
                if (appt.ServiceId > 0)
                {
                    // Dùng DbSet<Service> cụ thể, không dùng _db.Set<Service>()
                    var service = await _db.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ServiceId == appt.ServiceId);

                    // Truy cập trực tiếp service.BasePrice và kiểm tra null (HasValue)
                    if (service != null && service.BasePrice.HasValue && service.BasePrice.Value > 0)
                    {
                        var it = new InvoiceItem
                        {
                            InvoiceId = invoice.InvoiceId,
                            ItemType = "service",
                            RefType = "Service",
                            RefId = appt.ServiceId,
                            // Lấy tên trực tiếp
                            Description = $"Khám: {service.Name ?? "Service"}",
                            Quantity = 1,
                            // Lấy giá trị từ BasePrice.Value
                            UnitPrice = service.BasePrice.Value,
                            CreatedAt = now
                        };
                        itemsToAdd.Add(it);
                        subtotal += it.Quantity * it.UnitPrice;
                    }
                }

                // 2) Lab orders -> sử dụng LabOrder.Items
                var labOrders = await _db.LabOrders
                .Where(lo => lo.EncounterId == enc.EncounterId)
                .Include(lo => lo.Items)
                .ThenInclude(item => item.LabTest) 
                .ToListAsync();

                foreach (var lo in labOrders)
                {
                    foreach (var loi in lo.Items)
                    {
                        decimal unitPrice = 0m;

                        // LabTestId + Navigation Property
                        if (loi.LabTestId.HasValue && loi.LabTest != null)
                        {
                            unitPrice = loi.LabTest.BasePrice ?? 0m;
                        }
                        else
                        {
                            // Nếu không có LabTestId, thử tìm theo tên 
                            var labTest = await _db.LabTests.AsNoTracking()
                                .FirstOrDefaultAsync(t => t.Name == loi.TestName);

                            if (labTest != null && labTest.BasePrice.HasValue)
                            {
                                unitPrice = labTest.BasePrice.Value;
                            }
                        }

                        // Chỉ thêm vào hóa đơn nếu có giá
                        if (unitPrice > 0)
                        {
                            var it = new InvoiceItem
                            {
                                InvoiceId = invoice.InvoiceId,
                                ItemType = "lab",
                                RefType = "LabOrderItem",
                                RefId = loi.LabOrderItemId,
                                Description = $"Xét nghiệm: {loi.TestName}",
                                Quantity = 1,
                                UnitPrice = unitPrice,
                                CreatedAt = now
                            };
                            itemsToAdd.Add(it);
                            subtotal += it.Quantity * it.UnitPrice;
                        }
                    }
                }

                // 3) Prescription items
                var prescriptions = await _db.EncounterPrescriptions
                .Where(p => p.EncounterId == enc.EncounterId)
                .Include(p => p.Items)
                .ToListAsync();

                foreach (var p in prescriptions)
                {
                    foreach (var pi in p.Items)
                    {
                        var qty = pi.Quantity ?? 1m;
                        var unitPrice = pi.UnitPrice ?? 0m;

                        string medicineName = "Medicine"; // Tên dự phòng

                        if (unitPrice == 0m && pi.MedicineId.HasValue)
                        {
                            var med = await _db.Medicines.AsNoTracking()
                                .FirstOrDefaultAsync(m => m.MedicineId == pi.MedicineId.Value);

                            if (med != null)
                            {
                                medicineName = med.Name ?? "Medicine";

                                if (med.BasePrice > 0)
                                {
                                    unitPrice = med.BasePrice;
                                }
                            }
                        }

                        if (unitPrice > 0)
                        {
                            var it = new InvoiceItem
                            {
                                InvoiceId = invoice.InvoiceId,
                                ItemType = "medicine",
                                RefType = "PrescriptionItem",
                                RefId = pi.ItemId,
                                // Lấy tên an toàn
                                Description = $"Thuốc: {pi.CustomName ?? medicineName}",
                                Quantity = qty,
                                UnitPrice = unitPrice,
                                CreatedAt = now
                            };
                            itemsToAdd.Add(it);
                            subtotal += it.Quantity * it.UnitPrice;
                        }
                    }
                }

                if (itemsToAdd.Any())
                {
                    _db.InvoiceItems.AddRange(itemsToAdd);
                }

                // Cập nhật subtotal & total 
                invoice.Subtotal = subtotal;
                invoice.TotalAmount = subtotal - invoice.DiscountAmt + invoice.TaxAmt;
                invoice.UpdatedAt = DateTime.UtcNow;
                _db.Invoices.Update(invoice);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return ApiOk(new { appt.AppointmentId, appt.Status, invoiceId = invoice.InvoiceId, invoiceCode = invoice.Code },
                             "Đã hoàn tất khám và tạo hóa đơn (nếu có item có giá).");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiError(500, "Lỗi khi hoàn tất khám / tạo hóa đơn: " + (ex.InnerException?.Message ?? ex.Message));
            }
        });
    }
}
