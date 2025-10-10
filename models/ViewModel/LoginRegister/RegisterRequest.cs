using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace FoMed.Api.ViewModel;

public class RegisterRequest
{
    /// <example>Nguyễn Văn A</example>
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string FullName { get; set; } = null!;  

    /// <example>example@gmail.com</example>
    public string? Email { get; set; }

    /// <example>0912345678</example>
    public string? Phone { get; set; }

    /// <example>Passw0rd!</example>
    [Required]
    public string Password { get; set; } = default!;

    /// <example>M</example>
    public char? Gender { get; set; }

    /// <summary>Ngày sinh theo định dạng dd/MM/yyyy</summary>
    /// <example>29/09/2025</example>
    public string? DateOfBirth { get; set; }

}
