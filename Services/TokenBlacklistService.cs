using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;

namespace FoMed.Api.Services;

public interface ITokenBlacklistService
{
    Task RevokeTokenAsync(string token, TimeSpan expirationTime);
    Task<bool> IsTokenRevokedAsync(string token);
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;

    private static string KeyFor(string token)
    {
        // Avoid very long cache keys (JWTs are ~1-2KB) and provider-specific limits.
        // Hash token -> stable, compact key.
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        var b64 = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"jwtbl:{b64}";
    }

    public TokenBlacklistService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task RevokeTokenAsync(string token, TimeSpan expirationTime)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationTime
        };

        await _cache.SetStringAsync(KeyFor(token), "revoked", options);
    }

    public async Task<bool> IsTokenRevokedAsync(string token)
    {
        var value = await _cache.GetStringAsync(KeyFor(token));
        return !string.IsNullOrEmpty(value);
    }
}