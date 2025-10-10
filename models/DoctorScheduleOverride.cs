using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorScheduleOverride
{
    [Key]
    public long OverrideId { get; set; }
    public int DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsAvailable { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Note { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
}
