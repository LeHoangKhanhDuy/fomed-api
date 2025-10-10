
using System.ComponentModel.DataAnnotations;

public sealed class AvatarUploadRequest
{
    [Required(ErrorMessage = "Vui lòng chọn ảnh.")]
    public IFormFile File { get; set; } = default!;
}
