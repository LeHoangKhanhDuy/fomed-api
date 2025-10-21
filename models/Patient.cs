using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoMed.Api.Models;

[Table("Patients")]
public class Patient
{
    [Key]
    public long PatientId { get; set; }

    [MaxLength(20)]
    public string? PatientCode { get; set; }

    [MaxLength(200)]
    [Required]
    public string FullName { get; set; } = default!;

    [MaxLength(1)] // 'M' / 'F' / 'O'
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    [Required]
    public string Phone { get; set; } = default!;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    public string? District { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }

    public string? IdentityNo { get; set; }
    public string? InsuranceNo { get; set; }

    public string? Note { get; set; }

    public string? AllergyText { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public long? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}
