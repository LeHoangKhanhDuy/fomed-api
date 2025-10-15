using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using FoMed.Api.Auth;
using FoMed.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// ========= 1) CONFIG =========
var cfg = builder.Configuration;

// Connection string
var cs = cfg.GetConnectionString("Default")
         ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

// Allowed origins
var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

// JWT config
var jwtIssuer = cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer");
var jwtAudience = cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience");
var jwtKeyB64 = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");

// Decode Base64 → bytes, yêu cầu >= 32 bytes (256-bit) cho HS256
byte[] jwtKeyBytes;
try
{
    jwtKeyBytes = Convert.FromBase64String(jwtKeyB64);
}
catch (FormatException)
{
    throw new InvalidOperationException(
        "Jwt:Key must be BASE64. Hãy tạo khóa 32 bytes và lưu ở dạng Base64.");
}
if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:Key quá ngắn. Cần tối thiểu 32 bytes (256-bit).");

// ========= 2) SERVICES =========

// EF Core + retry
builder.Services.AddDbContext<FoMedContext>(opt =>
    opt.UseSqlServer(cs, sql =>
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    )
);

// Controllers + JSON converters
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonDateOnlyConverter("dd/MM/yyyy"));
        o.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter("dd/MM/yyyy"));
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        o.JsonSerializerOptions.PropertyNamingPolicy = null; // giữ tên property như model
    });

// CORS
const string CorsPolicy = "AllowFE";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicy, p =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Không cấu hình -> mở tạm toàn bộ (không dùng cookie)
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            p.WithOrigins(allowedOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod();
            // Nếu dùng cookie: thêm .AllowCredentials() và KHÔNG được AllowAnyOrigin
        }
    });
});

// Swagger + Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FoMed API", Version = "v1" });
    c.EnableAnnotations();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập access token (KHÔNG kèm chữ 'Bearer ').",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
          Array.Empty<string>() }
    });
});

// AuthN/AuthZ (JWT)
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                // gom các claim "role"/"roles" về ClaimTypes.Role
                var id = (ClaimsIdentity)ctx.Principal!.Identity!;
                var extraRoles = id.FindAll("role").Concat(id.FindAll("roles"))
                                   .Select(r => r.Value).Distinct();
                foreach (var r in extraRoles)
                    id.AddClaim(new Claim(ClaimTypes.Role, r));
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                return ctx.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Bạn chưa đăng nhập hoặc token không hợp lệ."
                });
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                return ctx.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Bạn không có quyền truy cập."
                });
            }
        };
    });

builder.Services.AddAuthorization();

// ModelState gọn
builder.Services.Configure<ApiBehaviorOptions>(opt =>
{
    opt.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(k => k.Key, v => v.Value!.Errors.Select(e => e.ErrorMessage));
        return new BadRequestObjectResult(new { errors });
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// ========= 3) PIPELINE =========

// Chuẩn hóa header forward khi chạy sau ALB/API Gateway/Nginx
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto |
                       ForwardedHeaders.XForwardedHost,
    RequireHeaderSymmetry = false
});

// Local có thể bật HTTPS redirect nếu muốn
// if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FoMed API v1");
    c.DocumentTitle = "FoMed API Documentation";
});

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// Health
app.MapGet("/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

app.Run();
