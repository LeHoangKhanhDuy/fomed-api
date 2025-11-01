using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FoMed.Api.Models;
using FoMed.Api.Dtos.Billing;

namespace FoMed.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admin/billing")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public class BillingController : ControllerBase
    {
        private readonly FoMedContext _db;
        public BillingController(FoMedContext db)
        {
            _db = db;
        }

        /* =====================  CHỜ THANH TOÁN ===================== */
        [HttpGet("pending")]
        [Produces("application/json")]
        [SwaggerOperation(
            Summary = "Danh sách ca chờ thanh toán",
            Description = "Các hoá đơn chưa thanh toán đủ (Status != 'paid')",
            Tags = new[] { "Billing" }
        )]
        public async Task<IActionResult> GetPending(CancellationToken ct = default)
        {
            // Lấy invoice chưa paid
            var list = await _db.Invoices
                .AsNoTracking()
                .Where(inv => inv.Status != "paid" && inv.Status != "cancelled")
                .OrderBy(inv => inv.CreatedAt)
                .Select(inv => new PendingBillingRowDto
                {
                    InvoiceId = inv.InvoiceId,
                    InvoiceCode = inv.Code,
                    CaseCode = inv.PatientCode ?? "",
                    PatientName = inv.PatientName,
                    DoctorName = inv.DoctorName ?? "",
                    ServiceName = inv.Items
                        .OrderBy(i => i.InvoiceItemId)
                        .Select(i => i.Description)
                        .FirstOrDefault() ?? "-",
                    FinishedTime = inv.CreatedAt.ToLocalTime().ToString("HH:mm"),
                    FinishedDate = inv.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                    TotalAmount = inv.TotalAmount
                })
                .ToListAsync(ct);

            return Ok(new
            {
                success = true,
                data = list
            });
        }

        /* ===================== DANH SÁCH HOÁ ĐƠN ===================== */
        [HttpGet("invoices")]
        [Authorize(Roles = "ADMIN,EMPLOYEE")]
        public async Task<IActionResult> ListInvoices(
    [FromQuery] int page = 1,
    [FromQuery] int limit = 20,
    [FromQuery] string? keyword = null,
    [FromQuery] string? status = null,
    CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            // 1. Build query cơ bản
            var query = _db.Invoices.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var q = keyword.Trim().ToLower();
                query = query.Where(inv =>
                    inv.Code.ToLower().Contains(q) ||
                    inv.PatientName.ToLower().Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim().ToLower(); // "paid" | "partial" | ...
                query = query.Where(inv => inv.Status.ToLower() == st);
            }

            var totalItems = await query.CountAsync(ct);

            // 2. Truy vấn một lần từ DB ra anonymous type (an toàn cho EF Core)
            var rawRows = await query
                .OrderByDescending(inv => inv.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(inv => new
                {
                    inv.InvoiceId,
                    inv.Code,
                    inv.PatientName,
                    inv.CreatedAt,
                    inv.PaidAmount,
                    inv.TotalAmount,
                    LastPaymentMethod = inv.Payments
                        .OrderByDescending(p => p.PaidAt)
                        .Select(p => p.Method)
                        .FirstOrDefault(),
                    inv.Status
                })
                .ToListAsync(ct);

            // 3. Map sang DTO thật sự (ngoài DB => không còn bị EF ràng buộc)
            var items = rawRows.Select(inv => new InvoiceListRowDto
            {
                InvoiceId = inv.InvoiceId, // <-- kiểu long -> long, không cast
                InvoiceCode = inv.Code,
                PatientName = inv.PatientName,
                VisitDate = inv.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                PaidAmount = inv.PaidAmount,
                RemainingAmount = inv.TotalAmount - inv.PaidAmount,
                TotalAmount = inv.TotalAmount,
                LastPaymentMethod = inv.LastPaymentMethod,
                StatusLabel = inv.Status switch
                {
                    "paid" => "Đã thanh toán",
                    "partial" => "Thanh toán một phần",
                    "unpaid" => "Chưa thanh toán",
                    "cancelled" => "Đã hủy",
                    _ => inv.Status
                }
            }).ToList();

            return Ok(new
            {
                success = true,
                data = new
                {
                    page,
                    limit,
                    totalItems,
                    totalPages = (int)Math.Ceiling(totalItems / (double)limit),
                    items
                }
            });
        }

        /* ===================== CHI TIẾT HÓA ĐƠN ===================== */
        [HttpGet("invoices/{invoiceId:long}")]
        [Produces("application/json")]
        [SwaggerOperation(
            Summary = "Chi tiết hoá đơn",
            Description = "Phục vụ màn hình Chi tiết / In hoá đơn",
            Tags = new[] { "Billing" }
        )]
        public async Task<IActionResult> GetInvoiceDetail(
            [FromRoute] long invoiceId,
            CancellationToken ct = default)
        {
            var inv = await _db.Invoices
                .AsNoTracking()
                .Include(i => i.Items)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

            if (inv == null)
                return NotFound(new { success = false, message = "Không tìm thấy hoá đơn." });

            var latestPayment = inv.Payments
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault();

            var dto = new InvoiceDetailDto
            {
                InvoiceId = inv.InvoiceId,
                InvoiceCode = inv.Code,
                CreatedAtText = inv.CreatedAt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy"),
                StatusLabel = inv.Status switch
                {
                    "paid" => "Đã thanh toán",
                    "partial" => "Thanh toán một phần",
                    "unpaid" => "Chưa thanh toán",
                    "cancelled" => "Đã hủy",
                    _ => inv.Status
                },
                Lines = inv.Items
                    .OrderBy(i => i.InvoiceItemId)
                    .Select((it, idx) => new InvoiceLineDto
                    {
                        LineNo = idx + 1,
                        ItemName = it.Description,
                        ItemType = it.ItemType,       // "visit","service","lab","medicine","other"
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        LineTotal = it.Quantity * it.UnitPrice
                    })
                    .ToList(),
                PatientInfo = new PatientInfoDto
                {
                    FullName = inv.PatientName,
                    CaseCode = inv.PatientCode ?? "",
                    DateOfBirth = inv.PatientDob.HasValue
                        ? inv.PatientDob.Value.ToString("dd/MM/yyyy")
                        : "",
                    Gender = inv.PatientGender ?? "",
                    Email = inv.PatientEmail ?? "",
                    Phone = inv.PatientPhone ?? "",
                    Note = string.IsNullOrWhiteSpace(inv.PatientNote) ? "-" : inv.PatientNote!
                },
                DoctorInfo = new DoctorInfoDto
                {
                    FullName = inv.DoctorName ?? "",
                    SpecialtyName = inv.DoctorSpecialty ?? "",
                    ClinicName = inv.ClinicName ?? "",
                    Email = inv.DoctorEmail ?? "",
                    Phone = inv.DoctorPhone ?? ""
                },
                PaymentInfo = new PaymentInfoDto
                {
                    Subtotal = inv.Subtotal,
                    Discount = inv.DiscountAmt,
                    Tax = inv.TaxAmt,
                    TotalAmount = inv.TotalAmount,
                    PaidAmount = inv.PaidAmount,
                    RemainingAmount = inv.TotalAmount - inv.PaidAmount,
                    Method = latestPayment?.Method,
                    PaidAtText = latestPayment != null
                        ? latestPayment.PaidAt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy")
                        : null
                }
            };

            return Ok(new
            {
                success = true,
                data = dto
            });
        }

        /* ===================== GHI NHẬN THANH TOÁN ===================== */
        [HttpPost("pay")]
        [Produces("application/json")]
        [SwaggerOperation(
            Summary = "Thanh toán hoá đơn",
            Description = "Thêm bản ghi Payment và cập nhật Invoice.PaidAmount / Status",
            Tags = new[] { "Billing" }
        )]
        public async Task<IActionResult> RecordPayment(
            [FromBody] RecordPaymentRequest req,
            CancellationToken ct = default)
        {
            if (req.Amount <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Số tiền phải > 0."
                });
            }

            var inv = await _db.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId, ct);

            if (inv == null)
                return NotFound(new { success = false, message = "Không tìm thấy hoá đơn." });

            // Tạo payment
            var now = DateTime.UtcNow;
            var pay = new Payment
            {
                InvoiceId = inv.InvoiceId,
                Amount = req.Amount,
                Method = req.Method,
                RefNumber = string.IsNullOrWhiteSpace(req.RefNumber) ? null : req.RefNumber,
                PaidAt = now,
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note,
                CreatedAt = now
            };

            _db.Payments.Add(pay);

            // Cập nhật PaidAmount
            inv.PaidAmount += req.Amount;
            inv.UpdatedAt = now;

            // Tính status mới
            if (inv.PaidAmount >= inv.TotalAmount)
            {
                inv.Status = "paid";
            }
            else if (inv.PaidAmount > 0)
            {
                inv.Status = "partial";
            }
            else
            {
                inv.Status = "unpaid";
            }

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Thanh toán thành công.",
                data = new
                {
                    invoiceId = inv.InvoiceId,
                    code = inv.Code,
                    status = inv.Status,
                    paidAmount = inv.PaidAmount,
                    remaining = inv.TotalAmount - inv.PaidAmount
                }
            });
        }
    }
}
