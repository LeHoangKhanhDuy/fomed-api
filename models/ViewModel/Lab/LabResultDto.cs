public enum LabStatus
{
    Normal,        // Bình thường
    Abnormal,      // Bất thường
    Processing,    // Đang xử lý
    Pending,       // Chờ xử lý
    Canceled       // Đã hủy
}
public sealed class LabResultListItemDto
{
    public string Code { get; init; } = string.Empty;                 // LR-0001
    public DateTime SampleTakenAt { get; init; }                      // 2025-08-01T09:10:00Z
    public string ServiceName { get; init; } = string.Empty;          // Sinh hóa máu cơ bản
    public LabStatus Status { get; init; }                            // Normal/Processing/...
}

public sealed class LabResultItemRowDto
{
    public string TestName { get; init; } = string.Empty;             // Glucose
    public string ResultValue { get; init; } = string.Empty;          // 6.4 | "82"
    public string? Unit { get; init; }                                // mmol/L
    public string? ReferenceRange { get; init; }                      // "3.9 – 6.4" hoặc null
    public string? Note { get; init; }                                // "Hơi tăng"
    public bool IsHigh { get; init; }
    public bool IsLow { get; init; }
}

public sealed class LabResultDetailDto
{
    // Header phiếu
    public string Code { get; init; } = string.Empty;
    public DateTime SampleTakenAt { get; init; }
    public DateTime? ResultAt { get; init; }
    public string? SampleType { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string? OrderedDoctorName { get; init; }
    public LabStatus Status { get; init; }

    // Header bệnh nhân
    public string PatientCode { get; init; } = string.Empty;          // BN000567
    public string PatientFullName { get; init; } = string.Empty;      // Nguyễn Minh K
    public DateOnly? PatientDob { get; init; }
    public string? PatientGender { get; init; }

    // Bảng kết quả
    public List<LabResultItemRowDto> Items { get; init; } = new();

    // Ghi chú
    public string? Note { get; init; }           // Ghi chú
    public string? Warning { get; init; }        // Cảnh báo
}