
using FoMed.Api.Models;

public class LabOrder
{
    public long LabOrderId { get; set; }
    public string Code { get; set; } = null!;
    public long PatientId { get; set; }
    public long EncounterId { get; set; }
    public int? DoctorId { get; set; }
    public int ServiceId { get; set; }
    public DateTime SampleTakenAt { get; set; }
    public DateTime? ResultAt { get; set; }
    public string? SampleType { get; set; }
    public LabStatus Status { get; set; } = LabStatus.Processing;
    public string? Note { get; set; }
    public string? Warning { get; set; }
    public DateTime CreatedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public Service Service { get; set; } = null!;
    public ICollection<LabOrderItem> Items { get; set; } = new List<LabOrderItem>();
}

// Models/LabOrderItem.cs
public class LabOrderItem
{
    public long LabOrderItemId { get; set; }
    public long LabOrderId { get; set; }
    public int DisplayOrder { get; set; }
    public string TestName { get; set; } = null!;
    public string ResultValue { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal? ReferenceMin { get; set; }
    public decimal? ReferenceMax { get; set; }
    public string? ReferenceText { get; set; }
    public string? Note { get; set; }
    public bool IsHigh { get; set; }
    public bool IsLow { get; set; }
    public DateTime CreatedAt { get; set; }

    public LabOrder LabOrder { get; set; } = null!;
}
