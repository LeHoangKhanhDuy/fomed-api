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
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
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
        // Kiểm tra thuốc có tồn tại không 
        var exists = await db.Medicines.AnyAsync(x => x.MedicineId == id);
        if (!exists)
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc.", 404));

        // Kiểm tra ràng buộc tham chiếu 
        var tLots = db.MedicineLots.AnyAsync(x => x.MedicineId == id);
        var tTxns = db.InventoryTransactions.AnyAsync(x => x.MedicineId == id);
        var tPrescs = db.PrescriptionItems.AnyAsync(x => x.MedicineId == id);
        // Kiểm tra thêm InvoiceItems 
        var tInvoices = db.InvoiceItems.AnyAsync(x => x.RefType == "PrescriptionItem" &&
                                                      db.PrescriptionItems.Any(p => p.ItemId == x.RefId && p.MedicineId == id));

        await Task.WhenAll(tLots, tTxns, tPrescs);

        var hasLots = tLots.Result;
        var hasTxns = tTxns.Result;
        var hasPrescs = tPrescs.Result;

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
            // Xoá cứng 
            var rows = await db.Medicines
                .Where(x => x.MedicineId == id)
                .ExecuteDeleteAsync();

            if (rows == 0)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc (có thể đã bị xóa bởi người khác).", 404));

            return Ok(ApiResponse<object>.Success(new { }, "Đã xóa thuốc vĩnh viễn thành công"));
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Không thể xóa do ràng buộc dữ liệu (DB Constraint): {ex.InnerException?.Message ?? ex.Message}", 500));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Đã xảy ra lỗi khi xóa thuốc: {ex.Message}", 500));
        }
    }

    // API Lấy danh sách lô 
    // GET: api/v1/admin/medicines/5/lots
    [HttpGet("{id:int}/lots")]
    [SwaggerOperation(Summary = "Lấy danh sách lô của thuốc")]
    public async Task<IActionResult> GetLots(int id)
    {
        var exists = await db.Medicines.AnyAsync(x => x.MedicineId == id);
        if (!exists) return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc."));

        var lots = await db.MedicineLots.AsNoTracking()
            .Where(x => x.MedicineId == id)
            .OrderBy(x => x.ExpiryDate)
            .Select(x => new
            {
                x.LotId,
                x.LotNumber,
                x.ExpiryDate,
                x.Quantity,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Success(lots, "Lấy danh sách lô thành công"));
    }

    // API Tạo lô mới
    // POST: api/v1/admin/medicines/5/lots
    [HttpPost("{id:int}/lots")]
    [SwaggerOperation(Summary = "Tạo lô thuốc mới")]
    public async Task<IActionResult> CreateLot(int id, [FromBody] MedicineLotRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LotNumber))
            return BadRequest(ApiResponse<object>.Fail("Số lô không được để trống."));

        var medExists = await db.Medicines.AnyAsync(x => x.MedicineId == id);
        if (!medExists) return NotFound(ApiResponse<object>.Fail("Thuốc không tồn tại."));

        var dup = await db.MedicineLots.AnyAsync(x => x.MedicineId == id && x.LotNumber == req.LotNumber);
        if (dup) return Conflict(ApiResponse<object>.Fail($"Số lô '{req.LotNumber}' đã tồn tại."));

        try
        {
            var lot = new MedicineLot
            {
                MedicineId = id,
                LotNumber = req.LotNumber.Trim(),
                ExpiryDate = req.ExpiryDate,
                Quantity = req.Quantity,
                CreatedAt = DateTime.UtcNow
            };
            db.MedicineLots.Add(lot);
            await db.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(new { lot.LotId, lot.LotNumber }, "Tạo lô thành công"));
        }
        catch (Exception ex) { return StatusCode(500, ApiResponse<object>.Fail(ex.Message, 500)); }
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
        if (req.Quantity == 0) return BadRequest(ApiResponse<object>.Fail("Quantity phải khác 0."));
        if (!req.LotId.HasValue) return BadRequest(ApiResponse<object>.Fail("Bắt buộc phải chọn Lô thuốc (LotId)."));

        var exist = await db.Medicines.AnyAsync(x => x.MedicineId == id);
        if (!exist) return NotFound(ApiResponse<object>.Fail("Không tìm thấy thuốc."));

        // Validate âm kho tổng
        if (req.Quantity < 0)
        {
            var currentTotal = await db.InventoryTransactions.Where(t => t.MedicineId == id).SumAsync(t => (decimal?)t.Quantity) ?? 0m;
            if (currentTotal + req.Quantity < 0) return BadRequest(ApiResponse<object>.Fail("Tổng tồn kho không đủ."));
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                // Ghi Log
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    MedicineId = id,
                    LotId = req.LotId,
                    TxnType = req.TxnType,
                    Quantity = req.Quantity,
                    UnitCost = req.UnitCost,
                    RefNote = req.RefNote
                });

                // Update Lô
                var lot = await db.MedicineLots.FirstOrDefaultAsync(l => l.LotId == req.LotId);
                if (lot == null) throw new Exception("Không tìm thấy Lô thuốc.");

                lot.Quantity += req.Quantity;
                if (lot.Quantity < 0) throw new Exception($"Lô {lot.LotNumber} không đủ hàng để xuất.");

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                var newStock = await db.InventoryTransactions.Where(t => t.MedicineId == id).SumAsync(t => (decimal?)t.Quantity) ?? 0m;
                return Ok(ApiResponse<object>.Success(new { MedicineId = id, Stock = newStock }, "Cập nhật kho thành công"));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ApiResponse<object>.Fail(ex.Message, 500));
            }
        });
    }

    
}
