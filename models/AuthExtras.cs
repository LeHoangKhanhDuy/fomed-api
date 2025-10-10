using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class EmailVerificationToken
{
    [Key]
    public long TokenId { get; set; }
    public long UserId { get; set; }
    public byte[] TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual User User { get; set; } = default!;
}

public class UserSession
{
    [Key]
    public Guid SessionId { get; set; }
    public long UserId { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public virtual User User { get; set; } = default!;
}

public class UserExternalLogin
{
    [Key]
    public long ExternalLoginId { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = default!;
    public string ProviderUserId { get; set; } = default!;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime LinkedAt { get; set; }
    public virtual User User { get; set; } = default!;
}

public class UserMfa
{
    [Key]
    public long UserId { get; set; }
    public byte[]? TOTPSecret { get; set; }
    public byte[]? RecoveryCodes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User User { get; set; } = default!;
}
