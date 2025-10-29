using FoMed.Api.Features.Doctor.TodayPatients;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[ApiController]
[Route("api/v1/dashboard/")]
[Authorize(Roles = "ADMIN,EMPLOYEE")]
public class DashboardController : ControllerBase
{
    private readonly FoMedContext _db;

    public DashboardController(FoMedContext db) => _db = db;

    //Thống kê tổng số lượt khám (Appointments.Status = done, có VisitAt)
    [HttpGet("visits")]
    [Produces("application/json")]
    public async Task<ActionResult> GetVisitTotals(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? doctorId,
        [FromQuery] int? serviceId,
        CancellationToken ct = default)
    {
        // ===== Khung ngày mặc định: 30 ngày gần nhất (tính cả hôm nay) =====
        var today = DateOnly.FromDateTime(DateTime.Now.Date);         // local server time
        var defaultFrom = today.AddDays(-29);
        var fromDate = from ?? defaultFrom;
        var toDate = to ?? today;

        if (toDate < fromDate)
            return BadRequest(new { success = false, message = "`to` phải >= `from`" });

        // ===== Base query: lượt khám đã hoàn tất theo ngày =====
        var q = _db.Appointments
            .AsNoTracking()
            .Where(a => a.Status == "done");

        if (doctorId is > 0) q = q.Where(a => a.DoctorId == doctorId);
        if (serviceId is > 0) q = q.Where(a => a.ServiceId == serviceId);

        // Tổng all-time (sau khi filter doctor/service)
        var totalAllTime = await q.CountAsync(ct);

        // Trong khoảng ngày (dựa vào VisitDate)
        var qInRange = q.Where(a => a.VisitDate >= fromDate && a.VisitDate <= toDate);
        var totalInRange = await qInRange.CountAsync(ct);

        // ===== Mốc today / thisWeek / thisMonth theo VisitDate =====
        // Monday-start week
        int dow = (int)DateTime.Now.DayOfWeek;                 // 0=Sunday..6=Saturday
        int offsetToMonday = ((dow + 6) % 7);                  // ép kiểu int để dùng %
        var startOfWeek = today.AddDays(-offsetToMonday);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);

        var totalToday = await q.Where(a => a.VisitDate == today).CountAsync(ct);
        var totalThisWeek = await q.Where(a => a.VisitDate >= startOfWeek && a.VisitDate <= today).CountAsync(ct);
        var totalThisMonth = await q.Where(a => a.VisitDate >= startOfMonth && a.VisitDate <= today).CountAsync(ct);

        // ===== Nhóm theo ngày (VisitDate) để vẽ biểu đồ =====
        var dailyRaw = await qInRange
            .GroupBy(a => a.VisitDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var daily = dailyRaw.Select(d => new VisitDailyPoint(d.Date.ToString("yyyy-MM-dd"), d.Count)).ToList();

        var res = new VisitTotalResponse(
            Success: true,
            From: fromDate.ToString("yyyy-MM-dd"),
            To: toDate.ToString("yyyy-MM-dd"),
            Timezone: TimeZoneInfo.Local.Id,   // giữ lại field cho FE, hiện đang dùng theo server local
            TotalAllTime: totalAllTime,
            TotalInRange: totalInRange,
            TotalToday: totalToday,
            TotalThisWeek: totalThisWeek,
            TotalThisMonth: totalThisMonth,
            Daily: daily
        );

        return Ok(res);
    }

    [HttpGet("doctors")]
    [Produces("application/json")]
    public async Task<IActionResult> GetDoctorTotals(
    [FromQuery] int? specialtyId,
    [FromQuery] bool? isActive,
    CancellationToken ct = default)
    {
        var q = _db.Doctors.AsNoTracking();

        // 🔹 Lọc theo chuyên khoa chính
        if (specialtyId is > 0)
            q = q.Where(d => d.PrimarySpecialtyId == specialtyId);

        // 🔹 Nếu truyền isActive, chỉ đếm theo trạng thái đó
        if (isActive is not null)
        {
            var total = await q.Where(d => d.IsActive == isActive).CountAsync(ct);
            var res1 = new DoctorTotalResponse(
                Success: true,
                TotalAll: total,
                TotalActive: isActive == true ? total : 0,
                TotalInactive: isActive == false ? total : 0
            );
            return Ok(res1);
        }

        // 🔹 Mặc định: trả cả 3 số liệu
        var totalAll = await q.CountAsync(ct);
        var totalActive = await q.Where(d => d.IsActive).CountAsync(ct);
        var totalInactive = totalAll - totalActive;

        var res = new DoctorTotalResponse(
            Success: true,
            TotalAll: totalAll,
            TotalActive: totalActive,
            TotalInactive: totalInactive
        );

        return Ok(res);
    }
}
