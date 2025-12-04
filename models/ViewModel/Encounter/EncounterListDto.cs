public sealed class EncounterListItemDto
{
    public string Code { get; init; } = string.Empty;   // HSFM-ABCDEF
    public DateTime VisitAt { get; init; }              // ngày khám
    public string DoctorName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string Status { get; init; } = "draft";      // draft|finalized|cancelled
    public decimal? TotalCost { get; init; }
}

public sealed class EncounterDetailDrugDto
{
    public string MedicineName { get; init; } = string.Empty;
    public string? Strength { get; init; }
    public string? Form { get; init; }
    public string? Dose { get; init; }                  // "1 viên x 3 lần/ngày"
    public string? Duration { get; init; }              // "5 ngày"
    public decimal Quantity { get; init; }
    public string? Instruction { get; init; }           // "Uống sau ăn…"
}

public sealed class EncounterDetailDto
{
    // Header
    public string EncounterCode { get; init; } = string.Empty; // HSFM-...
    public string PrescriptionCode { get; init; } = string.Empty; // DTFM-...
    public DateTime VisitAt { get; init; }
    public DateTime? ExpiryAt { get; init; }
    public string? ErxCode { get; init; }
    public string? ErxStatus { get; init; }
    public decimal? TotalCost { get; init; }

    // Bác sĩ
    public string DoctorName { get; init; } = string.Empty;
    public string? LicenseNo { get; init; }
    public string? ServiceName { get; init; }
    public string? SpecialtyName { get; init; }

    // Bệnh nhân
    public string PatientFullName { get; init; } = string.Empty;
    public string? PatientCode { get; init; }
    public DateOnly? PatientDob { get; init; }
    public string? PatientGender { get; init; }
    public string? PatientPhone { get; init; }
    public string? PatientEmail { get; init; }
    public string? PatientAddress { get; init; }
    public string? Diagnosis { get; init; }
    public string? Allergy { get; init; }

    // Thuốc
    public List<EncounterDetailDrugDto> Items { get; init; } = new();

    // Ghi chú
    public string? Advice { get; init; }
    public string? Warning { get; init; }
}
