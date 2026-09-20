using System.Security.Cryptography;
using AminAmval.Domain.Services;

namespace AminAmval.Infrastructure.Services;

public sealed class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"v1.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 3 || parts[0] != "v1") return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
