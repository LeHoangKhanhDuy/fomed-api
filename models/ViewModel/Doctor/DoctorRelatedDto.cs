public sealed record RelatedDoctorDto(
    int DoctorId,
    string? FullName,
    string? Title,
    string? AvatarUrl,
    int? PrimarySpecialtyId,
    string? PrimarySpecialtyName,
    short? ExperienceYears,
    decimal RatingAvg,
    int RatingCount,
    string? RoomName
);

public sealed record RelatedDoctorsResponse(
    bool Success,
    List<RelatedDoctorDto> Data
);
