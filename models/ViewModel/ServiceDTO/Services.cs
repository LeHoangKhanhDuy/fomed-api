using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Dtos.Services
{
    public class ServiceRequestBase
    {
        [StringLength(50, ErrorMessage = "Mã dịch vụ tối đa 50 ký tự.")]
        public string? Code { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
        public string? Description { get; set; }

        [Range(0, 999_999_999, ErrorMessage = "Giá phải >= 0.")]
        public decimal? BasePrice { get; set; }

        [Range(1, 1440, ErrorMessage = "Thời lượng (phút) phải trong khoảng 1..1440.")]
        public short? DurationMin { get; set; }

        public bool IsActive { get; set; } = true;
        
        [StringLength(300, ErrorMessage = "URL ảnh tối đa 300 ký tự.")]
        [Url(ErrorMessage = "ImageUrl phải là URL hợp lệ.")]
        public string? ImageUrl { get; set; }

        // Optional ở base để dùng lại cho Update (có thể không đổi danh mục)
        public int? CategoryId { get; set; }
    }

    public class ServiceCreateRequest : ServiceRequestBase
    {
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ.")]
        [StringLength(150, ErrorMessage = "Tên dịch vụ tối đa 150 ký tự.")]
        public string Name { get; set; } = "";

        // BẮT BUỘC khi tạo
        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [Range(1, int.MaxValue, ErrorMessage = "Danh mục không hợp lệ.")]
        public new int? CategoryId { get; set; }
    }

    public class ServiceUpdateRequest : ServiceRequestBase
    {
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ.")]
        [StringLength(150, ErrorMessage = "Tên dịch vụ tối đa 150 ký tự.")]
        public string Name { get; set; } = "";

        // Cho phép null (giữ nguyên), nếu có giá trị thì kiểm tra >0
        [Range(1, int.MaxValue, ErrorMessage = "Danh mục không hợp lệ.")]
        public new int? CategoryId { get; set; }
    }

    public class ServiceStatusRequest
    {
        [Required]
        public bool IsActive { get; set; }
    }
}
