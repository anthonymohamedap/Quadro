using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QuadroApp.Service.Foutlog;

/// <summary>Unieke vingerafdruk van een fout, zodat dezelfde crash niet dubbel in de database komt.</summary>
public static class FoutLogHash
{
    /// <summary>SHA-256 (hex, kleine letters) op tijdstip (UTC, seconde) + type + eerste 3 stackregels (ruw) + computer.</summary>
    public static string Bereken(DateTime tijdstipUtc, string exceptionType, string stacktrace, string computer)
    {
        var eerste = (stacktrace ?? "")
            .Split('\n')
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Take(3);
        var tekst = string.Concat(
            tijdstipUtc.ToString("yyyy-MM-dd'T'HH':'mm':'ss", CultureInfo.InvariantCulture),
            "|", exceptionType, "|", string.Join("\n", eerste), "|", computer);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tekst))).ToLowerInvariant();
    }
}
