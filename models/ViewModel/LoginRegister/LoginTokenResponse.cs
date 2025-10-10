namespace FoMed.Api.ViewModel;

public sealed class LoginTokenResponse
{
    public string Token { get; set; } = string.Empty;         // access token (JWT)
    public DateTime ExpiresAt { get; set; }                   // access token expiry
    public string RefreshToken { get; set; } = string.Empty;  // refresh token
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
