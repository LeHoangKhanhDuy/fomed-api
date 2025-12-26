using FoMed.Api.Services;
using FoMed.Api.ViewModel;

namespace FoMed.Api.Cors;

public class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public JwtBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITokenBlacklistService blacklistService)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();

        // Tối ưu: Chỉ check cache nếu header có chứa Bearer token
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            if (await blacklistService.IsTokenRevokedAsync(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var response = ApiResponse<object>.Fail("Token đã bị vô hiệu hóa (Logged out).", 401);
                await context.Response.WriteAsJsonAsync(response); // Dùng System.Text.Json có sẵn
                return;
            }
        }

        await _next(context);
    }
}