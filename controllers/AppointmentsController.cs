using System.Data;
using System.Security.Claims;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace FoMed.Api.Features.Appointments;

[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly FoMedContext _db;
    public AppointmentsController(FoMedContext db) => _db = db;

    // POST: tạo lịch (PATIENT / EMPLOYEE / ADMIN)
    [HttpPost("create")]
    [Authorize(Roles = "PATIENT,EMPLOYEE,ADMIN")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Tạo lịch khám bệnh", Description = "Bệnh nhân có thể đặt lịch khám. Nhân viên đặt lịch cho bệnh nhân ở CMS", Tags = new[] { "Appointments" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        // Load thông tin bệnh nhân
        var patient = await _db.Patients
            .Where(p => p.PatientId == req.PatientId)
            .Select(p => new { p.PatientId, p.FullName, p.Phone })
            .FirstOrDefaultAsync(ct);
        if (patient == null)
            return BadRequest(new { success = false, message = "Bệnh nhân không tồn tại." });

        // Load thông tin bác sĩ (JOIN với User để lấy tên)
        var doctorInfo = await _db.Doctors
            .Where(d => d.DoctorId == req.DoctorId)
            .Select(d => new
            {
                DoctorId = d.DoctorId,
                DoctorName = d.User != null ? d.User.FullName : "BS #" + d.DoctorId.ToString()
            })
            .FirstOrDefaultAsync(ct);
        if (doctorInfo == null)
            return BadRequest(new { success = false, message = "Bác sĩ không tồn tại." });

        // Load thông tin dịch vụ (nếu có)
        string? serviceName = null;
        if (req.ServiceId.HasValue)
        {
            serviceName = await _db.Services
                .Where(s => s.ServiceId == req.ServiceId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(ct);
            if (serviceName == null)
                return BadRequest(new { success = false, message = "Dịch vụ không tồn tại." });
        }

        // PATIENT chỉ được đặt cho chính mình
        if (User.IsInRole("PATIENT"))
        {
            var patientIdStr = User.FindFirst("patient_id")?.Value;
            if (long.TryParse(patientIdStr, out var myPid) && myPid != req.PatientId)
                return Forbid();
        }

        var lastCode = await _db.Appointments
            .OrderByDescending(a => a.AppointmentId)
            .Select(a => a.Code)
            .FirstOrDefaultAsync(ct);

        // Tính số thứ tự kế tiếp
        int nextNumber = 1;
        if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.StartsWith("BN"))
        {
            var numericPart = lastCode.Substring(2);
            if (int.TryParse(numericPart, out var n))
                nextNumber = n + 1;
        }

        // Không cho đặt quá khứ 
        var nowLocal = DateTime.Now;
        if (req.VisitDate.ToDateTime(req.VisitTime) < nowLocal)
            return BadRequest(new { success = false, message = "Không thể đặt lịch trong quá khứ." });

        // ===== Quan trọng: dùng ExecutionStrategy để bao toàn bộ transaction vào 1 đơn vị có thể retry =====
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            IActionResult result;

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Chặn trùng giờ cùng bác sĩ (trong transaction)
                var exists = await _db.Appointments
                    .AnyAsync(a => a.DoctorId == req.DoctorId
                                   && a.VisitDate == req.VisitDate
                                   && a.VisitTime == req.VisitTime, ct);
                if (exists)
                {
                    await tx.RollbackAsync(ct);
                    return Conflict(new { success = false, message = "Khung giờ này đã có lịch với bác sĩ." });
                }

                // Lấy queue lớn nhất theo Bác sĩ + Ngày (trong transaction)
                var maxQueue = await _db.Appointments
                    .Where(a => a.DoctorId == req.DoctorId && a.VisitDate == req.VisitDate && a.QueueNo != null)
                    .MaxAsync(a => (int?)a.QueueNo!, ct) ?? 0;

                var nextQueue = maxQueue + 1;
                var code = $"BN{nextNumber:D4}";
                var nowUtc = DateTime.UtcNow;

                var entity = new Appointment
                {
                    PatientId = req.PatientId,
                    DoctorId = req.DoctorId,
                    ServiceId = req.ServiceId,
                    VisitDate = req.VisitDate,
                    VisitTime = req.VisitTime,
                    Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason!.Trim(),
                    Status = "waiting",
                    QueueNo = nextQueue,
                    Code = code,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                _db.Appointments.Add(entity);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Trả về đầy đủ thông tin
                var resp = new
                {
                    AppointmentId = entity.AppointmentId,
                    Code = entity.Code,
                    Status = entity.Status,
                    QueueNo = entity.QueueNo,
                    CreatedAt = entity.CreatedAt,

                    // Lịch hẹn
                    VisitDate = entity.VisitDate,
                    VisitTime = entity.VisitTime,
                    Reason = entity.Reason,

                    // Bệnh nhân
                    PatientId = entity.PatientId,
                    PatientName = patient.FullName,
                    PatientPhone = patient.Phone,

                    // Bác sĩ
                    DoctorId = entity.DoctorId,
                    DoctorName = doctorInfo.DoctorName,

                    // Dịch vụ
                    ServiceId = entity.ServiceId,
                    ServiceName = serviceName
                };

                result = Ok(new { success = true, message = "Tạo lịch thành công", data = resp });
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                result = Conflict(new { success = false, message = "Trùng lịch hoặc STT. Vui lòng thử lại." });
            }

            return result;
        });
    }

    // GET: danh sách lịch theo ngày/bác sĩ (phục vụ UI)
    [HttpGet]
    [Authorize(Roles = "DOCTOR,EMPLOYEE,ADMIN")]
    [SwaggerOperation(
        Summary = "Danh sách lịch khám",
        Description = "Dùng chung cho UI danh sách chờ khám và danh sách bệnh nhân hôm nay",
        Tags = new[] { "Appointments" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? date,
        [FromQuery] int? doctorId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var today = DateOnly.FromDateTime(DateTime.Now.Date);
        var theDate = date ?? today;

        if (!doctorId.HasValue && User.IsInRole("DOCTOR"))
        {
            var docIdStr = User.FindFirst("doctor_id")?.Value;
            if (int.TryParse(docIdStr, out var did)) doctorId = did;
        }

        var query = _db.Appointments
            .AsNoTracking()
            .Where(a => a.VisitDate == theDate);

        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim().ToLower();
            query = query.Where(a =>
                a.Code != null && a.Code.ToLower().Contains(kw)
                || a.Patient.FullName.ToLower().Contains(kw)
                || (a.Patient.Phone != null && a.Patient.Phone.Contains(kw))
                || (a.Service != null && a.Service.Name.ToLower().Contains(kw))
                || (a.Doctor.User != null && a.Doctor.User.FullName.ToLower().Contains(kw))
            );
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(a => a.VisitTime)
            .ThenBy(a => a.QueueNo)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(a => new
            {
                a.AppointmentId,
                a.Code,
                a.Status,
                a.QueueNo,
                a.CreatedAt,
                a.VisitDate,
                a.VisitTime,
                a.PatientId,
                PatientName = a.Patient.FullName,
                PatientPhone = a.Patient.Phone,
                a.DoctorId,
                DoctorName = a.Doctor.User != null ? a.Doctor.User.FullName : $"BS #{a.DoctorId}",
                a.ServiceId,
                ServiceName = a.Service != null ? a.Service.Name : null
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
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

    [HttpGet("patient-schedule")]
    [Authorize(Roles = "PATIENT")]
    [SwaggerOperation(
    Summary = "Lấy danh sách lịch của bệnh nhân đang đăng nhập",
    Description = "PATIENT xem các lịch hẹn của chính mình",
    Tags = new[] { "Appointments" })]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAppointments(
    [FromQuery] DateOnly? dateFrom = null,
    [FromQuery] DateOnly? dateTo = null,
    [FromQuery] string? status = null,
    [FromQuery] int page = 1,
    [FromQuery] int limit = 10,
    CancellationToken ct = default)
    {
        // Lấy userId từ token
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdStr, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        // Lấy patientId liên kết với user
        var patientId = await _db.Patients
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => (long?)p.PatientId)
            .FirstOrDefaultAsync(ct);

        if (!patientId.HasValue)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ bệnh nhân cho user này." });

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var q = _db.Appointments
            .AsNoTracking()
            .Where(a => a.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status.Trim());

        if (dateFrom.HasValue)
            q = q.Where(a => a.VisitDate >= dateFrom.Value);

        if (dateTo.HasValue)
            q = q.Where(a => a.VisitDate <= dateTo.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.VisitDate)
            .ThenBy(a => a.VisitTime)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(a => new
            {
                a.AppointmentId,
                a.Code,
                a.Status,
                a.QueueNo,
                a.CreatedAt,
                a.VisitDate,
                a.VisitTime,
                a.Reason,
                a.DoctorId,
                DoctorName = a.Doctor != null && a.Doctor.User != null ? a.Doctor.User.FullName : $"BS #{a.DoctorId}",
                a.ServiceId,
                ServiceName = a.Service != null ? a.Service.Name : null
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            message = "Danh sách lịch khám của bạn.",
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
}