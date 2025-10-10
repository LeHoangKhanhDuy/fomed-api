public sealed class PrescriptionListItemDto
{
    public string Code { get; init; } = string.Empty; // DTFM-5534 (nếu DB chưa có Code thì dùng "DTFM-{PrescriptionId}")
    public DateTime PrescribedAt { get; init; }       // Ngày kê
    public string DoctorName { get; init; } = string.Empty;
    public string? Diagnosis { get; init; }
}

public sealed class PrescriptionDetailItemDto
{
    public string MedicineName { get; init; } = string.Empty;
    public string? Strength { get; init; }
    public string? Form { get; init; }
    public string? Dose { get; init; }
    public string? Duration { get; init; }
    public decimal Quantity { get; init; }
    public string? Instruction { get; init; }
}

public sealed class PrescriptionDetailDto
{
    public string Code { get; init; } = string.Empty;
    public DateTime PrescribedAt { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string Diagnosis { get; init; } = string.Empty;

    public List<PrescriptionDetailItemDto> Items { get; init; } = new();
    public string? Advice { get; init; }
    public string? Warning { get; init; }
}
