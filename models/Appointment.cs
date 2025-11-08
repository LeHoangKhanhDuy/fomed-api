using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Appointment
{
    [Key]
    public long AppointmentId { get; set; }

    [MaxLength(30)]
    public string? Code { get; set; }
    public long PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? ServiceId { get; set; }
    public DateOnly VisitDate { get; set; }
    public TimeOnly VisitTime { get; set; }
    public string? Reason { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "waiting";
    public int? QueueNo { get; set; }
    public decimal? FinalCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }


    // ===== Navigation =====
    public virtual Patient Patient { get; set; } = default!;
    public virtual Doctor Doctor { get; set; } = default!;
    public virtual Service? Service { get; set; }
}
