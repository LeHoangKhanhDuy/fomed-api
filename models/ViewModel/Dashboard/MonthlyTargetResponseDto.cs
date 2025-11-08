public record MonthlyTargetResponse(
    bool Success,
    int Year,
    int Month,
    decimal TargetRevenue,     // mục tiêu tháng
    decimal ActualRevenue,     // doanh thu thực tế tháng
    decimal ProgressPercent    // % đạt mục tiêu 
);
