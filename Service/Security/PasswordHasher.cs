using System;
using System.Security.Cryptography;

namespace QuadroApp.Service.Security;

/// <summary>
/// US-32 — PBKDF2 (SHA-256) wachtwoord-hashing.
/// Formaat: "iteraties.saltBase64.hashBase64" zodat parameters per hash
/// meebewaard worden en later verhoogd kunnen worden zonder migratie.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000; // OWASP-richtlijn voor PBKDF2-SHA256
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return false;
        var parts = stored.Split('.', 3);
        if (parts.Length != 3) return false;

        try
        {
            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }
}
