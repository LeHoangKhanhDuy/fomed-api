using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorEducation
{
    [Key]
    public long EducationId { get; set; }
    public int DoctorId { get; set; }
    public short? YearFrom { get; set; }
    public short? YearTo { get; set; }
    public string Title { get; set; } = default!;
    public string? Detail { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
}
