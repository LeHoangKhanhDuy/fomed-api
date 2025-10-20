using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Dtos.ServiceCategories
{
    public class ServiceCategoryCreateRequest
    {
        [StringLength(50, ErrorMessage = "Mã danh mục tối đa 50 ký tự.")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự.")]
        public string Name { get; set; } = "";

        [StringLength(300, ErrorMessage = "URL ảnh tối đa 300 ký tự.")]
        [Url(ErrorMessage = "ImageUrl phải là URL hợp lệ.")]
        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class ServiceCategoryUpdateRequest : ServiceCategoryCreateRequest { }

    public class ServiceCategoryStatusRequest
    {
        [Required]
        public bool IsActive { get; set; }
    }
}
