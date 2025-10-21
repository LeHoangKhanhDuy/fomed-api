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
        // Validate tồn tại
        if (!await _db.Patients.AnyAsync(p => p.PatientId == req.PatientId, ct))
            return BadRequest(new { success = false, message = "Bệnh nhân không tồn tại." });

        if (!await _db.Doctors.AnyAsync(d => d.DoctorId == req.DoctorId, ct))
            return BadRequest(new { success = false, message = "Bác sĩ không tồn tại." });

        if (req.ServiceId is int sid && !await _db.Services.AnyAsync(s => s.ServiceId == sid, ct))
            return BadRequest(new { success = false, message = "Dịch vụ không tồn tại." });

        // PATIENT chỉ được đặt cho chính mình
        if (User.IsInRole("PATIENT"))
        {
            var patientIdStr = User.FindFirst("patient_id")?.Value;
            if (long.TryParse(patientIdStr, out var myPid) && myPid != req.PatientId)
                return Forbid();
        }

        var lastCode = await _db.Appointments.OrderByDescending(a => a.AppointmentId).Select(a => a.Code).FirstOrDefaultAsync(ct);

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
                    VisitTime = req.VisitTime,   // .NET 9: phía FE nên gửi "HH:mm:ss" (vd "09:35:00") trừ khi có converter relax
                    Reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason!.Trim(),
                    Status = "waiting",  // waiting | booked | done | cancelled | no_show
                    QueueNo = nextQueue,
                    Code = code,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                _db.Appointments.Add(entity);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var resp = new AppointmentResponse
                {
                    AppointmentId = entity.AppointmentId,
                    Code = entity.Code,
                    PatientId = entity.PatientId,
                    DoctorId = entity.DoctorId,
                    ServiceId = entity.ServiceId,
                    VisitDate = entity.VisitDate,
                    VisitTime = entity.VisitTime,
                    Reason = entity.Reason,
                    Status = entity.Status,
                    QueueNo = entity.QueueNo,
                    CreatedAt = entity.CreatedAt
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


    // GET: xem STT kế tiếp (preview)
    // [HttpGet("next-queue")]
    // [Authorize(Roles = "PATIENT,EMPLOYEE,ADMIN")]
    // [SwaggerOperation(Summary = "Tạo lịch khám bệnh", Description = "Bệnh nhân có thể đặt lịch khám. Nhân viên đặt lịch cho bệnh nhân ở CMS", Tags = new[] { "Appointments" })]
    // [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    // [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    // [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    // public async Task<IActionResult> GetNextQueue([FromQuery] int doctorId, [FromQuery] DateOnly date, CancellationToken ct)
    // {
    //     if (!await _db.Doctors.AnyAsync(d => d.DoctorId == doctorId, ct))
    //         return BadRequest(new { success = false, message = "Bác sĩ không tồn tại." });

    //     var maxQueue = await _db.Appointments
    //         .Where(a => a.DoctorId == doctorId && a.VisitDate == date && a.QueueNo != null)
    //         .MaxAsync(a => (int?)a.QueueNo!, ct) ?? 0;

    //     return Ok(new { success = true, data = new { nextQueue = maxQueue + 1 } });
    // }

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
    [FromQuery] string? q,          // tìm kiếm chung
    [FromQuery] int page = 1,
    [FromQuery] int limit = 20,
    CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        // Mặc định: hôm nay
        var today = DateOnly.FromDateTime(DateTime.Now.Date);
        var theDate = date ?? today;

        // Nếu là bác sĩ và không truyền doctorId -> lấy từ claim
        if (!doctorId.HasValue && User.IsInRole("DOCTOR"))
        {
            var docIdStr = User.FindFirst("doctor_id")?.Value;
            if (int.TryParse(docIdStr, out var did)) doctorId = did;
        }

        // Base query + include đủ dữ liệu hiển thị
        var query = _db.Appointments
            .AsNoTracking()
            .Where(a => a.VisitDate == theDate);

        if (doctorId.HasValue)
            query = query.Where(a => a.DoctorId == doctorId.Value);

        // Tìm kiếm (tên BN, SĐT, mã hồ sơ, tên bác sĩ, tên dịch vụ)
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

        // Sắp xếp theo giờ khám -> STT
        var items = await query
            .OrderBy(a => a.VisitTime)
            .ThenBy(a => a.QueueNo)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(a => new
            {
                // chung
                a.AppointmentId,
                a.Code,                // BN0001...
                a.Status,              // waiting / booked / done / cancelled / no_show
                a.QueueNo,             // STT trong ngày theo bác sĩ
                a.CreatedAt,           // thời điểm đặt lịch

                // lịch hẹn
                a.VisitDate,
                a.VisitTime,

                // bệnh nhân
                a.PatientId,
                PatientName = a.Patient.FullName,
                PatientPhone = a.Patient.Phone,

                // bác sĩ & dịch vụ
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
}
