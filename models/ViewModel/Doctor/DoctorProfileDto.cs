public class CreateDoctorProfileRequest
{
    public long UserId { get; set; }
    public string? Title { get; set; }
    public int? PrimarySpecialtyId { get; set; }
    public string? LicenseNo { get; set; }
    public string? RoomName { get; set; }
    public short? ExperienceYears { get; set; }
    public string? ExperienceNote { get; set; }
    public string? Intro { get; set; }
}

public class UpdateDoctorProfileRequest
{
    public string? Title { get; set; }
    public int? PrimarySpecialtyId { get; set; }
    public string? LicenseNo { get; set; }
    public string? RoomName { get; set; }
    public short? ExperienceYears { get; set; }
    public string? ExperienceNote { get; set; }
    public string? Intro { get; set; }
    public bool? IsActive { get; set; }
}