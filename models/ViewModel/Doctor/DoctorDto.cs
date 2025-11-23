using FoMed.Api.Models;

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
    public string? Intro { get; set; }
    public List<DoctorEducationDto> Educations { get; init; } = new();
    public List<DoctorExpertiseDto> Expertises { get; init; } = new();
    public List<DoctorAchievementDto> Achievements { get; init; } = new();
}

public sealed class DoctorEducationDto
{
    public long DoctorEducationId { get; set; }
    public int DoctorId { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public int SortOrder { get; set; }
    public Doctor? Doctor { get; set; }
}

public sealed class DoctorAchievementDto
{
    public long DoctorAchievementId { get; set; }
    public int DoctorId { get; set; }
    public string? YearLabel { get; set; }
    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class DoctorExpertiseDto
{
    public long DoctorExpertiseId { get; set; }
    public int DoctorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Doctor? Doctor { get; set; }
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

