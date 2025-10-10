using FoMed.Api.Models;

namespace FoMed.Api.Auth
{
    public interface ITokenService
    {
        string CreateAccessToken(User user, IEnumerable<string> roles, int? doctorId = null);
        DateTime GetAccessTokenExpiry();
    }
}
