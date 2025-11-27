// Features/Medicines/MedicineDtos.cs
using System.ComponentModel.DataAnnotations;

public class MedicineCreateRequest
{
    public string? Code { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(50)] public string? Strength { get; set; }
    [MaxLength(50)] public string? Form { get; set; }
    [Required, MaxLength(50)] public string Unit { get; set; } = string.Empty;
    [MaxLength(200)] public string? Note { get; set; }
    [Range(0, 1_000_000_000)] public decimal BasePrice { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class MedicineUpdateRequest : MedicineCreateRequest { }

public sealed class MedicineItemResponse
{
    public int MedicineId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = "";
    public string? Strength { get; set; }
    public string? Form { get; set; }
    public string Unit { get; set; } = "";
    public decimal BasePrice { get; set; }
    public decimal Stock { get; set; }          // tính từ sổ kho
    public bool IsActive { get; set; }
    public decimal PhysicalStock { get; set; }  
}

public sealed class MedicinePageResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

public class InvAdjustRequest
{
    public long? LotId { get; set; }
    public string TxnType { get; set; } = "adjust";   // 'in' | 'out' | 'adjust'
    public decimal Quantity { get; set; }             // >0 nhập, <0 xuất
    public decimal? UnitCost { get; set; }
    public string? RefNote { get; set; }
}
