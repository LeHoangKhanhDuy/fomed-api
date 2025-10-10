using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public sealed class MedicineLot
{
    [Key]
    public long LotId { get; set; }
    public int MedicineId { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal? PurchasePrice { get; set; }   // decimal(18,2)
    public decimal Quantity { get; set; }         // decimal(18,3)

    public DateTime CreatedAt { get; set; }

    public Medicine Medicine { get; set; } = null!;
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<DispenseLine> DispenseLines { get; set; } = new List<DispenseLine>();
}
