using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorAchievement
{
    [Key]
    public long AchievementId { get; set; }
    public int DoctorId { get; set; }
    public string? YearLabel { get; set; }
    public string Content { get; set; } = default!;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
}
