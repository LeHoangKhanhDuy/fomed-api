using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class LabTest
{   
    [Key]
    public int LabTestId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class EncounterLabTest
{
    [Key]
    public long EncLabTestId { get; set; }
    public long EncounterId { get; set; }
    public int? LabTestId { get; set; }
    public string? CustomName { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "ordered";
    public DateTime CreatedAt { get; set; }

    public virtual Encounter Encounter { get; set; } = default!;
    public virtual LabTest? LabTest { get; set; }
    public virtual ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();
}

public class LabResult
{
    [Key]
    public long LabResultId { get; set; }
    public long EncLabTestId { get; set; }
    public string? ResultJson { get; set; }
    public string? ResultNote { get; set; }
    public DateTime ResultAt { get; set; }
    public string ResultStatus { get; set; } = "normal";
    public string? FileUrl { get; set; }

    public virtual EncounterLabTest EncLabTest { get; set; } = default!;
}
