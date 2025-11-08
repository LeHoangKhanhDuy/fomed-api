public record MonthlySalePoint(
    int Month,
    string MonthName,
    decimal Revenue,
    int VisitCount
);

public record MonthlySalesResponse(
    bool Success,
    int Year,
    decimal TotalRevenue,
    decimal CurrentMonthRevenue,
    decimal MonthOverMonthChange,
    decimal AvgMonthlyRevenue,
    List<MonthlySalePoint> Monthly
);