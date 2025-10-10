using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class Invoice
{
    [Key]
    public long InvoiceId { get; set; }
    public string Code { get; set; } = default!;
    public long? EncounterId { get; set; }
    public long? AppointmentId { get; set; }
    public long? PatientId { get; set; }

    public string? PatientCode { get; set; }
    public string PatientName { get; set; } = default!;
    public DateOnly? PatientDob { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientEmail { get; set; }
    public string? PatientPhone { get; set; }
    public string? PatientNote { get; set; }

    public int? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public string? DoctorSpecialty { get; set; }
    public string? DoctorEmail { get; set; }
    public string? DoctorPhone { get; set; }
    public string? ClinicName { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmt { get; set; }
    public decimal TaxAmt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }   // cập nhật qua trigger/db
    public string Status { get; set; } = "unpaid";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Encounter? Encounter { get; set; }
    public virtual Appointment? Appointment { get; set; }
    public virtual Patient? Patient { get; set; }
    public virtual Doctor? Doctor { get; set; }
    public virtual ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem
{
    public long InvoiceItemId { get; set; }
    public long InvoiceId { get; set; }
    public string ItemType { get; set; } = default!;
    public string? RefType { get; set; }
    public long? RefId { get; set; }
    public string Description { get; set; } = default!;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;
    public DateTime CreatedAt { get; set; }

    public virtual Invoice Invoice { get; set; } = default!;
}

public class Payment
{
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = default!;
    public string? RefNumber { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Invoice Invoice { get; set; } = default!;
}
