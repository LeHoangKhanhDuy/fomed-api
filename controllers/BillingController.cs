using FoMed.Api.Dtos.Billing;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;

namespace FoMed.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admin/billing")]
    [Authorize(Roles = "ADMIN,EMPLOYEE")]
    public class BillingController : ControllerBase
    {
        private static readonly Dictionary<string, string> StatusFilterMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "nháp", "draft" },
            { "draft", "draft" },
            { "đã thanh toán", "paid" },
            { "da thanh toan", "paid" },
            { "paid", "paid" },
            { "chưa thanh toán", "unpaid" },
            { "chua thanh toan", "unpaid" },
            { "unpaid", "unpaid" },
            { "hoàn tiền", "refunded" },
            { "hoan tien", "refunded" },
            { "refunded", "refunded" },
            { "hủy", "cancelled" },
            { "huỷ", "cancelled" },
            { "canceled", "cancelled" },
            { "cancelled", "cancelled" }
        };

        private readonly FoMedContext _db;

        public BillingController(FoMedContext db) => _db = db;

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
            var pendingRows = await _db.Invoices
                .AsNoTracking()
                .Where(inv => inv.Status != "paid" && inv.Status != "cancelled")
                .OrderByDescending(inv => inv.CreatedAt)
                .Select(inv => new PendingBillingRowDto
                {
                    InvoiceId = inv.InvoiceId,
                    InvoiceCode = inv.Code,
                    CaseCode = inv.PatientCode ?? string.Empty,
                    PatientName = inv.PatientName,
                    DoctorName = inv.DoctorName ?? string.Empty,
                    ServiceName = inv.Items
                        .OrderBy(i => i.InvoiceItemId)
                        .Select(i => i.Description)
                        .FirstOrDefault() ?? (inv.ClinicName ?? "-"),
                    FinishedTime = inv.CreatedAt.ToLocalTime().ToString("HH:mm"),
                    FinishedDate = inv.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                    TotalAmount = inv.TotalAmount
                })
                .ToListAsync(ct);

            return Ok(new { success = true, data = pendingRows });
        }

        /* ===================== DANH SÁCH HOÁ ĐƠN ===================== */
        [HttpGet("invoices")]
        [Authorize(Roles = "ADMIN,EMPLOYEE")]
        public async Task<IActionResult> ListInvoices(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery(Name = "q")] string? keyword = null,
            [FromQuery(Name = "status")] string? status = null,
            [FromQuery(Name = "from")] string? fromDate = null,
            [FromQuery(Name = "to")] string? toDate = null,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            var query = _db.Invoices.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var q = keyword.Trim().ToLowerInvariant();
                query = query.Where(inv =>
                    inv.Code.ToLower().Contains(q) ||
                    inv.PatientName.ToLower().Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = NormalizeStatusFilter(status);
                if (normalized == "unpaid")
                {
                    query = query.Where(inv => inv.Status == "unpaid" || inv.Status == "partial");
                }
                else if (!string.IsNullOrEmpty(normalized))
                {
                    query = query.Where(inv => inv.Status == normalized);
                }
            }

            if (TryParseDate(fromDate, out var from))
            {
                query = query.Where(inv => inv.CreatedAt >= from);
            }

            if (TryParseDate(toDate, out var to))
            {
                var inclusiveTo = to.Date.AddDays(1);
                query = query.Where(inv => inv.CreatedAt < inclusiveTo);
            }

            var totalItems = await query.CountAsync(ct);

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

            var items = rawRows.Select(inv => new InvoiceListRowDto
            {
                InvoiceId = inv.InvoiceId,
                InvoiceCode = inv.Code,
                PatientName = inv.PatientName,
                VisitDate = inv.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                PaidAmount = inv.PaidAmount,
                RemainingAmount = Math.Max(0, inv.TotalAmount - inv.PaidAmount),
                TotalAmount = inv.TotalAmount,
                LastPaymentMethod = inv.LastPaymentMethod,
                StatusLabel = MapStatusLabel(inv.Status)
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
            {
                return NotFound(new { success = false, message = "Không tìm thấy hoá đơn." });
            }

            var latestPayment = inv.Payments
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault();

            var dto = new InvoiceDetailDto
            {
                InvoiceId = inv.InvoiceId,
                InvoiceCode = inv.Code,
                CreatedAtText = inv.CreatedAt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy"),
                StatusLabel = MapStatusLabel(inv.Status),
                Items = inv.Items
                    .OrderBy(i => i.InvoiceItemId)
                    .Select((it, idx) => new InvoiceLineDto
                    {
                        LineNo = idx + 1,
                        ItemName = it.Description,
                        ItemType = it.ItemType,
                        Quantity = it.Quantity,
                        UnitPrice = it.UnitPrice,
                        LineTotal = it.Quantity * it.UnitPrice
                    })
                    .ToList(),
                PatientInfo = new PatientInfoDto
                {
                    FullName = inv.PatientName,
                    CaseCode = inv.PatientCode ?? string.Empty,
                    DateOfBirth = inv.PatientDob?.ToString("dd/MM/yyyy") ?? string.Empty,
                    Gender = inv.PatientGender ?? string.Empty,
                    Email = inv.PatientEmail ?? string.Empty,
                    Phone = inv.PatientPhone ?? string.Empty,
                    Note = string.IsNullOrWhiteSpace(inv.PatientNote) ? "-" : inv.PatientNote!
                },
                DoctorInfo = new DoctorInfoDto
                {
                    FullName = inv.DoctorName ?? string.Empty,
                    SpecialtyName = inv.DoctorSpecialty ?? string.Empty,
                    ClinicName = inv.ClinicName ?? string.Empty,
                    Email = inv.DoctorEmail ?? string.Empty,
                    Phone = inv.DoctorPhone ?? string.Empty
                },
                PaymentInfo = new PaymentInfoDto
                {
                    Subtotal = inv.Subtotal,
                    Discount = inv.DiscountAmt,
                    Tax = inv.TaxAmt,
                    TotalAmount = inv.TotalAmount,
                    PaidAmount = inv.PaidAmount,
                    RemainingAmount = Math.Max(0, inv.TotalAmount - inv.PaidAmount),
                    Method = latestPayment?.Method,
                    PaidAtText = latestPayment != null
                        ? latestPayment.PaidAt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy")
                        : null
                }
            };

            return Ok(new { success = true, data = dto });
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
                return BadRequest(new { success = false, message = "Số tiền phải > 0." });
            }

            var inv = await _db.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId, ct);

            if (inv == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy hoá đơn." });
            }

            var now = DateTime.UtcNow;
            var payment = new Payment
            {
                InvoiceId = inv.InvoiceId,
                Amount = req.Amount,
                Method = req.Method,
                RefNumber = string.IsNullOrWhiteSpace(req.RefNumber) ? null : req.RefNumber,
                PaidAt = now,
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note,
                CreatedAt = now
            };

            _db.Payments.Add(payment);

            inv.PaidAmount += req.Amount;
            inv.UpdatedAt = now;

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
                    status = MapStatusLabel(inv.Status),
                    paidAmount = inv.PaidAmount,
                    remaining = Math.Max(0, inv.TotalAmount - inv.PaidAmount)
                }
            });
        }

        private static string? NormalizeStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return StatusFilterMap.TryGetValue(status.Trim(), out var mapped) ? mapped : null;
        }

        private static bool TryParseDate(string? raw, out DateTime date)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                date = default;
                return false;
            }

            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
        }

        private static string MapStatusLabel(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "draft" => "Nháp",
                "paid" => "Đã thanh toán",
                "partial" => "Chưa thanh toán",
                "unpaid" => "Chưa thanh toán",
                "refunded" => "Hoàn tiền",
                "cancelled" or "canceled" => "Hủy",
                _ => status ?? "-"
            };
        }
    }
}
