// Controllers/MedicinesController.cs
using FoMed.Api.Models;
using FoMed.Api.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/admin/medicines")]
public sealed class MedicinesController(FoMedContext db) : ControllerBase
{

    /* ================== LẤY DANH SÁCH THUỐC ==================== */
    [HttpGet]
    [SwaggerOperation(
    Summary = "Lấy danh sách thuốc",
    Description = "Danh sách tất cả thuốc. Strength: khối lượng, Form: viên nén, viên nang,... Unit: hộp, vỉ, viên,...")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll(
    [FromQuery] int page = 1,
    [FromQuery(Name = "limit")] int pageSize = 20)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var baseQuery = db.Medicines.AsNoTracking();

            var total = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(x => x.Name).ThenBy(x => x.MedicineId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new MedicineItemResponse
                {
                    MedicineId = x.MedicineId,
                    Code = x.Code,
                    Name = x.Name,
                    Strength = x.Strength,
                    Form = x.Form,
                    Unit = x.Unit,
                    BasePrice = x.BasePrice,
                    IsActive = x.IsActive,
                    Stock = db.InventoryTransactions
                                   .Where(t => t.MedicineId == x.MedicineId)
                                   .Sum(t => (decimal?)t.Quantity) ?? 0m
                })
                .ToListAsync();

            var result = new MedicinePageResult<MedicineItemResponse>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            };

            return Ok(ApiResponse<MedicinePageResult<MedicineItemResponse>>.Success(result, "Lấy danh sách thuốc thành công"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi khi lấy danh sách thuốc: {ex.Message}", 500));
        }
    }


    // GET: api/medicines/5
    [HttpGet("details/{id:int}")]
    [SwaggerOperation(Summary = "Lấy chi tiết thuốc", Description = "Chi tiết của thuốc")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var item = await db.Medicines.AsNoTracking()
                .Where(x => x.MedicineId == id)
                .Select(x => new MedicineItemResponse
                {
                    MedicineId = x.MedicineId,
                    Code = x.Code,
                    Name = x.Name,
                    Strength = x.Strength,
                    Form = x.Form,
                    Unit = x.Unit,
                    BasePrice = x.BasePrice,
                    IsActive = x.IsActive,
                    Stock = db.InventoryTransactions
                                   .Where(t => t.MedicineId == x.MedicineId)
                                   .Sum(t => (decimal?)t.Quantity) ?? 0m
                })
                .FirstOrDefaultAsync();

            if (item is null)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            return Ok(ApiResponse<MedicineItemResponse>.Success(item, "Lấy chi tiết thuốc thành công"));
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi: {ex.Message}", 500));
        }
    }


    // POST: api/medicines
    [HttpPost("create")]
    [SwaggerOperation(Summary = "Tạo thuốc", Description = "Chi tiết của thuốc")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] MedicineCreateRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ", 400));

        try
        {
            if (!string.IsNullOrWhiteSpace(req.Code))
            {
                var dup = await db.Medicines.AnyAsync(x => x.Code == req.Code);
                if (dup)
                    return Conflict(ApiResponse<object>.Fail("Mã thuốc đã tồn tại.", 409));
            }

            var m = new Medicine
            {
                Code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim(),
                Name = req.Name.Trim(),
                Strength = string.IsNullOrWhiteSpace(req.Strength) ? null : req.Strength.Trim(),
                Form = string.IsNullOrWhiteSpace(req.Form) ? null : req.Form.Trim(),
                Unit = req.Unit.Trim(),
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
                BasePrice = req.BasePrice,
                IsActive = req.IsActive
            };

            db.Medicines.Add(m);
            await db.SaveChangesAsync();

            var res = new MedicineItemResponse
            {
                MedicineId = m.MedicineId,
                Code = m.Code,
                Name = m.Name,
                Strength = m.Strength,
                Form = m.Form,
                Unit = m.Unit,
                BasePrice = m.BasePrice,
                IsActive = m.IsActive,
                Stock = 0
            };

            return CreatedAtAction(nameof(GetById), new { id = m.MedicineId },
                ApiResponse<MedicineItemResponse>.Success(res, "Tạo thuốc thành công", 201));
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu khi thêm thuốc: {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi: {ex.Message}", 500));
        }
    }


    // PUT: api/medicines/5
    [HttpPut("update/{id:int}")]
    [SwaggerOperation(Summary = "Cập nhật thông tin thuốc", Description = "Chi tiết của thuốc")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(int id, [FromBody] MedicineUpdateRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            // Lấy dữ liệu hiện tại (nhẹ, không tracking)
            var current = await db.Medicines.AsNoTracking()
                .Where(x => x.MedicineId == id)
                .Select(x => new
                {
                    x.MedicineId,
                    x.Code,
                    x.Name,
                    x.Strength,
                    x.Form,
                    x.Unit,
                    x.Note,
                    x.BasePrice,
                    x.IsActive
                })
                .FirstOrDefaultAsync();

            if (current is null)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            // Chuẩn hoá input
            var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim();
            var name = req.Name.Trim();
            var strength = string.IsNullOrWhiteSpace(req.Strength) ? null : req.Strength.Trim();
            var form = string.IsNullOrWhiteSpace(req.Form) ? null : req.Form.Trim();
            var unit = req.Unit.Trim();
            var note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
            var price = req.BasePrice;
            var active = req.IsActive;

            // Trùng mã (chỉ check khi thay đổi mã)
            if (!string.Equals(code, current.Code, StringComparison.OrdinalIgnoreCase) && code is not null)
            {
                var exists = await db.Medicines.AnyAsync(x => x.Code == code && x.MedicineId != id);
                if (exists)
                    return Conflict(ApiResponse<object>.Fail("Mã thuốc đã tồn tại.", 409));
            }

            // Không có thay đổi?
            var noChange =
                string.Equals(code, current.Code, StringComparison.Ordinal) &&
                string.Equals(name, current.Name, StringComparison.Ordinal) &&
                string.Equals(strength, current.Strength, StringComparison.Ordinal) &&
                string.Equals(form, current.Form, StringComparison.Ordinal) &&
                string.Equals(unit, current.Unit, StringComparison.Ordinal) &&
                string.Equals(note, current.Note, StringComparison.Ordinal) &&
                price == current.BasePrice &&
                active == current.IsActive;

            if (noChange)
            {
                return Ok(ApiResponse<object>.Success(
                    new
                    {
                        id = current.MedicineId,
                        code = current.Code,
                        name = current.Name,
                        strength = current.Strength,
                        form = current.Form,
                        unit = current.Unit,
                        note = current.Note,
                        basePrice = current.BasePrice,
                        isActive = current.IsActive
                    },
                    "Không có thay đổi"
                ));
            }

            // Cập nhật trực tiếp (không load entity)
            var rows = await db.Medicines
                .Where(x => x.MedicineId == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Code, code)
                    .SetProperty(x => x.Name, name)
                    .SetProperty(x => x.Strength, strength)
                    .SetProperty(x => x.Form, form)
                    .SetProperty(x => x.Unit, unit)
                    .SetProperty(x => x.Note, note)
                    .SetProperty(x => x.BasePrice, price)
                    .SetProperty(x => x.IsActive, active)
                #if true   // nếu entity Medicine có UpdatedAt
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                #endif
                );

            if (rows == 0)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            return Ok(ApiResponse<object>.Success(
                new
                {
                    id,
                    code,
                    name,
                    strength,
                    form,
                    unit,
                    note,
                    basePrice = price,
                    isActive = active
                },
                "Cập nhật thuốc thành công"
            ));
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu khi cập nhật thuốc: {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi khi cập nhật thuốc: {ex.Message}", 500));
        }
    }


    // PATCH: api/medicines/5/active?value=true
    [HttpPatch("active/{id:int}")]
    [SwaggerOperation(Summary = "Bật/Tắt trạng thái của thuốc", Description = "Chỉnh sửa trạng thái của thuốc")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value)
    {
        try
        {
            // Lấy trạng thái hiện tại, không tracking cho nhẹ
            var current = await db.Medicines.AsNoTracking()
                .Where(x => x.MedicineId == id)
                .Select(x => x.IsActive)
                .FirstOrDefaultAsync();

            if (current == default && !await db.Medicines.AnyAsync(x => x.MedicineId == id))
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            if (current == value)
            {
                return Ok(ApiResponse<object>.Success(
                    new { MedicineId = id, IsActive = current },
                    "Trạng thái không thay đổi"
                ));
            }

            // Update trực tiếp, không cần load entity
            var rows = await db.Medicines
                .Where(x => x.MedicineId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, value));

            if (rows == 0)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            return Ok(ApiResponse<object>.Success(
                new { MedicineId = id, IsActive = value },
                value ? "Đã bật sử dụng thuốc" : "Đã tắt (ngừng sử dụng) thuốc"
            ));
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu khi cập nhật trạng thái thuốc: {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi khi cập nhật trạng thái thuốc: {ex.Message}", 500));
        }
    }


    // DELETE: api/medicines/5
    [HttpDelete("remove/{id:int}")]
    [SwaggerOperation(Summary = "Xóa thuốc", Description = "Xóa thuốc không còn được sử dụng")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id)
    {
        // chỉ lấy nhẹ cho nhanh
        var med = await db.Medicines.AsNoTracking()
            .Where(x => x.MedicineId == id)
            .Select(x => new { x.MedicineId, x.Name, x.IsActive })
            .FirstOrDefaultAsync();

        if (med is null)
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

        // kiểm tra ràng buộc tham chiếu
        var hasLots = await db.MedicineLots.AnyAsync(x => x.MedicineId == id);
        var hasTxns = await db.InventoryTransactions.AnyAsync(x => x.MedicineId == id);
        var hasPrescs = await db.PrescriptionItems.AnyAsync(x => x.MedicineId == id);

        if (hasLots || hasTxns || hasPrescs)
        {
            var reasons = new List<string>();
            if (hasLots) reasons.Add("đã có lô thuốc");
            if (hasTxns) reasons.Add("đã phát sinh giao dịch kho");
            if (hasPrescs) reasons.Add("đã nằm trong đơn thuốc");

            return BadRequest(ApiResponse<object>.Fail(
                $"Không thể xóa vì {string.Join(", ", reasons)}. " +
                "Hãy chuyển trạng thái thuốc sang 'ngừng hoạt động' (IsActive = false) để ngừng sử dụng."));
        }

        try
        {
            // xoá nhanh, không cần load entity đầy đủ
            var rows = await db.Medicines
                .Where(x => x.MedicineId == id)
                .ExecuteDeleteAsync();

            if (rows == 0)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

            return Ok(ApiResponse<object>.Success(null, "Đã xóa thuốc thành công"));
        }
        catch (DbUpdateException ex)
        {
            // phòng trường hợp còn ràng buộc DB khác
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu khi xóa thuốc: {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi khi xóa thuốc: {ex.Message}", 500));
        }
    }


    // POST: api/medicines/5/inventory  (nhập/xuất/điều chỉnh kho)
    [HttpPost("inventory/{id:int}")]
    [SwaggerOperation(Summary = "Nhập/Xuất kho thuốc", Description = "TxnType phải là: in, out, adjust (xuất, nhập, điều chỉnh)")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InventoryAdjust(int id, [FromBody] InvAdjustRequest req)
    {
        try
        {
            // thuốc tồn tại?
            var exist = await db.Medicines.AnyAsync(x => x.MedicineId == id);
            if (!exist)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc."));

            // validate nhập liệu
            if (req.TxnType is not ("in" or "out" or "adjust"))
                return BadRequest(ApiResponse<object>.Fail("TxnType phải là 'in' | 'out' | 'adjust'."));

            if (req.Quantity == 0)
                return BadRequest(ApiResponse<object>.Fail("Quantity phải khác 0."));

            // chặn âm kho khi xuất/giảm
            if (req.Quantity < 0)
            {
                var current = await db.InventoryTransactions
                                      .Where(t => t.MedicineId == id)
                                      .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

                if (current + req.Quantity < 0)
                    return BadRequest(ApiResponse<object>.Fail("Tồn kho không đủ."));
            }

            // ghi giao dịch
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                MedicineId = id,
                LotId = req.LotId,
                TxnType = req.TxnType,
                Quantity = req.Quantity,
                UnitCost = req.UnitCost,
                RefNote = req.RefNote
            });
            await db.SaveChangesAsync();

            // tồn kho mới
            var stock = await db.InventoryTransactions
                                .Where(t => t.MedicineId == id)
                                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            return Ok(ApiResponse<object>.Success(new { MedicineId = id, Stock = stock },"Cập nhật tồn kho thành công"));
        }
        catch (DbUpdateException ex)
        {
            // lỗi DB (ví dụ column không tồn tại, FK, v.v.)
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail($"Lỗi cơ sở dữ liệu khi ghi tồn kho: {ex.InnerException?.Message ?? ex.Message}",500));
        }
        catch (Exception ex)
        {
            // lỗi không lường trước
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail($"Đã xảy ra lỗi khi cập nhật tồn kho: {ex.Message}", 500));
        }
    }
}
