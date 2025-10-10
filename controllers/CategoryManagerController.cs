using FoMed.Api.Dtos.ServiceCategories;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace FoMed.Api.Controllers;

[ApiController]
[Route("api/v1/admin/")]
[Authorize(Roles = "ADMIN")]
public class AdminServiceCategoriesController : ControllerBase
{
    private readonly FoMedContext _db;
    public AdminServiceCategoriesController(FoMedContext db) => _db = db;

    [HttpGet("categories")]
    [SwaggerOperation(Summary = "Danh sách danh mục", Description = "Lọc theo từ khóa, trạng thái", Tags = new[] { "Categories" })]
    public async Task<IActionResult> GetList([FromQuery] string? keyword, [FromQuery] bool? isActive)
    {
        var q = _db.ServiceCategories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.Name.Contains(k) || (c.Code != null && c.Code.Contains(k)));
        }
        if (isActive != null) q = q.Where(c => c.IsActive == isActive);

        var items = await q.OrderBy(c => c.Name)
                           .Select(c => new
                           {
                               c.CategoryId,
                               c.Code,
                               c.Name,
                               c.IsActive,
                               c.CreatedAt,
                               c.UpdatedAt
                           }).ToListAsync();

        return Ok(new { success = true, message = "OK", data = items });
    }

    [HttpGet("categories/details/{id:int}")]
    [SwaggerOperation(Summary = "Chi tiết danh mục", Tags = new[] { "Categories" })]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var c = await _db.ServiceCategories.AsNoTracking()
                    .Where(x => x.CategoryId == id)
                    .Select(x => new { x.CategoryId, x.Code, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt })
                    .FirstOrDefaultAsync();
        if (c == null) return NotFound(new { success = false, message = "Không tìm thấy danh mục." });
        return Ok(new { success = true, message = "OK", data = c });
    }

    [HttpPost("categories/add")]
    [SwaggerOperation(Summary = "Tạo danh mục", Tags = new[] { "Categories" })]
    public async Task<IActionResult> Create([FromBody] ServiceCategoryCreateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var exists = await _db.ServiceCategories.AnyAsync(x => x.Code == req.Code);
            if (exists) return BadRequest(new { success = false, message = "Mã danh mục đã tồn tại." });
        }
        var nameExists = await _db.ServiceCategories.AnyAsync(x => x.Name == req.Name);
        if (nameExists) return BadRequest(new { success = false, message = "Tên danh mục đã tồn tại." });

        var c = new ServiceCategory
        {
            Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim(),
            Name = req.Name.Trim(),
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ServiceCategories.Add(c);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Tạo danh mục thành công", data = new { c.CategoryId } });
    }

    [HttpPut("categories/update/{id:int}")]
    [SwaggerOperation(Summary = "Cập nhật danh mục", Tags = new[] { "Categories" })]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] ServiceCategoryUpdateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var c = await _db.ServiceCategories.FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c == null) return NotFound(new { success = false, message = "Không tìm thấy danh mục." });

        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var exists = await _db.ServiceCategories.AnyAsync(x => x.Code == req.Code && x.CategoryId != id);
            if (exists) return BadRequest(new { success = false, message = "Mã danh mục đã tồn tại." });
            c.Code = req.Code.Trim();
        }
        else c.Code = null;

        var nameExists = await _db.ServiceCategories.AnyAsync(x => x.Name == req.Name && x.CategoryId != id);
        if (nameExists) return BadRequest(new { success = false, message = "Tên danh mục đã tồn tại." });

        c.Name = req.Name.Trim();
        c.IsActive = req.IsActive;
        c.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "Cập nhật danh mục thành công" });
    }

    [HttpPatch("categories/status/{id:int}")]
    [SwaggerOperation(Summary = "Bật/Tắt danh mục", Tags = new[] { "Categories" })]
    public async Task<IActionResult> Toggle([FromRoute] int id, [FromBody] ServiceCategoryStatusRequest req)
    {
        var c = await _db.ServiceCategories.FindAsync(id);
        if (c == null) return NotFound(new { success = false, message = "Không tìm thấy danh mục." });

        c.IsActive = req.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
    }

    [HttpDelete("categories/remove/{id:int}")]
    [SwaggerOperation(Summary = "Xóa danh mục (chỉ khi không còn dịch vụ)", Tags = new[] { "Categories" })]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var c = await _db.ServiceCategories.Include(x => x.Services).FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c == null) return NotFound(new { success = false, message = "Không tìm thấy danh mục." });

        if (await _db.Services.AnyAsync(s => s.CategoryId == id))
            return BadRequest(new { success = false, message = "Danh mục đang được sử dụng. Hãy chuyển dịch vụ sang danh mục khác trước." });

        _db.ServiceCategories.Remove(c);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "Xóa danh mục thành công" });
    }
}
