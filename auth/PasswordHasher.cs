using System.Security.Cryptography;

namespace FoMed.Api.Auth;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;   // bytes
    private const int KeySize = 32;   // bytes

    public static (byte[] hash, byte[] salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (hash, salt);
    }

    public static bool Verify(string password, byte[] hash, byte[]? salt)
    {
        if (salt is null || salt.Length == 0) return false;
        var test = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(test, hash);
    }
}
