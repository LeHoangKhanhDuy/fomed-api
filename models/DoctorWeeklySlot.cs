using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorWeeklySlot
{
    [Key]
    public long SlotId { get; set; }
    public int DoctorId { get; set; }
    public byte Weekday { get; set; }  // 1..7
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
}
