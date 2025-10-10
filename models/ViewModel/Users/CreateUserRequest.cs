using System.ComponentModel.DataAnnotations;

public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [MinLength(2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được bỏ trống.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được bỏ trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*\W).+$",
        ErrorMessage = "Mật khẩu phải có chữ thường, chữ hoa, chữ số và ký tự đặc biệt.")]
    public string Password { get; set; } = string.Empty;
    public List<string>? Roles { get; set; }
}
