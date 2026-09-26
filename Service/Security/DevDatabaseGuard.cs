using System;
using System.IO;

namespace QuadroApp.Service.Security;

/// <summary>
/// Vangnet voor ontwikkeling: een Debug-build mag nooit per ongeluk op de echte (cloud-)
/// PostgreSQL-database draaien. De data-map (<c>%LOCALAPPDATA%\QuadroApp\appsettings.json</c>)
/// wordt gedeeld met de geïnstalleerde app en kan dus de productie-verbinding bevatten —
/// zonder dit vangnet zou testdata (en zelfs een nog niet vrijgegeven migratie) in productie belanden.
///
/// Regel: in een Debug-build wordt een PostgreSQL-verbinding vervangen door een aparte lokale
/// SQLite-database <c>quadro-dev.db</c>, tenzij <see cref="AllowPgEnvVar"/> expliciet op "1"
/// staat. Release-builds (wat de klant installeert) worden nooit aangepast.
/// </summary>
public static class DevDatabaseGuard
{
    public const string AllowPgEnvVar = "QUADRO_DEV_ALLOW_PG";
    public const string DevDbFileName = "quadro-dev.db";

    /// <param name="connectionString">De geresolvede verbinding (env var / appsettings / fallback).</param>
    /// <param name="isDebugBuild">true in een Debug-build (<c>#if DEBUG</c>).</param>
    /// <param name="allowPgValue">Waarde van <see cref="AllowPgEnvVar"/> (null als niet gezet).</param>
    /// <param name="dataDirectory">De app-datamap waarin <c>quadro-dev.db</c> komt.</param>
    /// <param name="redirected">true als de verbinding omgeleid werd naar de dev-database.</param>
    public static string Apply(string connectionString, bool isDebugBuild, string? allowPgValue,
                               string dataDirectory, out bool redirected)
    {
        redirected = false;
        if (!isDebugBuild) return connectionString;
        if (!IsPostgres(connectionString)) return connectionString;
        if (string.Equals(allowPgValue?.Trim(), "1", StringComparison.Ordinal)) return connectionString;

        redirected = true;
        return $"Data Source={Path.Combine(dataDirectory, DevDbFileName)}";
    }

    private static bool IsPostgres(string cs)
        => cs.Contains("Host=", StringComparison.OrdinalIgnoreCase);
}
