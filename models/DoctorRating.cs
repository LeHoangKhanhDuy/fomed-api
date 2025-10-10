using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class DoctorRating
{
    [Key]
    public long RatingId { get; set; }
    public int DoctorId { get; set; }
    public long? PatientId { get; set; }
    public byte Score { get; set; } // 1..5
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Doctor Doctor { get; set; } = default!;
    public virtual Patient? Patient { get; set; }
}
