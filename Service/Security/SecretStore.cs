using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace QuadroApp.Service.Security;

/// <summary>
/// US-33 — Secrets management. Resolves the database password without ever
/// storing it as plaintext next to the exe.
///
/// Resolution order (first hit wins):
///   1. Environment variable  QUADRO_DB_PASSWORD
///   2. Secret file           &lt;dataDir&gt;/db.secret
///        Windows → DPAPI-encrypted (CurrentUser scope, only this Windows account can decrypt)
///        macOS   → plaintext file with 600 permissions (owner-only)
///   3. Password already present in the connection string (legacy, discouraged)
///
/// The connection string in appsettings.json should use the placeholder:
///   Password=__SECRET__
/// which this class replaces at startup. SQLite connection strings pass through untouched.
/// </summary>
public static class SecretStore
{
    public const string PasswordEnvVar = "QUADRO_DB_PASSWORD";
    public const string SecretFileName = "db.secret";
    public const string Placeholder = "__SECRET__";

    /// <summary>Optional entropy so the DPAPI blob is bound to this app.</summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("QuadroApp.v1");

    /// <summary>
    /// Injects the resolved DB password into a PostgreSQL connection string.
    /// SQLite strings (no "Password=" needed) are returned unchanged.
    /// Returns the input unchanged when no secret source is available, so
    /// startup never hard-fails here — the DB connect will surface the error.
    /// </summary>
    public static string InjectPassword(string connectionString, string dataDir)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        // Only Postgres strings carry a password; SQLite passes through.
        if (!connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var hasPlaceholder = connectionString.Contains($"Password={Placeholder}", StringComparison.OrdinalIgnoreCase)
                             || connectionString.Contains("Password=CHANGE_ME", StringComparison.OrdinalIgnoreCase);
        var hasPasswordKey = connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase);

        // A real password is already present (legacy path) — leave as-is.
        if (hasPasswordKey && !hasPlaceholder)
            return connectionString;

        var secret = ResolvePassword(dataDir);
        if (secret is null)
            return connectionString; // no secret source — connection attempt will fail loudly

        if (hasPlaceholder)
        {
            connectionString = connectionString
                .Replace($"Password={Placeholder}", $"Password={secret}", StringComparison.OrdinalIgnoreCase)
                .Replace("Password=CHANGE_ME", $"Password={secret}", StringComparison.OrdinalIgnoreCase);
            return connectionString;
        }

        // No Password= key at all — append.
        var sep = connectionString.TrimEnd().EndsWith(';') ? "" : ";";
        return $"{connectionString}{sep}Password={secret}";
    }

    /// <summary>
    /// Resolves the DB password: env var first, then the secret file. Null when neither exists.
    /// </summary>
    public static string? ResolvePassword(string dataDir)
    {
        var fromEnv = Environment.GetEnvironmentVariable(PasswordEnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var path = Path.Combine(dataDir, SecretFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var raw = File.ReadAllBytes(path);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var decrypted = ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            return Encoding.UTF8.GetString(raw).Trim();
        }
        catch
        {
            // Corrupt/foreign-account blob — treat as absent; DB connect will surface the problem.
            return null;
        }
    }

    /// <summary>
    /// Stores the DB password in the secret file (DPAPI on Windows, 600-perms file elsewhere).
    /// </summary>
    public static void StorePassword(string password, string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var path = Path.Combine(dataDir, SecretFileName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }
        else
        {
            File.WriteAllText(path, password);
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* best effort on non-Unix */ }
        }
    }

    /// <summary>True when the connection string still contains an unresolved placeholder.</summary>
    public static bool HasUnresolvedPlaceholder(string connectionString) =>
        connectionString.Contains($"Password={Placeholder}", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Password=CHANGE_ME", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Redacts the password for safe logging: "Password=xyz" → "Password=***".
    /// </summary>
    public static string Redact(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return connectionString;
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString, @"(Password\s*=\s*)[^;]+", "$1***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
