using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Doctor
{
    [Key]
    public int DoctorId { get; set; }
    public long UserId { get; set; }
    public string? LicenseNo { get; set; }
    public string? Title { get; set; }
    public int? PrimarySpecialtyId { get; set; }
    public string? AvatarUrl { get; set; }
    public short? ExperienceYears { get; set; }
    public string? ExperienceNote { get; set; }
    public string? Intro { get; set; }
    public int VisitCount { get; set; }
    public decimal RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public string? RoomName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Specialty? PrimarySpecialty { get; set; }
    public virtual User? User { get; set; }
    public virtual ICollection<DoctorSpecialty> DoctorSpecialties { get; set; } = new List<DoctorSpecialty>();
    public virtual ICollection<DoctorWeeklySlot> WeeklySlots { get; set; } = new List<DoctorWeeklySlot>();
    public virtual ICollection<DoctorEducation> Educations { get; set; } = new List<DoctorEducation>();
    public virtual ICollection<DoctorExpertise> Expertises { get; set; } = new List<DoctorExpertise>();
    public virtual ICollection<DoctorAchievement> Achievements { get; set; } = new List<DoctorAchievement>();
    public virtual ICollection<DoctorRating> Ratings { get; set; } = new List<DoctorRating>();
}
