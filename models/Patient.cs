using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Patient
{
    [Key]
    public long PatientId { get; set; }
    public string? PatientCode { get; set; }
    public string FullName { get; set; } = default!;
    public string? AllergyText { get; set; }
    public string? Gender { get; set; } // 'M'/'F' theo DB là CHAR(1); map string 1 char cho dễ
    public DateTime? DateOfBirth { get; set; }
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? IdentityNo { get; set; }
    public string? InsuranceNo { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? UserId { get; set; }

    public virtual User? User { get; set; }
}
