using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using FoMed.Api.Models;

namespace FoMed.Api.Auth
{
    public sealed class TokenService : ITokenService
    {
        private readonly IConfiguration _cfg;

        public TokenService(IConfiguration cfg) => _cfg = cfg;

        public string CreateAccessToken(User user, IEnumerable<string> roles, int? doctorId = null)
        {
            var roleCodes = roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName ?? string.Empty),
            };

            foreach (var rc in roleCodes)
            {
                claims.Add(new Claim(ClaimTypes.Role, rc));
                // thêm "role" để tương thích nếu có service khác đọc custom claim
                claims.Add(new Claim("role", rc));
            }

            if (doctorId.HasValue)
            {
                claims.Add(new Claim("doctor_id", doctorId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: _cfg["Jwt:Audience"],
                claims: claims,
                expires: GetAccessTokenExpiry(),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public DateTime GetAccessTokenExpiry()
        {
            var minutes = int.Parse(_cfg["Jwt:AccessMinutes"] ?? "60");
            return DateTime.UtcNow.AddMinutes(minutes);
        }
    }
}
