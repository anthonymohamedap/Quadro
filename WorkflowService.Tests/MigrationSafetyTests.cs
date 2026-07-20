using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-30 — the Baseline squash. These tests exercise the two real-world paths:
/// a fresh install (clean MigrateAsync) and an upgrade of an old EnsureCreated
/// database (healer patches + Baseline marking). Plus the drift guard that
/// replaces the old PendingModelChangesWarning suppression.
/// </summary>
public class MigrationSafetyTests : IDisposable
{
    private readonly string _dir;

    public MigrationSafetyTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuadroMigrationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* temp dir cleanup is best-effort */ }
        catch (UnauthorizedAccessException) { /* idem */ }
    }

    private AppDbContext CreateContext(string dbFile)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, dbFile)}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task AssertAllTablesQueryableAsync(AppDbContext db)
    {
        // A SELECT against every core DbSet catches missing tables/columns.
        _ = await db.Klanten.CountAsync();
        _ = await db.TypeLijsten.CountAsync();
        _ = await db.Leveranciers.CountAsync();
        _ = await db.AfwerkingsGroepen.CountAsync();
        _ = await db.AfwerkingsOpties.CountAsync();
        _ = await db.Offertes.CountAsync();
        _ = await db.OfferteRegels.CountAsync();
        _ = await db.WerkBonnen.CountAsync();
        _ = await db.Facturen.CountAsync();
        _ = await db.VoorraadAlerts.CountAsync();
        _ = await db.LeverancierBestellingen.CountAsync();
    }

    [Fact]
    public void Assembly_DefinesExactlyOneMigration_TheBaseline()
    {
        using var db = CreateContext("defined.db");
        var migrations = db.Database.GetMigrations().ToList();

        Assert.Single(migrations);
        Assert.EndsWith("Baseline", migrations[0]);
    }

    [Fact]
    public void Model_HasNoPendingChanges_SnapshotInSync()
    {
        // Replaces the old PendingModelChangesWarning suppression: if this
        // fails, run Scripts/regen-baseline.ps1 or add a proper migration.
        using var db = CreateContext("drift.db");
        Assert.False(db.Database.HasPendingModelChanges(),
            "EF-model wijkt af van de migration-snapshot — voeg een migratie toe.");
    }

    [Fact]
    public async Task FreshDatabase_PatchAsync_CreatesSchemaViaBaseline()
    {
        await using var db = CreateContext("fresh.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Single(applied);
        Assert.EndsWith("Baseline", applied[0]);

        await AssertAllTablesQueryableAsync(db);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task ExistingEnsureCreatedDatabase_PatchAsync_MarksBaselineWithoutDataLoss()
    {
        // Simulate an old install: schema via EnsureCreated (no migration history) + data.
        await using (var old = CreateContext("upgrade.db"))
        {
            await old.Database.EnsureCreatedAsync();
            old.Klanten.Add(new QuadroApp.Model.DB.Klant { Voornaam = "Bestaande", Achternaam = "Klant" });
            await old.SaveChangesAsync();
        }

        await using var db = CreateContext("upgrade.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        // Baseline marked as applied, no pending migrations, data intact.
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Contains(applied, m => m.EndsWith("Baseline"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await db.Klanten.CountAsync(k => k.Achternaam == "Klant" && k.Voornaam == "Bestaande"));
        await AssertAllTablesQueryableAsync(db);
    }

    [Fact]
    public async Task ExistingDatabase_WithStalePreSquashHistory_IsCleaned()
    {
        // Old installs have fake pre-marked history rows — they must be pruned.
        await using (var old = CreateContext("stale.db"))
        {
            await old.Database.EnsureCreatedAsync();
            await old.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" " +
                "(\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL)");
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260228232856_InitialClean', '9.0.0')");
        }

        await using var db = CreateContext("stale.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.DoesNotContain(applied, m => m.Contains("InitialClean"));
        Assert.Contains(applied, m => m.EndsWith("Baseline"));
    }

    [Fact]
    public async Task PatchAsync_IsIdempotent_SecondRunChangesNothing()
    {
        await using var db = CreateContext("idempotent.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        await AssertAllTablesQueryableAsync(db);
    }
}
