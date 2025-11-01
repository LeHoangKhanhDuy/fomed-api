namespace FoMed.Api.Dtos.Billing
{
    public class PendingBillingRowDto
    {
        public long InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = ""; 
        public string CaseCode { get; set; } = "";  
        public string PatientName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string ServiceName { get; set; } = "-";
        public string FinishedTime { get; set; } = ""; 
        public string FinishedDate { get; set; } = "";
        public decimal TotalAmount { get; set; }     
    }
}
