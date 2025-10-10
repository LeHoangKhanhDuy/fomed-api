using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public sealed class Medicine
{
    [Key]
    public int MedicineId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string Unit { get; set; } = null!;
    public string? Note { get; set; }
    public decimal BasePrice { get; set; }     // decimal(18,2)
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<MedicineLot> Lots { get; set; } = new List<MedicineLot>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}

