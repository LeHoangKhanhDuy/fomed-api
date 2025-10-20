using FoMed.Api.Dtos.Services;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/services")]
public class ServiceCateController : ControllerBase
{
    private readonly FoMedContext _db;
    public ServiceCateController(FoMedContext db) => _db = db;

    /* ============== DỊCH VỤ =============== */
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Danh sách dịch vụ", Description = "Danh sách tất cả dịch vụ khám chữa bệnh", Tags = new[] { "Services" })]
    public async Task<IActionResult> GetAllRaw(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Services
            .AsNoTracking()
            .Include(s => s.Category)
            .AsQueryable();

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.ServiceId,
                s.Code,
                s.Name,
                s.Description,
                s.BasePrice,
                s.DurationMin,
                s.IsActive,
                s.ImageUrl,
                Category = s.Category == null
                    ? null
                    : new { s.Category.CategoryId, s.Category.Name, s.Category.ImageUrl }
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            message = "Lấy toàn bộ dịch vụ thành công",
            total,
            page,
            pageSize,
            data = items
        });
    }

    /* ============== CHI TIẾT DANH MỤC DỊCH VỤ =============== */
    [HttpGet("details/{id:int}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Chi tiết dịch vụ", Description = "Lấy chi tiết từng dịch vụ khám chữa bệnh", Tags = new[] { "Services" })]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var service = await _db.Services
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.ServiceId == id)
            .Select(s => new
            {
                s.ServiceId,
                s.Code,
                s.Name,
                s.Description,
                s.BasePrice,
                s.DurationMin,
                s.IsActive,
                s.ImageUrl,
                Category = s.Category == null
                ? null
                : new { s.Category.CategoryId, s.Category.Name, s.Category.ImageUrl }
            }).FirstOrDefaultAsync();
        if (service == null)
            return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        return Ok(new { success = true, data = service });
    }

    /* ============== TẠO DANH MỤC DỊCH VỤ =============== */
    [HttpPost("add")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Tạo dịch vụ", Description = "Tạo dịch vụ khám bệnh", Tags = new[] { "Services" })]
    public async Task<IActionResult> Create([FromBody] ServiceCreateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var existsCode = await _db.Services.AnyAsync(s => s.Code == req.Code);
            if (existsCode) return BadRequest(new { success = false, message = "Mã dịch vụ đã tồn tại." });
        }

        var category = await _db.ServiceCategories.FindAsync(req.CategoryId);
        if (category == null)
            return BadRequest(new { success = false, message = "Danh mục không hợp lệ." });

        var service = new Service
        {
            Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            CategoryId = req.CategoryId,
            BasePrice = req.BasePrice ?? 0,
            DurationMin = req.DurationMin,
            IsActive = req.IsActive,
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl!.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Tạo dịch vụ thành công", data = new { service.ServiceId } });
    }

    /* ============== CẬP NHẬT DANH MỤC DỊCH VỤ =============== */
    [HttpPut("update/{id:int}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Cập nhật dịch vụ", Description = "Cập nhật dịch vụ khám chữa bệnh", Tags = new[] { "Services" })]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] ServiceUpdateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var s = await _db.Services.FirstOrDefaultAsync(x => x.ServiceId == id);
        if (s == null) return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var dup = await _db.Services.AnyAsync(x => x.Code == req.Code && x.ServiceId != id);
            if (dup) return BadRequest(new { success = false, message = "Mã dịch vụ đã tồn tại." });
            s.Code = req.Code.Trim();
        }
        else
        {
            s.Code = null; // cho phép xóa mã
        }

        // Đảm bảo category hợp lệ
        var category = await _db.ServiceCategories.FindAsync(req.CategoryId);
        if (category == null)
            return BadRequest(new { success = false, message = "Danh mục không hợp lệ." });

        s.Name = req.Name.Trim();
        s.Description = req.Description?.Trim();
        if (req.BasePrice.HasValue) s.BasePrice = req.BasePrice.Value;
        if (req.DurationMin.HasValue) s.DurationMin = req.DurationMin.Value;
        s.CategoryId = req.CategoryId;
        s.IsActive = req.IsActive;
        s.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl!.Trim();
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "Cập nhật dịch vụ thành công" });
    }

    /* ============== CẬP NHẬT TRẠNG THÁI DANH MỤC DỊCH VỤ =============== */
    [HttpPatch("status/{id:int}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Bật/Tắt dịch vụ", Description = "Bật/Tắt trạng thái dịch vụ", Tags = new[] { "Services" })]
    public async Task<IActionResult> ToggleStatus([FromRoute] int id, [FromBody] ServiceStatusRequest req)
    {
        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        s.IsActive = req.IsActive;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
    }

    /* ============== XÓA DANH MỤC DỊCH VỤ =============== */
    [HttpDelete("remove/{id:int}")]
    [Produces("application/json")]
    [SwaggerOperation(Summary = "Xóa dịch vụ", Description = "Chỉ xóa khi không còn ràng buộc quan trọng (tùy nghiệp vụ)", Tags = new[] { "Services" })]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound(new { success = false, message = "Không tìm thấy dịch vụ." });

        var used = await _db.InvoiceItems.AnyAsync(ii => ii.RefType == "Service" && ii.RefId == id);
        if (used) return BadRequest(new { success = false, message = "Dịch vụ đang được sử dụng, không thể xóa." });

        _db.Services.Remove(s);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Xóa dịch vụ thành công" });
    }
}