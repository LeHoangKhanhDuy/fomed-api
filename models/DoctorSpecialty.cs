namespace FoMed.Api.Models;

public class DoctorSpecialty
{
    public int DoctorId { get; set; }
    public int SpecialtyId { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
    public virtual Specialty Specialty { get; set; } = default!;
}
