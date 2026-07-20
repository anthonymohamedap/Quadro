using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Service.Backup;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-34 — automatic backups.</summary>
public class BackupServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;

    public BackupServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuadroBackupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "quadro.db");

        // Create a real SQLite db with one table + row
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE Klanten (Id INTEGER PRIMARY KEY, Naam TEXT); INSERT INTO Klanten (Naam) VALUES ('Testklant');";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private BackupService CreateSut(BackupOptions? options = null) => new(
        $"Data Source={_dbPath}", _dir, options ?? new BackupOptions(),
        NullLogger<BackupService>.Instance);

    [Fact]
    public async Task RunDailyBackup_CreatesValidBackupFile()
    {
        var result = await CreateSut().RunDailyBackupAsync();

        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        // Backup must be a valid SQLite db containing the data
        using var conn = new SqliteConnection($"Data Source={result};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Naam FROM Klanten LIMIT 1";
        Assert.Equal("Testklant", cmd.ExecuteScalar());
    }

    [Fact]
    public async Task RunDailyBackup_SecondRunSameDay_Skips()
    {
        var sut = CreateSut();
        var first = await sut.RunDailyBackupAsync();
        var second = await sut.RunDailyBackupAsync();

        Assert.NotNull(first);
        Assert.Null(second); // already backed up today
    }

    [Fact]
    public async Task RunDailyBackup_PrunesExpiredBackups()
    {
        var backupDir = Path.Combine(_dir, "Backups");
        Directory.CreateDirectory(backupDir);
        var oldFile = Path.Combine(backupDir,
            $"quadro-backup-{DateTime.Now.AddDays(-40):yyyyMMdd}.db");
        File.WriteAllText(oldFile, "oud");
        var recentFile = Path.Combine(backupDir,
            $"quadro-backup-{DateTime.Now.AddDays(-5):yyyyMMdd}.db");
        File.WriteAllText(recentFile, "recent");

        await CreateSut(new BackupOptions { RetentionDays = 30 }).RunDailyBackupAsync();

        Assert.False(File.Exists(oldFile), "backup ouder dan 30 dagen moet verwijderd zijn");
        Assert.True(File.Exists(recentFile), "recente backup moet blijven staan");
    }

    [Fact]
    public async Task RunDailyBackup_PostgresConnectionString_Skips()
    {
        var sut = new BackupService("Host=localhost;Database=quadrodb;Username=quadro",
            _dir, new BackupOptions(), NullLogger<BackupService>.Instance);
        Assert.Null(await sut.RunDailyBackupAsync());
    }

    [Fact]
    public async Task RunDailyBackup_CustomDirectory_IsUsed()
    {
        var custom = Path.Combine(_dir, "Elders");
        var result = await CreateSut(new BackupOptions { Directory = custom }).RunDailyBackupAsync();

        Assert.NotNull(result);
        Assert.StartsWith(custom, result);
    }

    [Theory]
    [InlineData("Data Source=c:\\pad\\quadro.db", "c:\\pad\\quadro.db")]
    [InlineData("Host=localhost;Database=x", null)]
    [InlineData("", null)]
    public void GetSqlitePath_Parses(string cs, string? expected)
    {
        Assert.Equal(expected, BackupService.GetSqlitePath(cs));
    }

    [Fact]
    public void ParseBackupDate_ValidAndInvalid()
    {
        Assert.Equal(new DateTime(2026, 7, 20), BackupService.ParseBackupDate("quadro-backup-20260720.db"));
        Assert.Null(BackupService.ParseBackupDate("andersbestand.db"));
    }
}
