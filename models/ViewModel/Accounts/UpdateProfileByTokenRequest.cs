
using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.ViewModels.Accounts;

public sealed class UpdateProfileByTokenRequest
{
    [Required(ErrorMessage = "Token không được bỏ trống.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    // Nếu cần bắt buộc phone -> thêm [Required]
    [StringLength(32, ErrorMessage = "Số điện thoại tối đa 32 ký tự.")]
    [RegularExpression(@"^(0|\+84)\d{9,10}$",
        ErrorMessage = "Số điện thoại không đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "Ảnh đại diện tối đa 500 ký tự.")]
    [Url(ErrorMessage = "Đường dẫn ảnh đại diện không hợp lệ.")]
    public string? AvatarUrl { get; set; }

    [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự.")]
    public string? Address { get; set; }

    [StringLength(500, ErrorMessage = "Giới thiệu tối đa 500 ký tự.")]
    public string? Bio { get; set; }
}
