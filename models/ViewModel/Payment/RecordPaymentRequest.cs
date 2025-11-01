namespace FoMed.Api.Dtos.Billing
{
    public class RecordPaymentRequest
    {
        public long InvoiceId { get; set; }
        public decimal Amount { get; set; }      
        public string Method { get; set; } = ""; // "cash" / "card" / "bank" / "ewallet"
        public string? RefNumber { get; set; }   // mã giao dịch
        public string? Note { get; set; }        
    }
}
