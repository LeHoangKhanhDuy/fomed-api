using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.ViewModels.Patients;

public record PatientListVm(
    long PatientId,
    string? PatientCode,
    string FullName,
    string? Gender,
    string? DateOfBirth,   // "yyyy-MM-dd" hoặc null
    string Phone,
    string? Email,
    string? Address,
    string? District,
    string? City,
    string? Province,
    string? IdentityNo,
    bool IsActive,
    DateTime CreatedAt
);


public record PatientDetailVm(
    long PatientId,
    string? PatientCode,
    string FullName,
    string? Gender,
    DateTime? DateOfBirth,
    string Phone,
    string? Email,
    string? Address,
    string? City,
    string? Province,
    string? District,
    string? IdentityNo,
    string? InsuranceNo,
    string? Note,
    string? AllergyText,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class PatientCreateReq
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = default!;

    [Required, MaxLength(20)]
    public string Phone { get; set; } = default!;

    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? IdentityNo { get; set; }
    public string? InsuranceNo { get; set; }
    public string? Note { get; set; }
    public string? AllergyText { get; set; }
}

public class PatientUpdateReq : PatientCreateReq { }

public record ToggleStatusReq(bool IsActive);
