using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public sealed class CreateAppointmentRequest
{
    [Required] public long PatientId { get; set; }
    [Required] public int DoctorId { get; set; }
    public int? ServiceId { get; set; }

    [Required] public DateOnly VisitDate { get; set; }  // yyyy-MM-dd
    [Required] public TimeOnly VisitTime { get; set; }  // HH:mm
    [MaxLength(500)] public string? Reason { get; set; }
}

public sealed class AppointmentResponse
{
    public long AppointmentId { get; set; }
    public string? Code { get; set; }
    public long PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? ServiceId { get; set; }
    public DateOnly VisitDate { get; set; }
    public TimeOnly VisitTime { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "waiting";
    public int? QueueNo { get; set; }
    public DateTime CreatedAt { get; set; }
}
