using System.ComponentModel.DataAnnotations;
using FoMed.Api.Models;

public class InventoryTransaction
{
    [Key]
    public long InvTxnId { get; set; }

    public int MedicineId { get; set; }
    public long? LotId { get; set; }
    public string TxnType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? RefNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Medicine Medicine { get; set; } = null!;
    public MedicineLot? Lot { get; set; }          // navigation nullable
}
