namespace FoMed.Api.ViewModel;

public sealed class ApiResponse<T>
{
    public bool Status { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = "";
    public T? Data { get; init; }

    public static ApiResponse<T> Success(T data, string msg = "Thành công", int code = 200)
        => new() { Status = true, StatusCode = code, Message = msg, Data = data };

    public static ApiResponse<T> Fail(string msg, int code = 400)
        => new() { Status = false, StatusCode = code, Message = msg, Data = default };
}
