using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace QuadroApp.Service;

/// <summary>
/// US-31 — gestructureerde logging. Serilog met roterend logbestand:
///   Windows → %LOCALAPPDATA%\QuadroApp\logs\quadro-JJJJMMDD.log
///   macOS   → ~/Library/Application Support/QuadroApp/logs/...
/// Retentie: 14 bestanden (dagen). Niveau configureerbaar via appsettings.json:
///   { "Logging": { "MinimumLevel": "Debug" } }
/// Gevoelige data (wachtwoorden, connection strings) mag nooit gelogd worden —
/// gebruik SecretStore.Redact() voor connection strings.
/// </summary>
public static class LoggingSetup
{
    public static Serilog.Core.Logger CreateLogger(string dataDir, string? minimumLevel = null)
    {
        var logDir = Path.Combine(dataDir, "logs");
        Directory.CreateDirectory(logDir);

        var level = ParseLevel(minimumLevel);

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(logDir, "quadro-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();
    }

    public static LogEventLevel ParseLevel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "verbose" or "trace" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "warning" or "warn" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" or "critical" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}
