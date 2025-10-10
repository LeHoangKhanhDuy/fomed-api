using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class VisitQueue
{
    [Key]
    public int DoctorId { get; set; }
    public DateOnly VisitDate { get; set; }
    public int QueueNo { get; set; }
    public long? AppointmentId { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
    public virtual Appointment? Appointment { get; set; }
}
