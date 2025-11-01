namespace FoMed.Api.Dtos.Billing
{
    public class InvoiceListRowDto
    {
        public long InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = "";  
        public string PatientName { get; set; } = "";
        public string VisitDate { get; set; } = "";   
        public decimal PaidAmount { get; set; }        
        public decimal RemainingAmount { get; set; }
        public decimal TotalAmount { get; set; }         
        public string? LastPaymentMethod { get; set; }  
        public string StatusLabel { get; set; } = "";    
    }
}
