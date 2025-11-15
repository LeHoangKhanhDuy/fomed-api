using FoMed.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FoMed.Api.Features.Appointments;

// Kế thừa từ BackgroundService, một service có sẵn của .NET
public class AppointmentCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentCleanupService> _logger;

    // Sử dụng IServiceProvider để có thể tạo DbContext trong một scope riêng
    // (vì BackgroundService là Singleton, còn DbContext là Scoped)
    public AppointmentCleanupService(IServiceProvider serviceProvider, ILogger<AppointmentCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Appointment Cleanup Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Xử lý công việc
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in Appointment Cleanup Service.");
            }

            // Chờ 15 phút rồi chạy lại
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }

        _logger.LogInformation("Appointment Cleanup Service is stopping.");
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Appointment Cleanup Service is running.");

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoMedContext>();

        // Lấy múi giờ của Việt Nam (Indochina Time)
        TimeZoneInfo vietnamZone;
        try
        {
            vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            vietnamZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }

        // Lấy thời gian hiện tại chính xác ở Việt Nam
        DateTime vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamZone);

        var currentDate = DateOnly.FromDateTime(vietnamNow);
        var currentTime = TimeOnly.FromDateTime(vietnamNow);

        _logger.LogInformation("Current Vietnam time: {CurrentTime}, checking for appointments...", vietnamNow);

        // === KẾT THÚC THAY ĐỔI ===

        // Tìm tất cả lịch hẹn có status "waiting" đã qua ngày hẹn, cùng ngày hẹn nhưng đã qua giờ hẹn
        var appointmentsToCancel = await db.Appointments
            .Where(a => a.Status == "waiting" &&
                   (a.VisitDate < currentDate ||
                   (a.VisitDate == currentDate && a.VisitTime < currentTime)))
            .ToListAsync(stoppingToken);

        if (appointmentsToCancel.Any())
        {
            // Log này rất quan trọng, hãy kiểm tra nó
            _logger.LogInformation("Found {Count} appointments to cancel.", appointmentsToCancel.Count);
            var utcNow = DateTime.UtcNow;

            foreach (var appt in appointmentsToCancel)
            {
                // Cập nhật trạng thái
                appt.Status = "cancelled"; // Hoặc "missed"
                appt.UpdatedAt = utcNow;
            }

            // Lưu thay đổi vào DB
            await db.SaveChangesAsync(stoppingToken);
        }
        else
        {
            // Bạn sẽ thấy log này nếu logic múi giờ bị sai
            _logger.LogInformation("No appointments to clean up.");
        }
    }
}