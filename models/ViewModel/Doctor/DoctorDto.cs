public sealed class DoctorListItemDto
{
    public int DoctorId { get; init; }
    public string FullName { get; set; } = null!;
    public string? Title { get; init; }
    public string? PrimarySpecialtyName { get; init; }
    public string? RoomName { get; init; }
    public short? ExperienceYears { get; init; }
    public decimal RatingAvg { get; init; }
    public int RatingCount { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class DoctorEducationDto
{
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public sealed class DoctorAchievementDto
{
    public string? YearLabel { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed class DoctorExpertiseDto
{
    public string Content { get; init; } = string.Empty;
}

public sealed class DoctorWeeklySlotDto
{
    public byte Weekday { get; init; }    // 1=Mon..7=Sun
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public string? Note { get; init; }
}

public sealed class DoctorDetailDto
{
    public int DoctorId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? LicenseNo { get; init; }
    public string? PrimarySpecialtyName { get; init; }
    public string? RoomName { get; init; }
    public short? ExperienceYears { get; init; }
    public string? ExperienceNote { get; init; }
    public string? Intro { get; init; }
    public int VisitCount { get; init; }
    public decimal RatingAvg { get; init; }
    public int RatingCount { get; init; }
    public string? AvatarUrl { get; init; }

    public List<DoctorEducationDto> Educations { get; init; } = new();
    public List<DoctorExpertiseDto> Expertises { get; init; } = new();
    public List<DoctorAchievementDto> Achievements { get; init; } = new();
    public List<DoctorWeeklySlotDto> WeeklySlots { get; init; } = new();
}

public sealed class DoctorRatingItemDto
{
    public long RatingId { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}
