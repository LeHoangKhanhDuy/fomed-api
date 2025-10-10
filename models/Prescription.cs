using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public class EncounterPrescription
{
    [Key]
    public long PrescriptionId { get; set; }
    public long EncounterId { get; set; }
    public string? Code { get; set; }
    public string? Advice { get; set; }
    public string? ErxCode { get; set; }       
    public string? ErxStatus { get; set; }     
    public DateTime? ExpiryAt { get; set; }
    public string? Warning { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Encounter Encounter { get; set; } = default!;
    public virtual ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}

public class PrescriptionItem
{
    [Key]
    public long ItemId { get; set; }
    public long PrescriptionId { get; set; }
    public int? MedicineId { get; set; }
    public string? CustomName { get; set; }
    public string? DoseText { get; set; }
    public string? FrequencyText { get; set; }
    public string? DurationText { get; set; }
    public string? Note { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual EncounterPrescription Prescription { get; set; } = default!;
    public virtual Medicine? Medicine { get; set; }
    public virtual ICollection<DispenseLine> DispenseLines { get; set; } = new List<DispenseLine>();
}

