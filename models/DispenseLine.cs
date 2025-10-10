using System.ComponentModel.DataAnnotations;

namespace FoMed.Api.Models;

public sealed class DispenseLine
{
    [Key]
    public long DispenseId { get; set; }
    public long PrescriptionItemId { get; set; }
    public long? LotId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public DateTime CreatedAt { get; set; }

    public PrescriptionItem PrescriptionItem { get; set; } = null!;
    public MedicineLot? Lot { get; set; }
}
