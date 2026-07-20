using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.Service.Backup;

/// <summary>
/// US-34 — daily automatic SQLite backups using the safe online-backup API
/// (SqliteConnection.BackupDatabase), which produces a consistent copy even
/// while the app has the database open.
///
/// Behaviour:
///   - One backup per calendar day: quadro-backup-yyyyMMdd.db (skip if it exists)
///   - Default target: &lt;dataDir&gt;/Backups, overridable via Backup:Directory
///   - Retention: files older than RetentionDays (default 30) are deleted
///   - PostgreSQL mode: skipped here — use Scripts/backup-postgres.ps1 (pg_dump)
///   - Never throws: backup failure must never break app startup
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly string _connectionString;
    private readonly string _defaultBackupDir;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupService> _logger;

    public BackupService(string connectionString, string dataDir, BackupOptions options, ILogger<BackupService> logger)
    {
        _connectionString = connectionString;
        _defaultBackupDir = Path.Combine(dataDir, "Backups");
        _options = options;
        _logger = logger;
    }

    public async Task<string?> RunDailyBackupAsync()
    {
        try
        {
            var sourcePath = GetSqlitePath(_connectionString);
            if (sourcePath is null)
            {
                _logger.LogInformation("[Backup] Geen SQLite-database (PostgreSQL-modus) — in-app backup overgeslagen. Gebruik pg_dump (Scripts/backup-postgres.ps1).");
                return null;
            }

            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("[Backup] Databasebestand niet gevonden: {Path}", sourcePath);
                return null;
            }

            var backupDir = string.IsNullOrWhiteSpace(_options.Directory) ? _defaultBackupDir : _options.Directory;
            Directory.CreateDirectory(backupDir);

            var targetPath = Path.Combine(backupDir, $"quadro-backup-{DateTime.Now:yyyyMMdd}.db");
            if (File.Exists(targetPath))
            {
                Prune(backupDir);
                return null; // already backed up today
            }

            await Task.Run(() => CreateBackup(sourcePath, targetPath));
            _logger.LogInformation("[Backup] Dagelijkse backup gemaakt: {Path}", targetPath);

            Prune(backupDir);
            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Backup] Backup mislukt: {Message}", ex.Message);
            return null;
        }
    }

    private static void CreateBackup(string sourcePath, string targetPath)
    {
        // Write to a temp file first, then atomically move into place, so a
        // half-written file never looks like a valid backup.
        var tmpPath = targetPath + ".tmp";

        using (var source = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly"))
        using (var target = new SqliteConnection($"Data Source={tmpPath}"))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }
        SqliteConnection.ClearAllPools(); // release file handles before the move

        File.Move(tmpPath, targetPath, overwrite: true);
    }

    private void Prune(string backupDir)
    {
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, _options.RetentionDays));
            var expired = Directory.GetFiles(backupDir, "quadro-backup-*.db")
                .Where(f => ParseBackupDate(f) is { } d && d < cutoff)
                .ToList();

            foreach (var file in expired)
            {
                File.Delete(file);
                _logger.LogInformation("[Backup] Verlopen backup verwijderd: {File}", Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Backup] Opschonen van oude backups mislukt: {Message}", ex.Message);
        }
    }

    /// <summary>Parses the date from "quadro-backup-yyyyMMdd.db"; null when it doesn't match.</summary>
    internal static DateTime? ParseBackupDate(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        const string prefix = "quadro-backup-";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return DateTime.TryParseExact(name[prefix.Length..], "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>Extracts the file path from a SQLite connection string; null for non-SQLite.</summary>
    internal static string? GetSqlitePath(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)) return null; // PostgreSQL

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.DataSource) ? null : builder.DataSource;
        }
        catch
        {
            return null;
        }
    }
}
