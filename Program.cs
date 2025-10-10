using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using FoMed.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using FoMed.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// ===== EF Core =====
var connectionString =
    "Server=localhost;Database=FoMed_Managerment;User Id=sa;Password=khanhduy23112002;Encrypt=True;TrustServerCertificate=True";
builder.Services.AddDbContext<FoMedContext>(options =>
    options.UseSqlServer(connectionString)
);

// ===== Controllers (gộp 1 lần) + JSON converters =====
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // format ngày dd/MM/yyyy
        o.JsonSerializerOptions.Converters.Add(new JsonDateOnlyConverter("dd/MM/yyyy"));
        o.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter("dd/MM/yyyy"));

        // Enum hiển thị chuỗi (waiting/booked/...) thay vì số
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    });

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FoMed API", Version = "v1" });
    c.EnableAnnotations();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập **access token** (KHÔNG kèm chữ Bearer).",
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

// ===== AuthN/AuthZ =====
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };

        // === Viết body cho 401/403 ===
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var id = (ClaimsIdentity)context.Principal!.Identity!;
                var extraRoles = id.FindAll("role").Concat(id.FindAll("roles"))
                           .Select(r => r.Value).Distinct().ToList();
                foreach (var r in extraRoles)
                    id.AddClaim(new Claim(ClaimTypes.Role, r));
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                var payload = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Bạn chưa đăng nhập hoặc token không hợp lệ."
                });
                return context.Response.WriteAsync(payload);
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                var payload = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Bạn không có quyền truy cập."
                });
                return context.Response.WriteAsync(payload);
            }
        };
    });

builder.Services.AddAuthorization();

// Trả lỗi model-state ngắn gọn
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        return new BadRequestObjectResult(new { errors });
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FoMed API v1");
    c.DocumentTitle = "FoMed API Documentation";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
