using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Specialty
{
    [Key]
    public int SpecialtyId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<DoctorSpecialty> DoctorSpecialties { get; set; } = new List<DoctorSpecialty>();
}
