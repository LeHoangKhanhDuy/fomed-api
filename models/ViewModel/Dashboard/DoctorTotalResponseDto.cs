public sealed record DoctorTotalResponse(
    bool Success,
    int TotalAll,
    int TotalActive,
    int TotalInactive
);