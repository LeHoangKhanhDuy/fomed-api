using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class User
{
    [Key]
    public long UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    // public char? Gender { get; set; } // 'M'/'F'
    // public DateOnly? DateOfBirth { get; set; }
    public byte[] PasswordHash { get; set; } = default!;
    public byte[]? PasswordSalt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public virtual ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public virtual UserMfa? Mfa { get; set; }
    public virtual UserProfile Profile { get; set; } = null!;
}
