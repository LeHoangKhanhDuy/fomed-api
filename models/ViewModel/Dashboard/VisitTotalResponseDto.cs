public sealed record VisitTotalResponse(
    bool Success,
    string From,
    string To,
    string Timezone,
    int TotalAllTime,
    int TotalInRange,
    int TotalToday,
    int TotalThisWeek,
    int TotalThisMonth,
    IReadOnlyList<VisitDailyPoint> Daily
);

public sealed record VisitDailyPoint(string Date, int Count);