using FoMed.Api.Auth;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/v1/")]

public class AuthenticationController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly FoMedContext _db;

    public AuthenticationController(FoMedContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("access-token")]
    [AllowAnonymous]
    [Produces("application/json")]
    [Consumes("application/json")]
    [SwaggerOperation(
    Summary = "Generate Developer Access Token",
    Description = "Chỉ ADMIN mới được cấp access token",
    Tags = new[] { "Authentication Client" })]
    public async Task<IActionResult> GenerateAccessToken([FromBody] AccessTokenRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { status = false, status_code = 400, message = "Dữ liệu không hợp lệ" });

        // Tìm user theo email
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.IsActive && u.Email == req.Email);

        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
            return Unauthorized(new { status = false, status_code = 401, message = "Sai tài khoản hoặc mật khẩu" });

        var roles = user.UserRoles.Select(r => r.Role.Code).ToArray();
        if (!roles.Contains("ADMIN"))
            return Unauthorized(new { status = false, status_code = 401, message = "Bạn không có quyền truy cập!" });

        // Tạo token bằng TokenService
        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var expiresAt = _tokenService.GetAccessTokenExpiry();

        return Ok(new
        {
            status = true,
            status_code = 200,
            message = "Token Generated Successfully",
            data = new
            {
                token = accessToken,
                expiresAt
            }
        });
    }
}