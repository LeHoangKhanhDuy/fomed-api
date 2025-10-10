using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorExpertise
{
    [Key]
    public long ExpertiseId { get; set; }
    public int DoctorId { get; set; }
    public string Content { get; set; } = default!;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
}
