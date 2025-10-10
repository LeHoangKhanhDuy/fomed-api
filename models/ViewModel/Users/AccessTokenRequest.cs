using System.ComponentModel.DataAnnotations;

public sealed class AccessTokenRequest
{
    [Required(ErrorMessage = "Email không được bỏ trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được bỏ trống.")]
    public string Password { get; set; } = string.Empty;
}
