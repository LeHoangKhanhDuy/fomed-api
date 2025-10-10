using System.Text.Json.Serialization;

namespace FoMed.Api.Features.Doctor.TodayPatients;

// Enum hiển thị dạng chuỗi trên Swagger/JSON
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppointmentStatus
{
    waiting,
    booked,
    done,
    cancelled,
    no_show
}

// Query: Status = null => trả tất cả
public sealed class TodayPatientsQuery
{
    public AppointmentStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public sealed class TodayPatientItemDto
{
    public long AppointmentId { get; set; }
    public string? Code { get; set; }
    public long PatientId { get; set; }
    public string PatientName { get; set; } = "";
    public string? Phone { get; set; }

    public DateOnly VisitDate { get; set; }
    public TimeOnly VisitTime { get; set; }
    public string TimeText { get; set; } = ""; // "HH:mm"

    public string Status { get; set; } = "";
    public int? QueueNo { get; set; }

    public int? ServiceId { get; set; }
    public string? ServiceName { get; set; }
}

// Kết quả phân trang KHỚP với controller (Total, PageSize)
public sealed class PatientTodayPagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}
