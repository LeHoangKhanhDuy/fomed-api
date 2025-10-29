public sealed record PatientTotalResponse(
    bool Success,
    string From,
    string To,
    int TotalAll,
    int NewInRange,
    int NewToday,
    int NewThisWeek,
    int NewThisMonth
);
