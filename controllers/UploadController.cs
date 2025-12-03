using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoMed.Api.Controllers
{
    [ApiController]
    [Route("api/v1/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("image")]
        [Authorize(Roles = "ADMIN,EMPLOYEE")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Vui lòng chọn file." });

            // 1. Kiểm tra định dạng
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
                return BadRequest(new { success = false, message = "Định dạng file không hỗ trợ." });

            // 2. Tạo thư mục lưu trữ 
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "common");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            // 3. Tạo tên file unique
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadPath, fileName);

            // 4. Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 5. Trả về URL
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var fileUrl = $"/uploads/common/{fileName}";

            return Ok(new
            {
                success = true,
                message = "Upload thành công",
                data = new { url = fileUrl }
            });
        }
    }
}