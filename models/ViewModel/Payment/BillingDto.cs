namespace FoMed.Api.Dtos.Billing
{
    public class InvoiceDetailDto
    {
        public long InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = "";        
        public string CreatedAtText { get; set; } = "";       
        public string StatusLabel { get; set; } = "";       

        public List<InvoiceLineDto> Items { get; set; } = new();

        public PatientInfoDto PatientInfo { get; set; } = new();
        public DoctorInfoDto DoctorInfo { get; set; } = new();
        public PaymentInfoDto PaymentInfo { get; set; } = new();
    }

    public class InvoiceLineDto
    {
        public int LineNo { get; set; }
        public string ItemName { get; set; } = ""; 
        public string ItemType { get; set; } = ""; 
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class PatientInfoDto
    {
        public string FullName { get; set; } = "";
        public string CaseCode { get; set; } = ""; 
        public string DateOfBirth { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Note { get; set; } = "-";
    }

    public class DoctorInfoDto
    {
        public string FullName { get; set; } = "";
        public string SpecialtyName { get; set; } = "";
        public string ClinicName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }


    public class PaymentInfoDto
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }

        public string? Method { get; set; }      
        public string? PaidAtText { get; set; }   
    }
}
