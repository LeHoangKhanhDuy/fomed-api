using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using FoMed.Api.Auth;
using FoMed.Api.Models;
using System.Text;
using Microsoft.OpenApi.Any;
using Microsoft.Extensions.FileProviders;

// ================== 1) CONFIG ==================
var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// DB connection
var cs = cfg.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

// CORS allowed origins (cho policy dùng credentials)
var allowedOrigins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

// JWT config
var jwtIssuer = cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Jwt:Issuer");
var jwtAudience = cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Missing Jwt:Audience");
var jwtKeyB64 = cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");

// Decode Base64 key (>= 32 bytes)
byte[] jwtKeyBytes;
try { jwtKeyBytes = Convert.FromBase64String(jwtKeyB64); }
catch { throw new InvalidOperationException("Jwt:Key must be BASE64 (256-bit+)."); }
if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException("Jwt:Key too short. Need >= 32 bytes (256-bit).");

// ================== SERVICES ==================

// EF Core + retry
builder.Services.AddDbContext<FoMedContext>(opt =>
    opt.UseSqlServer(cs, sql =>
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// Controllers + JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Convert PascalCase -> camelCase
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;

        // Ignore null values
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

        // Format DateTime as ISO 8601
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ------------- CORS  -------------
const string PublicApi = "PublicApi";
const string AuthWithCreds = "AuthWithCreds";

builder.Services.AddCors(o =>
{
    // Policy mặc định: không credentials 
    o.AddPolicy(PublicApi, p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());

    // Policy cho route cần cookie/credentials 
    o.AddPolicy(AuthWithCreds, p =>
    {
        if (allowedOrigins.Length == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins is required for AuthWithCreds.");
        p.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Swagger + Bearer
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FoMed API", Version = "v1" });
    c.EnableAnnotations();

    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Paste **only** the token (no 'Bearer ' prefix).",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
    c.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date", Example = new OpenApiString("2025-10-21") });
    c.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Example = new OpenApiString("09:35:00") });
});

var signingKey = new SymmetricSecurityKey(jwtKeyBytes);

// JWT Auth
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = signingKey,

        ClockSkew = TimeSpan.FromMinutes(2)
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine("JWT failed: " + ctx.Exception?.Message);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            Console.WriteLine("JWT challenge: " + ctx.ErrorDescription);
            return Task.CompletedTask;
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

// DI cho token service
builder.Services.AddScoped<ITokenService, TokenService>();

// ================== BUILD & PIPELINE ==================
var app = builder.Build();

// Forwarded headers 
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedHost,
    RequireHeaderSymmetry = false
});

var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
var doctorsPath = Path.Combine(uploadsPath, "doctors");
var avatarsPath = Path.Combine(uploadsPath, "avatars");

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"Created folder: {uploadsPath}");
}

if (!Directory.Exists(doctorsPath))
{
    Directory.CreateDirectory(doctorsPath);
    Console.WriteLine($"Created folder: {doctorsPath}");
}

if (!Directory.Exists(avatarsPath))
{
    Directory.CreateDirectory(avatarsPath);
    Console.WriteLine($"Created folder: {avatarsPath}");
}

// Serve files từ wwwroot
app.UseStaticFiles();

// // Serve files từ folder uploads
// app.UseStaticFiles(new StaticFileOptions
// {
//     FileProvider = new PhysicalFileProvider(
//         Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
//     RequestPath = "/uploads",
//     OnPrepareResponse = ctx =>
//     {
//         ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800");
//     }
// });

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FoMed API v1");
    c.DocumentTitle = "FoMed API Documentation";
});

app.UseRouting();

// CORS mặc định: PublicApi
app.UseCors(PublicApi);

app.UseAuthentication();
app.UseAuthorization();

// Health
app.MapGet("/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }))
.AllowAnonymous();

// ================== ROUTES ==================
// Refresh token
app.MapPost("/api/v1/auth/refresh", async (HttpContext http, ITokenService tokenSvc) =>
{
    var refresh = http.Request.Cookies["refresh_token"];
    if (string.IsNullOrEmpty(refresh))
        return Results.Unauthorized();
    return Results.Ok(new { token = "placeholder", expires_in = 900 });
})
.RequireCors(AuthWithCreds)
.AllowAnonymous();

// Controllers
app.MapControllers();

app.Run();
