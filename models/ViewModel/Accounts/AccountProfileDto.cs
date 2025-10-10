
namespace FoMed.Api.ViewModels.Accounts;

public sealed class AccountProfileDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Gender { get; set; } 
    public DateTime? DateOfBirth { get; set; }

    // profile
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public DateTime? ProfileUpdatedAt { get; set; }
}
