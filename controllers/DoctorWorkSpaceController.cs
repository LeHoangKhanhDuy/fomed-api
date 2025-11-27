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
    [SwaggerOperation(Summary = "Danh mục thuốc (có tồn kho khả dụng)")]
    public async Task<IActionResult> GetMedicines()
    {
        var now = DateTime.UtcNow;

        // Lấy danh sách thuốc và tính tồn kho chỉ từ các lô còn hạn sử dụng
        var items = await _db.Medicines
            .AsNoTracking()
            .Where(x => x.IsActive) // Chỉ lấy thuốc đang hoạt động
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                medicineId = x.MedicineId,
                name = x.Name,
                unit = x.Unit,
                isActive = x.IsActive,
                // Tồn kho = Tổng số lượng của các lô chưa hết hạn
                stock = x.Lots
                    .Where(l => l.ExpiryDate == null || l.ExpiryDate > now)
                    .Sum(l => (decimal?)l.Quantity) ?? 0m
            })
            .ToListAsync();

        return ApiOk(items);
    }

    // =========  Bắt đầu khám  =========

    [HttpPost("encounters/start")]
    [SwaggerOperation(Summary = "Bắt đầu khám")]
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

        if (appt.Status is "cancelled" or "no_show" or "done")
            return Bad("Trạng thái lịch hẹn hiện tại không thể bắt đầu khám.");

        // Có thể cập nhật trạng thái ở đây nếu cần (ví dụ: "in_progress")
        // appt.Status = "in_progress";
        await _db.SaveChangesAsync();

        return ApiOk(new { appt.AppointmentId, appt.Status }, "Đã sẵn sàng khám.");
    }

    // ========= Lưu chẩn đoán =========

    [HttpPost("encounters/diagnosis")]
    [SwaggerOperation(Summary = "Lưu chẩn đoán")]
    public async Task<IActionResult> SaveDiagnosis([FromBody] DiagnosisReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");
        if (string.IsNullOrWhiteSpace(req.Symptoms)) return Bad("Vui lòng nhập 'Triệu chứng'.");
        if (string.IsNullOrWhiteSpace(req.Diagnosis)) return Bad("Vui lòng nhập 'Chẩn đoán'.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);

        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        // Tạo hoặc cập nhật Encounter
        var enc = await _db.Encounters
            .FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);

        if (enc == null)
        {
            enc = new Encounter
            {
                AppointmentId = appt.AppointmentId,
                PatientId = appt.PatientId,
                DoctorId = appt.DoctorId,
                ServiceId = appt.ServiceId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Encounters.Add(enc);
        }

        enc.Symptoms = req.Symptoms.Trim();
        enc.DiagnosisText = req.Diagnosis.Trim();
        enc.DoctorNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        await _db.SaveChangesAsync();
        return ApiOk(new { enc.EncounterId }, "Đã lưu chẩn đoán.");
    }

    // ========= Tạo chỉ định xét nghiệm =========

    [HttpPost("encounters/lab-orders")]
    [SwaggerOperation(Summary = "Tạo chỉ định xét nghiệm")]
    public async Task<IActionResult> CreateLabOrder([FromBody] LabOrderReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");

        if ((req.TestIds == null || req.TestIds.Count == 0) && string.IsNullOrWhiteSpace(req.Note))
            return Bad("Vui lòng chọn ít nhất 1 xét nghiệm HOẶC ghi rõ lý do không xét nghiệm.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);
        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);
        if (enc == null) return Bad("Chưa có hồ sơ khám. Vui lòng lưu chẩn đoán trước.");

        // Nếu chỉ có ghi chú (không xét nghiệm)
        if (req.TestIds == null || req.TestIds.Count == 0)
        {
            // Có thể lưu ghi chú vào đâu đó nếu cần, ở đây ta coi như thành công
            return ApiOk(null, "Đã ghi nhận không có xét nghiệm.");
        }

        var validTests = await _db.LabTests
            .Where(t => req.TestIds.Contains(t.LabTestId))
            .ToListAsync();

        if (validTests.Count != req.TestIds.Distinct().Count())
            return Bad("Một số xét nghiệm không tồn tại.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var code = $"LO{DateTime.UtcNow:yyMMddHHmmss}";
                var order = new LabOrder
                {
                    EncounterId = enc.EncounterId,
                    PatientId = enc.PatientId,
                    Code = code,
                    Note = req.Note?.Trim(),
                    Status = LabStatus.Processing,
                    DoctorId = doctorId,
                    ServiceId = enc.ServiceId ?? 0,
                    CreatedAt = DateTime.UtcNow
                };
                _db.LabOrders.Add(order);
                await _db.SaveChangesAsync();

                foreach (var t in validTests)
                {
                    _db.LabOrderItems.Add(new LabOrderItem
                    {
                        LabOrderId = order.LabOrderId,
                        LabTestId = t.LabTestId,
                        TestName = t.Name,
                        ResultValue = "-",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return ApiOk(new { order.LabOrderId, order.Code }, "Đã tạo chỉ định xét nghiệm.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiError(500, "Lỗi tạo xét nghiệm: " + ex.Message);
            }
        });
    }

    // ========= Kê toa =========
    [HttpPost("encounters/prescriptions")]
    [SwaggerOperation(Summary = "Tạo toa thuốc")]
    public async Task<IActionResult> CreatePrescription([FromBody] PrescriptionReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");
        if (req.Lines == null || req.Lines.Count == 0) return Bad("Vui lòng thêm ít nhất 1 thuốc.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);
        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);
        if (enc == null) return Bad("Chưa có hồ sơ khám.");

        // Kiểm tra thuốc tồn tại
        var medIds = req.Lines.Select(x => x.MedicineId).Distinct().ToList();
        var meds = await _db.Medicines
            .Where(m => medIds.Contains(m.MedicineId))
            .Select(m => new { m.MedicineId, m.Name })
            .ToListAsync();

        if (meds.Count != medIds.Count) return Bad("Một số thuốc không tồn tại.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                var rxCode = $"RX{now:yyMMddHHmmss}";

                var rx = new EncounterPrescription
                {
                    EncounterId = enc.EncounterId,
                    Code = rxCode,
                    Advice = req.Advice?.Trim(),
                    CreatedAt = now,
                    ExpiryAt = now.AddMonths(1)
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
                        Note = line.Note?.Trim(),
                        CreatedAt = now
                        // Quantity, UnitPrice sẽ được tính toán khi Dược sĩ duyệt hoặc xuất kho
                    });
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiOk(new { rx.PrescriptionId, rx.Code }, "Đã lưu toa thuốc.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiError(500, "Lỗi lưu toa thuốc: " + ex.Message);
            }
        });
    }

    // ========= Hoàn tất khám =========
    [HttpPost("encounters/complete")]
    [SwaggerOperation(Summary = "Hoàn tất khám và tạo hóa đơn")]
    public async Task<IActionResult> CompleteEncounter([FromBody] StartEncounterReq req)
    {
        if (req.AppointmentId <= 0) return Bad("Thiếu hoặc sai AppointmentId.");

        var (doctorId, err) = await GetDoctorIdAsync();
        if (err != null) return ForbidApi(err);

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == req.AppointmentId);
        if (appt == null) return NotFoundApi("Không tìm thấy lịch hẹn.");
        if (appt.DoctorId != doctorId) return ForbidApi("Bạn không có quyền thao tác lịch hẹn này.");

        // Kiểm tra nếu đã có hóa đơn thì trả về luôn
        var existingInv = await _db.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appt.AppointmentId);
        if (existingInv != null)
        {
            return ApiOk(new { appt.AppointmentId, status = "done", invoiceCode = existingInv.Code }, "Đã hoàn tất (Hóa đơn đã tồn tại).");
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                appt.Status = "done";
                var now = DateTime.UtcNow;

                var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.AppointmentId == appt.AppointmentId);
                // Nếu chưa có encounter (ca khám không kê đơn/xét nghiệm), tạo mới
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

                // Tạo Invoice
                var invoiceCode = $"INV{now:yyMMddHHmmss}";
                var invoice = new Invoice
                {
                    AppointmentId = appt.AppointmentId,
                    EncounterId = enc.EncounterId,
                    PatientId = appt.PatientId,
                    Code = invoiceCode,
                    CreatedAt = now,
                    Status = "unpaid",
                    TotalAmount = 0
                };

                // Lấy tên bệnh nhân
                var patientName = await _db.Patients
                    .Where(p => p.PatientId == appt.PatientId)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync();
                invoice.PatientName = patientName ?? "Unknown";

                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                var itemsToAdd = new List<InvoiceItem>();
                decimal subtotal = 0;

                // Tiền công khám (Service)
                if (appt.ServiceId > 0)
                {
                    var service = await _db.Services.FindAsync(appt.ServiceId);
                    if (service != null && service.BasePrice > 0)
                    {
                        var price = service.BasePrice ?? 0;
                        itemsToAdd.Add(new InvoiceItem
                        {
                            InvoiceId = invoice.InvoiceId,
                            ItemType = "service",
                            RefType = "Service",
                            RefId = appt.ServiceId,
                            Description = $"Khám: {service.Name}",
                            Quantity = 1,
                            UnitPrice = price,
                            CreatedAt = now
                        });
                        subtotal += price;
                    }
                }

                // Tiền xét nghiệm (Lab)
                var labOrders = await _db.LabOrders
                    .Include(lo => lo.Items)
                    .Where(lo => lo.EncounterId == enc.EncounterId)
                    .ToListAsync();

                foreach (var lo in labOrders)
                {
                    foreach (var item in lo.Items)
                    {
                        var test = await _db.LabTests.FindAsync(item.LabTestId);
                        if (test != null && test.BasePrice > 0)
                        {
                            var price = test.BasePrice ?? 0;
                            itemsToAdd.Add(new InvoiceItem
                            {
                                InvoiceId = invoice.InvoiceId,
                                ItemType = "lab",
                                RefType = "LabOrderItem",
                                RefId = item.LabOrderItemId,
                                Description = $"XN: {item.TestName}",
                                Quantity = 1,
                                UnitPrice = price,
                                CreatedAt = now
                            });
                            subtotal += price;
                        }
                    }
                }

                // Tiền thuốc (Prescription)
                var prescriptions = await _db.EncounterPrescriptions
                    .Include(p => p.Items)
                    .Where(p => p.EncounterId == enc.EncounterId)
                    .ToListAsync();

                foreach (var p in prescriptions)
                {
                    foreach (var item in p.Items)
                    {
                        if (item.MedicineId.HasValue)
                        {
                            var med = await _db.Medicines.FindAsync(item.MedicineId.Value);
                            if (med != null && med.BasePrice > 0)
                            {
                                var qty = item.Quantity ?? 1;
                                var price = med.BasePrice;
                                itemsToAdd.Add(new InvoiceItem
                                {
                                    InvoiceId = invoice.InvoiceId,
                                    ItemType = "medicine",
                                    RefType = "PrescriptionItem",
                                    RefId = item.ItemId,
                                    Description = $"Thuốc: {med.Name}",
                                    Quantity = qty,
                                    UnitPrice = price,
                                    CreatedAt = now
                                });
                                subtotal += (qty * price);
                            }
                        }
                    }
                }

                if (itemsToAdd.Any())
                {
                    _db.InvoiceItems.AddRange(itemsToAdd);
                }

                invoice.Subtotal = subtotal;
                invoice.TotalAmount = subtotal; // + Tax - Discount nếu có
                _db.Invoices.Update(invoice);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return ApiOk(new { appt.AppointmentId, status = "done", invoiceId = invoice.InvoiceId, invoiceCode = invoice.Code },
                             "Đã hoàn tất khám và tạo hóa đơn.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiError(500, "Lỗi hoàn tất khám: " + ex.Message);
            }
        });
    }
}