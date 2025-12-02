using FoMed.Api.Features.Doctor.TodayPatients;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[ApiController]
[Route("api/v1/dashboard/")]
[Authorize(Roles = "ADMIN,EMPLOYEE,DOCTOR")]
public class DashboardController : ControllerBase
{
    private readonly FoMedContext _db;

    public DashboardController(FoMedContext db) => _db = db;

    public sealed record DoctorVisitCountDto(int DoctorId, string DoctorName, int VisitCount);

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

        // ===== Base query: lượt khám đã hoàn tất theo ngày (chỉ tính những lịch có FinalCost) =====
        var q = _db.Appointments
            .AsNoTracking()
            .Where(a => a.Status == "done" && a.FinalCost.HasValue);

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

    [HttpGet("patients")]
    [Produces("application/json")]
    public async Task<IActionResult> GetPatientTotals(
    [FromQuery] DateOnly? from,
    [FromQuery] DateOnly? to,
    CancellationToken ct = default)
    {
        // ===== Khung ngày mặc định: 30 ngày gần nhất (tính cả hôm nay) =====
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now.Date);
        var defaultFrom = today.AddDays(-29);
        var fromDate = from ?? defaultFrom;
        var toDate = to ?? today;

        if (toDate < fromDate)
            return BadRequest(new { success = false, message = "`to` phải >= `from`" });

        // Ranh giới DateTime cho khoảng ngày (bao gồm ngày 'to')
        var fromDt = fromDate.ToDateTime(TimeOnly.MinValue);           // 00:00:00
        var toDtExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue); // < ngày+1

        var q = _db.Patients.AsNoTracking();

        // Tổng tất cả bệnh nhân (all-time)
        var totalAll = await q.CountAsync(ct);

        // Mới tạo trong khoảng ngày
        var qInRange = q.Where(p => p.CreatedAt >= fromDt && p.CreatedAt < toDtExclusive);
        var newInRange = await qInRange.CountAsync(ct);

        // Mốc Today / ThisWeek (Mon-start) / ThisMonth
        int dow = (int)now.DayOfWeek;                  // 0=Sun..6=Sat
        int offsetToMonday = ((dow + 6) % 7);
        var startOfToday = now.Date;
        var startOfWeek = startOfToday.AddDays(-offsetToMonday);
        var startOfMonth = new DateTime(startOfToday.Year, startOfToday.Month, 1);
        var tomorrow = startOfToday.AddDays(1);

        var newToday = await q.Where(p => p.CreatedAt >= startOfToday && p.CreatedAt < tomorrow).CountAsync(ct);
        var newThisWeek = await q.Where(p => p.CreatedAt >= startOfWeek && p.CreatedAt < tomorrow).CountAsync(ct);
        var newThisMonth = await q.Where(p => p.CreatedAt >= startOfMonth && p.CreatedAt < tomorrow).CountAsync(ct);

        var res = new PatientTotalResponse(
            Success: true,
            From: fromDate.ToString("yyyy-MM-dd"),
            To: toDate.ToString("yyyy-MM-dd"),
            TotalAll: totalAll,
            NewInRange: newInRange,
            NewToday: newToday,
            NewThisWeek: newThisWeek,
            NewThisMonth: newThisMonth
        );

        return Ok(res);
    }

    // Thống kê doanh thu theo tháng (từ Appointments hoàn thành)
    [HttpGet("monthly-sales")]
    [Produces("application/json")]
    public async Task<ActionResult> GetMonthlySales(
        [FromQuery] int? year,
        [FromQuery] int? doctorId,
        [FromQuery] int? serviceId,
        CancellationToken ct = default)
    {
        var targetYear = year ?? DateTime.Now.Year;

        // Base query: lấy appointments đã hoàn thành có FinalCost
        var q = _db.Appointments
            .AsNoTracking()
            .Where(a => a.Status == "done" && a.FinalCost.HasValue);

        if (doctorId is > 0) q = q.Where(a => a.DoctorId == doctorId);
        if (serviceId is > 0) q = q.Where(a => a.ServiceId == serviceId);

        // Lọc theo năm (dựa vào VisitDate)
        var startOfYear = new DateOnly(targetYear, 1, 1);
        var endOfYear = new DateOnly(targetYear, 12, 31);
        var qInYear = q.Where(a => a.VisitDate >= startOfYear && a.VisitDate <= endOfYear);

        // Tổng doanh thu cả năm
        var totalYearRevenue = await qInYear.SumAsync(a => a.FinalCost ?? 0, ct);

        // Group by tháng
        var monthlyRaw = await qInYear
            .GroupBy(a => a.VisitDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Sum(a => a.FinalCost ?? 0),
                Count = g.Count()
            })
            .OrderBy(x => x.Month)
            .ToListAsync(ct);

        // Tạo đầy đủ 12 tháng (tháng nào không có data thì = 0)
        var monthlyData = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var data = monthlyRaw.FirstOrDefault(m => m.Month == month);
                return new MonthlySalePoint(
                    Month: month,
                    MonthName: new DateTime(targetYear, month, 1).ToString("MMM", new CultureInfo("en-US")),
                    Revenue: data?.Revenue ?? 0,
                    VisitCount: data?.Count ?? 0
                );
            })
            .ToList();

        // Tính toán thống kê bổ sung
        var currentMonth = DateTime.Now.Month;
        var currentMonthData = monthlyData.FirstOrDefault(m => m.Month == currentMonth);
        var previousMonthData = currentMonth > 1
            ? monthlyData.FirstOrDefault(m => m.Month == currentMonth - 1)
            : null;

        decimal monthOverMonthChange = 0;
        if (previousMonthData != null && previousMonthData.Revenue > 0)
        {
            monthOverMonthChange = ((currentMonthData?.Revenue ?? 0) - previousMonthData.Revenue)
                / previousMonthData.Revenue * 100;
        }

        var avgMonthlyRevenue = monthlyData.Count > 0
            ? monthlyData.Average(m => m.Revenue)
            : 0;

        var res = new MonthlySalesResponse(
            Success: true,
            Year: targetYear,
            TotalRevenue: totalYearRevenue,
            CurrentMonthRevenue: currentMonthData?.Revenue ?? 0,
            MonthOverMonthChange: Math.Round(monthOverMonthChange, 2),
            AvgMonthlyRevenue: Math.Round(avgMonthlyRevenue, 2),
            Monthly: monthlyData
        );

        return Ok(res);
    }

    // Mục tiêu doanh thu theo tháng (100 tr)
    [HttpGet("monthly-target")]
    [Produces("application/json")]
    public async Task<ActionResult> GetMonthlyTarget(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? doctorId,
        [FromQuery] int? serviceId,
        [FromQuery] decimal? target,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month is >= 1 and <= 12 ? month!.Value : (y == now.Year ? now.Month : 12);

        var start = new DateOnly(y, m, 1);
        var endExclusive = (m == 12)
            ? new DateOnly(y + 1, 1, 1)
            : new DateOnly(y, m + 1, 1);

        // Base: chỉ lấy lịch hẹn done + có FinalCost trong tháng
        var q = _db.Appointments
            .AsNoTracking()
            .Where(a => a.Status == "done" && a.FinalCost.HasValue
                        && a.VisitDate >= start && a.VisitDate < endExclusive);

        if (doctorId is > 0) q = q.Where(a => a.DoctorId == doctorId);
        if (serviceId is > 0) q = q.Where(a => a.ServiceId == serviceId);

        var actual = await q.SumAsync(a => a.FinalCost ?? 0, ct);

        var targetRevenue = target ?? 100_000_000m; // mặc định 100 triệu
        var progress = targetRevenue > 0 ? Math.Min(100m, Math.Round(actual / targetRevenue * 100m, 2)) : 0m;

        var res = new MonthlyTargetResponse(
            Success: true,
            Year: y,
            Month: m,
            TargetRevenue: targetRevenue,
            ActualRevenue: actual,
            ProgressPercent: progress
        );

        return Ok(res);
    }
}
