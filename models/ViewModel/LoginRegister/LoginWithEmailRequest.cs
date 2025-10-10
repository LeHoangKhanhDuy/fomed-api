using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.ViewModel
{
    public sealed class LoginWithEmailRequest
    {
        [Required(ErrorMessage = "Email không được bỏ trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được bỏ trống.")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
        public string Password { get; set; } = string.Empty;
    }
}
