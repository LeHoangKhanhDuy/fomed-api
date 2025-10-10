using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FoMed.Api.Features.Doctor.TodayPatients;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/doctor")]
[Authorize(Roles = "DOCTOR")]
public sealed class DoctorPatientsController : ControllerBase
{
    private readonly FoMedContext _db;
    public DoctorPatientsController(FoMedContext db) => _db = db;

    private async Task<int?> GetDoctorIdAsync()
    {
        // 1) Ưu tiên claim doctor_id
        var c = User.FindFirst("doctor_id")?.Value;
        if (!string.IsNullOrWhiteSpace(c) && int.TryParse(c, out var fromClaim))
            return fromClaim;

        // 2) Fallback theo user id (NameIdentifier hoặc sub)
        var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(uidStr)) return null;

        // Nếu Users.UserId là BIGINT -> long; nếu INT -> int
        // == CHỌN MỘT trong hai khối WHERE dưới sao cho khớp kiểu cột Doctors.UserId ==
        if (long.TryParse(uidStr, out var uidLong))
        {
            // ----- Nếu Doctors.UserId là BIGINT/long -----
            var docIdLong = await _db.Doctors
                .Where(d => d.UserId == uidLong)
                .Select(d => (int?)d.DoctorId)
                .FirstOrDefaultAsync();

            if (docIdLong.HasValue) return docIdLong.Value;

            // ----- Nếu Doctors.UserId là INT (CAST xuống int an toàn) -----
            if (uidLong >= int.MinValue && uidLong <= int.MaxValue)
            {
                var uidInt = (int)uidLong;
                var docIdInt = await _db.Doctors
                    .Where(d => d.UserId == uidInt)
                    .Select(d => (int?)d.DoctorId)
                    .FirstOrDefaultAsync();
                if (docIdInt.HasValue) return docIdInt.Value;
            }
        }

        return null;
    }


    private static DateOnly TodayInVn()
    {
        // Chạy được cả Windows lẫn Linux container
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(nowVn.Date);
    }

    [HttpGet("today-patients")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Danh sách bệnh nhân hôm nay của bác sĩ",
        Description = "Mặc định hiển thị tất cả. Có thể lọc theo trạng thái: waiting, booked, done, cancelled, no_show; và tìm theo mã hẹn/tên/SĐT."
    )]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PatientTodayPagedResult<TodayPatientItemDto>>> GetTodayPatients(
    [FromQuery] TodayPatientsQuery q, CancellationToken ct)
    {
        var doctorId = await GetDoctorIdAsync();
        if (doctorId is null)
            return BadRequest(new { success = false, message = "Không xác định được bác sĩ từ token. Hãy đăng nhập bằng tài khoản DOCTOR." });

        var page = Math.Max(1, q.Page);
        var limit = Math.Clamp(q.Limit, 1, 200);
        var todayVn = TodayInVn();

        var baseQuery = _db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId && a.VisitDate == todayVn);

        if (q.Status.HasValue)
        {
            var st = q.Status.Value.ToString();
            baseQuery = baseQuery.Where(a => a.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = $"%{q.Keyword.Trim()}%";
            baseQuery = baseQuery.Where(a =>
                (a.Code != null && EF.Functions.Like(a.Code, kw)) ||
                (a.Patient.FullName != null && EF.Functions.Like(a.Patient.FullName, kw)) ||
                (a.Patient.Phone != null && EF.Functions.Like(a.Patient.Phone, kw)));
        }

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(a => a.QueueNo)
            .ThenBy(a => a.VisitTime)
            .ThenBy(a => a.AppointmentId)
            .Select(a => new TodayPatientItemDto
            {
                AppointmentId = a.AppointmentId,
                Code = a.Code,
                PatientId = a.PatientId,
                PatientName = a.Patient.FullName,
                Phone = a.Patient.Phone,
                VisitDate = a.VisitDate,
                VisitTime = a.VisitTime,
                TimeText = $"{a.VisitTime.Hour:D2}:{a.VisitTime.Minute:D2}",
                Status = a.Status,
                QueueNo = a.QueueNo,
                ServiceId = a.ServiceId,
                ServiceName = a.Service != null ? a.Service.Name : null
            })
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(new PatientTodayPagedResult<TodayPatientItemDto>
        {
            Page = page,
            PageSize = limit,
            Total = total,
            Items = items
        });
    }
}
