using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Encounter
{
    [Key]
    public long EncounterId { get; set; }
    [MaxLength(20)]
    public string? Code { get; set; }
    public long? AppointmentId { get; set; }
    public long PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? ServiceId { get; set; }

    public string? Symptoms { get; set; }
    public string? DiagnosisText { get; set; }
    public string? DoctorNote { get; set; }

    public string Status { get; set; } = "draft";
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }
    public virtual Patient Patient { get; set; } = default!;
    public virtual Doctor Doctor { get; set; } = default!;
    public virtual Service? Service { get; set; }

    public virtual ICollection<EncounterPrescription> Prescriptions { get; set; } = new List<EncounterPrescription>();
    public virtual ICollection<EncounterLabTest> EncounterLabTests { get; set; } = new List<EncounterLabTest>();
}
