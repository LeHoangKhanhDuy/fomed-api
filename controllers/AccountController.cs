using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using FoMed.Api.Models;
using FoMed.Api.ViewModel;
using FoMed.Api.Auth;
using FoMed.Api.ViewModels.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;


[ApiController]
[Route("api/v1/accounts")]
public class AccountsController : ControllerBase
{
    private readonly FoMedContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(FoMedContext db, IConfiguration cfg, ILogger<AccountsController> logger)
    {
        _db = db;
        _cfg = cfg;
        _logger = logger;
    }

    // ===== Helpers =====
    private static string[] NormalizeRoles(IEnumerable<string> roles) =>
        roles.Where(r => !string.IsNullOrWhiteSpace(r))
             .Select(r => r.Trim().ToUpperInvariant())
             .Distinct()
             .ToArray();

    // Tạo các claim phụ theo role: doctor_id / patient_id ...
    private async Task<List<Claim>> BuildExtraClaimsAsync(long userId, string[] roleCodes)
    {
        var extra = new List<Claim>();

        foreach (var rc in roleCodes)
        {
            switch (rc)
            {
                case "DOCTOR":
                    {
                        // Nếu Doctors.UserId là BIGINT -> giữ long; nếu là INT -> ép int
                        int? doctorId = null;

                        // thử map kiểu long trước
                        doctorId = await _db.Doctors
                            .Where(d => d.UserId == userId)            // nếu cột là bigint
                            .Select(d => (int?)d.DoctorId)
                            .FirstOrDefaultAsync();

                        // nếu không có, thử cast xuống int (khi Doctors.UserId là INT)
                        if (!doctorId.HasValue && userId >= int.MinValue && userId <= int.MaxValue)
                        {
                            int uidInt = (int)userId;
                            doctorId = await _db.Doctors
                                .Where(d => d.UserId == uidInt)         // nếu cột là int
                                .Select(d => (int?)d.DoctorId)
                                .FirstOrDefaultAsync();
                        }

                        if (doctorId.HasValue)
                            extra.Add(new Claim("doctor_id", doctorId.Value.ToString()));
                        break;
                    }

                case "PATIENT":
                    {
                        // PatientId có thể là bigint/long — đổi qua string
                        long? patientId = await _db.Patients
                            .Where(p => p.UserId == userId)             // nếu p.UserId là bigint
                            .Select(p => (long?)p.PatientId)
                            .FirstOrDefaultAsync();

                        if (!patientId.HasValue && userId >= int.MinValue && userId <= int.MaxValue)
                        {
                            int uidInt = (int)userId;
                            patientId = await _db.Patients
                                .Where(p => p.UserId == uidInt)         // nếu p.UserId là int
                                .Select(p => (long?)p.PatientId)
                                .FirstOrDefaultAsync();
                        }

                        if (patientId.HasValue)
                            extra.Add(new Claim("patient_id", patientId.Value.ToString()));
                        break;
                    }

                    // case "EMPLOYEE":
                    //   BẬT LẠI khi có DbSet<Employees> trong FoMedContext
            }
        }

        return extra;
    }


    // Tạo claims phụ theo role: doctor_id / employee_id / patient_id ...
    private async Task<List<Claim>> BuildExtraClaimsAsync(int userId, string[] roleCodes)
    {
        var extra = new List<Claim>();

        foreach (var rc in roleCodes)
        {
            switch (rc)
            {
                case "DOCTOR":
                    {
                        var doctorId = await _db.Doctors
                            .Where(d => d.UserId == userId)
                            .Select(d => (int?)d.DoctorId)
                            .FirstOrDefaultAsync();
                        if (doctorId.HasValue)
                            extra.Add(new Claim("doctor_id", doctorId.Value.ToString()));
                        break;
                    }
                case "EMPLOYEE":
                    {
                        var employeeId = await _db.Employees
                            .Where(e => e.UserId == userId)
                            .Select(e => (int?)e.EmployeeId)
                            .FirstOrDefaultAsync();
                        if (employeeId.HasValue)
                            extra.Add(new Claim("employee_id", employeeId.Value.ToString()));
                        break;
                    }
                case "PATIENT":
                    {
                        var patientId = await _db.Patients
                            .Where(p => p.UserId == userId)
                            .Select(p => (long?)p.PatientId)
                            .FirstOrDefaultAsync();
                        if (patientId.HasValue)
                            extra.Add(new Claim("patient_id", patientId.Value.ToString()));
                        break;
                    }
                default:
                    break;
            }
        }
        return extra;
    }

    private (string token, DateTime expiresAt) GenerateJwt(User user, string[] roleCodes, IEnumerable<Claim>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:AccessMinutes"]!));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName ?? string.Empty),
        };

        foreach (var rc in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, rc));
            claims.Add(new Claim("role", rc));
        }

        if (extraClaims is not null)
            claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }


    // ===== LOGIN =====
    [HttpPost("login-with-email")]
    [SwaggerOperation(
    Summary = "Login to account",
    Description = "Đăng nhập bằng email",
    Tags = new[] { "Accounts" })]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginWithEmailRequest req)
    {
        // (1) Validate input đơn giản (nếu bạn có [Required] trên DTO thì có thể bỏ)
        if (string.IsNullOrWhiteSpace(req?.Email) || string.IsNullOrWhiteSpace(req?.Password))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Dữ liệu không hợp lệ.", 400));

        var email = req.Email.Trim().ToLowerInvariant();
        var password = req.Password;

        // (2) Tải user + roles
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        // Không tiết lộ là email hay pass sai
        if (user is null || !user.IsActive)
        {
            // Nếu user tồn tại nhưng IsActive = false → 403
            if (user is not null && !user.IsActive)
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<LoginTokenResponse>.Fail("Tài khoản đã bị vô hiệu hóa.", 403));

            return Unauthorized(ApiResponse<LoginTokenResponse>.Fail("Email hoặc mật khẩu không đúng.", 401));
        }

        // (3) Kiểm tra khoá tài khoản (tùy schema bạn có IsLocked / LockedUntil)
        // Giả định 2 field phổ biến:
        //   - bool IsLocked
        //   - DateTime? LockedUntil (UTC)
        var isLocked = (bool)(user.GetType().GetProperty("IsLocked")?.GetValue(user) ?? false);
        var lockedUntil = (DateTime?)user.GetType().GetProperty("LockedUntil")?.GetValue(user);

        if (isLocked || (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow))
        {
            var msg = lockedUntil.HasValue
                ? $"Tài khoản đang bị khoá đến {lockedUntil.Value:HH:mm dd/MM/yyyy}."
                : "Tài khoản đang bị khoá.";
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<LoginTokenResponse>.Fail(msg, 423));
        }

        // (4) Xác thực mật khẩu
        var ok = PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt);
        if (!ok)
            return Unauthorized(ApiResponse<LoginTokenResponse>.Fail("Email hoặc mật khẩu không đúng.", 401));

        // (5) Tạo token + refresh token
        var roleCodes = NormalizeRoles(user.UserRoles.Select(r => r.Role.Code));
        var extraClaims = await BuildExtraClaimsAsync(user.UserId, roleCodes);
        var (accessToken, expiresAt) = GenerateJwt(user, roleCodes, extraClaims);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshDays = int.Parse(_cfg["Jwt:RefreshDays"]!);

        _db.UserSessions.Add(new UserSession
        {
            UserId = user.UserId,
            RefreshToken = refresh,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });

        await _db.SaveChangesAsync();

        var data = new LoginTokenResponse
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = refresh,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Roles = roleCodes
        };

        return Ok(ApiResponse<LoginTokenResponse>.Success(data, "Đăng nhập thành công", 200));
    }

    // ===== REFRESH TOKEN =====
    [HttpPost("refresh")]
    [SwaggerOperation(Description = "Làm mới token", Tags = new[] { "Accounts" })]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken)
    {
        var session = await _db.UserSessions
            .Include(s => s.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

        if (session is null || session.RevokedAt != null || session.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(ApiResponse<LoginTokenResponse>.Fail("Invalid refresh token.", 401));

        var user = session.User;
        var roleCodes = NormalizeRoles(user.UserRoles.Select(r => r.Role.Code));
        var extraClaims = await BuildExtraClaimsAsync(user.UserId, roleCodes);
        var (access, expiresAt) = GenerateJwt(user, roleCodes, extraClaims);

        // rotate refresh token
        session.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        session.IssuedAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_cfg["Jwt:RefreshDays"]!));
        await _db.SaveChangesAsync();

        var data = new LoginTokenResponse
        {
            Token = access,
            ExpiresAt = expiresAt,
            RefreshToken = session.RefreshToken,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Roles = roleCodes
        };

        return Ok(ApiResponse<LoginTokenResponse>.Success(data, "Refresh thành công"));
    }

    // ===== REGISTER WITH EMAIL =====
    [HttpPost("register-with-email")]
    [SwaggerOperation(
    Summary = "Register to account",
    Description = "Đăng ký tài khoản bằng email (hoặc phone)",
    Tags = new[] { "Accounts" })]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LoginTokenResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterWithEmail([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail(
                string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)), 400));
        // --- Validate cơ bản ---
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Vui lòng nhập họ tên.", 400));

        if (string.IsNullOrWhiteSpace(req.Email) && string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Vui lòng nhập Email hoặc Số điện thoại.", 400));

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Mật khẩu phải có ít nhất 6 ký tự.", 400));

        // Mật khẩu mạnh: 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt
        var strongPwd = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$");
        if (!strongPwd.IsMatch(req.Password))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail(
                "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt.", 400));

        // Chuẩn hoá + validate email/phone (nếu có)
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();

        if (email is not null && !new EmailAddressAttribute().IsValid(email))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Email không đúng định dạng.", 400));

        // (Đơn giản) phone 9-12 số
        if (phone is not null && !Regex.IsMatch(phone, @"^\d{9,12}$"))
            return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Số điện thoại không đúng định dạng.", 400));

        // --- Kiểm tra trùng ---
        if (email is not null && await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(ApiResponse<LoginTokenResponse>.Fail("Email này đã được sử dụng.", 409));

        if (phone is not null && await _db.Users.AnyAsync(u => u.Phone == phone))
            return Conflict(ApiResponse<LoginTokenResponse>.Fail("Số điện thoại này đã được sử dụng.", 409));

        // --- Băm mật khẩu ---
        var (hash, salt) = PasswordHasher.Hash(req.Password);

        // Parse ngày sinh dd/MM/yyyy
        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(req.DateOfBirth))
        {
            if (DateTime.TryParseExact(req.DateOfBirth, "dd/MM/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var dt))
            {
                dob = DateOnly.FromDateTime(dt);
            }
            else
            {
                return BadRequest(ApiResponse<LoginTokenResponse>.Fail("Ngày sinh phải có dạng dd/MM/yyyy.", 400));
            }
        }

        // Parse gender (chấp nhận 'M'/'F' hoặc "M"/"F")
        char? gender = null;
        if (req.Gender is not null)
        {
            var g = req.Gender.ToString()!.Trim().ToUpperInvariant();
            if (g == "M" || g == "F") gender = g[0];
        }

        // --- Tạo user (chưa lưu) ---
        var now = DateTime.UtcNow;
        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            Phone = phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,

            // tạo luôn profile vì quan hệ 1-1 là Required
            Profile = new UserProfile
            {
                DateOfBirth = dob,
                Gender = gender,
                AvatarUrl = null,
                Address = null,
                Bio = null,
                UpdatedAt = now
            }
        };

        // --- Gán role mặc định & lưu trong transaction ---
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // Seed role PATIENT nếu chưa có (lấy trong tx)
                var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "PATIENT");
                if (role is null)
                {
                    role = new Role { Code = "PATIENT", Name = "Bệnh nhân", IsActive = true, CreatedAt = DateTime.UtcNow };
                    _db.Roles.Add(role);
                    await _db.SaveChangesAsync();
                }

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = role.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<LoginTokenResponse>.Fail("Đăng ký thất bại, vui lòng thử lại.", 500));
        }


        // --- Tạo JWT + refresh token như login ---
        var roles = new[] { "PATIENT" };
        var (accessToken, expiresAt) = GenerateJwt(user, roles);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshDays = int.Parse(_cfg["Jwt:RefreshDays"]!);

        _db.UserSessions.Add(new UserSession
        {
            UserId = user.UserId,
            RefreshToken = refresh,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync();

        var data = new LoginTokenResponse
        {
            Token = accessToken,
            ExpiresAt = expiresAt,
            RefreshToken = refresh,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Roles = roles
        };

        return Ok(ApiResponse<LoginTokenResponse>.Success(data, "Đăng ký thành công"));
    }


    // ===== LOGOUT WITH REFRESH TOKEN =====
    [HttpPost("logout")]
    [SwaggerOperation(
    Summary = "Logout to account",
    Description = "Đăng xuất tài khoản bằng refreshToken",
    Tags = new[] { "Accounts" })]

    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.RefreshToken == request.RefreshToken);

        if (session is null || session.RevokedAt != null || session.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(ApiResponse<object>.Fail("Token không hợp lệ hoặc đã hết hạn.", 401));

        session.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Success(new { }, "Đăng xuất thành công"));
    }


    [HttpPost("profile")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Get user profile",
    Description = "Lấy user profile bằng cách gửi token",
    Tags = new[] { "Accounts" })]
    public async Task<IActionResult> GetProfile([FromBody] ProfileByTokenRequest req)
    {
        // 1) Lấy token: ưu tiên Authorization header, fallback body
        string? token = null;

        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = authHeader.Substring("Bearer ".Length).Trim();

        if (string.IsNullOrWhiteSpace(token))
            token = req.Token;

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Thiếu token." });

        // 2) Validate token (đồng nhất với JwtBearer)
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = _cfg["Jwt:Issuer"],
            ValidAudience = _cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(2) // nới 2 phút để tránh lệch giờ nhỏ
        };

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(token, tvp, out _);
        }
        catch (Exception)
        {
            return Unauthorized(new { error = "Token không hợp lệ hoặc đã hết hạn." });
        }

        // 3) Lấy userId từ claims
        var idStr = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("uid");
        if (!long.TryParse(idStr, out var userId))
            return Unauthorized(new { error = "Token không chứa user id." });

        // 4) Truy vấn dữ liệu
        var data = await _db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .Where(u => u.UserId == userId && u.IsActive)
            .Select(u => new
            {
                id = u.UserId,
                name = u.FullName,
                email = u.Email,
                phone = u.Phone,
                gender = u.Profile!.Gender == null ? null : (u.Profile.Gender == 'M' ? "Male" : "Female"),
                dateOfBirth = u.Profile!.DateOfBirth.HasValue
                    ? (DateTime?)u.Profile.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                    : (DateTime?)null,
                createdAt = u.CreatedAt,
                avatarUrl = u.Profile!.AvatarUrl,
                address = u.Profile!.Address,
                bio = u.Profile!.Bio,
                profileUpdatedAt = (DateTime?)u.Profile!.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (data == null)
            return NotFound(new { error = "Không tìm thấy người dùng" });

        return Ok(new { message = "Get account profile successfully", data });
    }

    // ===== UPDATE PROFILE =====
    [HttpPost("update-profile")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
    Summary = "Update profile",
    Description = "Chỉnh sửa hồ sơ cá nhân",
    Tags = new[] { "Accounts" })]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileByTokenRequest req)
    {
        // 1) Validate input
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { success = false, message = "Thiếu token." });

        // 2) Validate token
        var tvp = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _cfg["Jwt:Issuer"],
            ValidAudience = _cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero
        };

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(req.Token, tvp, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Unauthorized(new { success = false, message = "Token không hợp lệ hoặc đã hết hạn." });
        }

        var idStr = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? principal.FindFirstValue("uid");
        if (!long.TryParse(idStr, out var userId))
        {
            _logger.LogWarning("Cannot parse userId from token. IdStr: {IdStr}", idStr);
            return Unauthorized(new { success = false, message = "Token không chứa thông tin người dùng." });
        }

        // 3) Lấy user + profile
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("User not found. UserId: {UserId}", userId);
            return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
        }

        // 4) Validate phone nếu có thay đổi
        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            var phoneToCheck = req.Phone.Trim();

            // Chỉ check trùng nếu phone khác với phone hiện tại
            if (user.Phone != phoneToCheck)
            {
                var existed = await _db.Users
                    .AnyAsync(x => x.Phone == phoneToCheck && x.UserId != userId);

                if (existed)
                {
                    _logger.LogWarning("Phone already exists. Phone: {Phone}, UserId: {UserId}", phoneToCheck, userId);
                    ModelState.AddModelError(nameof(req.Phone), "Số điện thoại đã được sử dụng.");
                    return ValidationProblem(ModelState);
                }
            }
        }

        // 5) Cập nhật Users + UserProfiles
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Update User fields
            user.FullName = req.Name.Trim();

            // Chỉ update Phone nếu có giá trị mới
            if (!string.IsNullOrWhiteSpace(req.Phone))
            {
                user.Phone = req.Phone.Trim();
            }

            // Update hoặc tạo mới UserProfile
            if (user.Profile == null)
            {
                _logger.LogInformation("Creating new UserProfile for UserId: {UserId}", userId);

                user.Profile = new UserProfile
                {
                    UserId = user.UserId,
                    AvatarUrl = string.IsNullOrWhiteSpace(req.AvatarUrl) ? null : req.AvatarUrl.Trim(),
                    Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim(),
                    Bio = string.IsNullOrWhiteSpace(req.Bio) ? null : req.Bio.Trim(),
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserProfiles.Add(user.Profile);
            }
            else
            {
                // CHỈ update các field có giá trị mới, không overwrite với null/empty
                if (!string.IsNullOrWhiteSpace(req.AvatarUrl))
                {
                    user.Profile.AvatarUrl = req.AvatarUrl.Trim();
                }

                if (!string.IsNullOrWhiteSpace(req.Address))
                {
                    user.Profile.Address = req.Address.Trim();
                }

                if (!string.IsNullOrWhiteSpace(req.Bio))
                {
                    user.Profile.Bio = req.Bio.Trim();
                }

                user.Profile.UpdatedAt = DateTime.UtcNow;
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Profile updated successfully for UserId: {UserId}", userId);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();

            // LOG CHI TIẾT LỖI - đây là điểm quan trọng!
            _logger.LogError(ex, "Error updating profile for UserId: {UserId}. Error: {ErrorMessage}",
                userId, ex.Message);

            // Kiểm tra loại exception để trả về message phù hợp
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {InnerException}", ex.InnerException.Message);
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                new
                {
                    success = false,
                    message = "Có lỗi máy chủ khi cập nhật hồ sơ.",
                    // CHỈ DÙNG TRONG DEV - xóa dòng dưới trong production
                    // error = ex.Message 
                });
        }

        return Ok(new
        {
            success = true,
            message = "Cập nhật hồ sơ thành công",
            data = new
            {
                id = user.UserId,
                name = user.FullName,
                email = user.Email,
                phone = user.Phone,
                avatarUrl = user.Profile?.AvatarUrl,
                address = user.Profile?.Address,
                bio = user.Profile?.Bio,
                updatedAt = user.Profile?.UpdatedAt
            }
        });
    }


    // ===== UPLOAD AVATAR =====
    [HttpPost("avatar")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
    Summary = "Upload avatar",
    Description = "Tải lên ảnh đại diện",
    Tags = new[] { "Accounts" })]
    [RequestSizeLimit(5_000_000)] // 5MB
    public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadRequest req)
    {
        if (req.File == null || req.File.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn ảnh." });

        string? idStr =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
            User.FindFirst("uid")?.Value ??
            User.FindFirst("userId")?.Value;

        long userId = 0;

        if (!long.TryParse(idStr, out userId))
        {
            // Fallback: tra theo email
            var email =
                User.FindFirst(ClaimTypes.Email)?.Value ??
                User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ??
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "Unauthorized" });

            var userIdQ = await _db.Users
                .Where(u => u.Email == email)
                .Select(u => u.UserId)
                .FirstOrDefaultAsync();

            if (userIdQ <= 0)
                return Unauthorized(new { message = "Unauthorized" });

            userId = userIdQ;
        }

        // ========= Validate file =========
        const long MaxBytes = 5_000_000;
        if (req.File.Length > MaxBytes)
            return BadRequest(new { message = "Ảnh vượt quá dung lượng tối đa 5MB." });

        var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (!allowedMime.Contains(req.File.ContentType))
            return BadRequest(new { message = "Chỉ hỗ trợ JPG/PNG/WebP." });

        var ext = Path.GetExtension(req.File.FileName);
        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExt.Contains(ext))
            return BadRequest(new { message = "Định dạng tệp không hợp lệ." });

        // ========= Save =========
        var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(root);
        var fileName = $"{userId}_{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absPath = Path.Combine(root, fileName);

        using (var stream = System.IO.File.Create(absPath))
            await req.File.CopyToAsync(stream);

        var publicUrl = $"/uploads/avatars/{fileName}";
        var fullUrl = $"{Request.Scheme}://{Request.Host}{publicUrl}";

        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        var oldUrl = user.Profile?.AvatarUrl;

        if (user.Profile == null)
            user.Profile = new UserProfile { UserId = userId, AvatarUrl = publicUrl, UpdatedAt = DateTime.UtcNow };
        else
        {
            user.Profile.AvatarUrl = publicUrl;
            user.Profile.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        TryDeleteOldAvatar(oldUrl);

        return Ok(new { message = "Tải ảnh thành công", data = new { avatarUrl = fullUrl } });
    }

    private void TryDeleteOldAvatar(string? oldUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(oldUrl)) return;
            var rel = oldUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(oldUrl).AbsolutePath : oldUrl;
            if (!rel.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase)) return;
            var abs = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rel.TrimStart('/'));
            if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
        }
        catch { }
    }


    // ====== FORGOT PASSWORD (GỬI TOKEN QUA EMAIL) ======
    /* ===== Helpers riêng cho Forgot/Reset ===== */
    private static byte[] ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(input));
    }
    public sealed class ForgotPasswordRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Quên mật khẩu",
        Description = "Tạo token đặt lại mật khẩu và (thường là) gửi qua email.",
        Tags = new[] { "Accounts" })]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Email không hợp lệ.", 400));

        var email = req.Email.Trim().ToLowerInvariant();

        // Luôn trả về 200 để tránh đoán email (user enumeration)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.IsActive && u.Email == email);

        if (user != null)
        {
            // Tạo reset token (chuỗi ngẫu nhiên) + hash lưu DB
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var tokenHash = ComputeSha256(rawToken);

            var minutes = int.TryParse(_cfg["Auth:ResetMinutes"], out var m) ? m : 15;

            _db.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.UserId,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutes),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // TODO: gửi email ở đây (rawToken)
            // VD: await _mailer.SendResetPassword(user.Email, rawToken);

            // Cho môi trường DEV, có thể trả token ra để test nhanh
            if (string.Equals(_cfg["Auth:ReturnResetTokenInResponse"], "true", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse<object>.Success(new
                {
                    email,
                    resetToken = rawToken,
                    expiresInMinutes = minutes
                }, "Nếu email tồn tại, token đặt lại đã được tạo."));
            }
        }

        return Ok(ApiResponse<object>.Success(new { }, "Nếu email tồn tại, token đặt lại đã được tạo."));
    }

    // ====== RESET PASSWORD (DÙNG TOKEN) ======
    public sealed class ResetPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;
        [Required] public string NewPassword { get; set; } = string.Empty;
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Đặt lại mật khẩu bằng token",
        Description = "Xác thực token, cập nhật mật khẩu và thu hồi các phiên đăng nhập cũ.",
        Tags = new[] { "Accounts" })]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", 400));

        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.IsActive && u.Email == email);
        if (user is null)
            return BadRequest(ApiResponse<object>.Fail("Token không hợp lệ hoặc đã hết hạn.", 400)); // tránh lộ email

        var tokenHash = ComputeSha256(req.Token);

        var record = await _db.EmailVerificationTokens
            .Where(t => t.UserId == user.UserId &&
                        t.UsedAt == null &&
                        t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (record is null || !record.TokenHash.SequenceEqual(tokenHash))
            return BadRequest(ApiResponse<object>.Fail("Token không hợp lệ hoặc đã hết hạn.", 400));

        // Validate độ mạnh mật khẩu (giống khi register)
        var strongPwd = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$");
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6 || !strongPwd.IsMatch(req.NewPassword))
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu mới không đạt yêu cầu.", 400));

        // Cập nhật mật khẩu + đánh dấu UsedAt + revoke các session hiện có
        var (hash, salt) = PasswordHasher.Hash(req.NewPassword);

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.UpdatedAt = DateTime.UtcNow;

            record.UsedAt = DateTime.UtcNow;

            // Thu hồi tất cả refresh token đang hiệu lực
            var activeSessions = await _db.UserSessions
                .Where(s => s.UserId == user.UserId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var s in activeSessions)
                s.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Không thể đặt lại mật khẩu, vui lòng thử lại.", 500));
        }

        return Ok(ApiResponse<object>.Success(new { }, "Đặt lại mật khẩu thành công."));
    }
}
