using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-30 — the Baseline squash. Exercises the two real-world paths:
/// a fresh install (clean MigrateAsync) and an upgrade of a legacy database
/// (Baseline-shaped, no usable history → healer + Baseline marking, after
/// which post-Baseline migrations run normally). Plus the drift guard that
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

    /// <summary>
    /// Simulates a legacy production database: schema exactly at Baseline shape
    /// (via targeted migrate) but without usable migration history (old installs
    /// were created via EnsureCreated + raw patches).
    /// </summary>
    private static async Task MakeLegacyBaselineDbAsync(AppDbContext db)
    {
        var baseline = db.Database.GetMigrations().First(m => m.EndsWith("_Baseline", StringComparison.Ordinal));
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(baseline);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
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
        _ = await db.Gebruikers.CountAsync();
    }

    [Fact]
    public void Assembly_FirstMigrationIsBaseline()
    {
        using var db = CreateContext("defined.db");
        var migrations = db.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.EndsWith("_Baseline", migrations[0]);
    }

    [Fact]
    public void Model_HasNoPendingChanges_SnapshotInSync()
    {
        // Replaces the old PendingModelChangesWarning suppression: if this
        // fails, add a proper migration (Scripts/add-migration.ps1 <Naam>).
        using var db = CreateContext("drift.db");
        Assert.False(db.Database.HasPendingModelChanges(),
            "EF-model wijkt af van de migration-snapshot — voeg een migratie toe.");
    }

    [Fact]
    public async Task FreshDatabase_PatchAsync_CreatesSchemaViaMigrations()
    {
        await using var db = CreateContext("fresh.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        var defined = db.Database.GetMigrations().OrderBy(m => m).ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).OrderBy(m => m).ToList();
        Assert.Equal(defined, applied);

        await AssertAllTablesQueryableAsync(db);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task LegacyDatabase_PatchAsync_MarksBaselineAndRunsNewerMigrations_NoDataLoss()
    {
        await using (var old = CreateContext("upgrade.db"))
        {
            await MakeLegacyBaselineDbAsync(old);
            old.Klanten.Add(new QuadroApp.Model.DB.Klant { Voornaam = "Bestaande", Achternaam = "Klant" });
            await old.SaveChangesAsync();
        }

        await using var db = CreateContext("upgrade.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        // All defined migrations applied (Baseline marked, newer ones executed), data intact.
        var defined = db.Database.GetMigrations().OrderBy(m => m).ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).OrderBy(m => m).ToList();
        Assert.Equal(defined, applied);
        Assert.Equal(1, await db.Klanten.CountAsync(k => k.Achternaam == "Klant" && k.Voornaam == "Bestaande"));
        await AssertAllTablesQueryableAsync(db);
    }

    [Fact]
    public async Task LegacyDatabase_WithStalePreSquashHistory_IsCleaned()
    {
        await using (var old = CreateContext("stale.db"))
        {
            await MakeLegacyBaselineDbAsync(old);
            await old.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20260228232856_InitialClean', '9.0.0')");
        }

        await using var db = CreateContext("stale.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.DoesNotContain(applied, m => m.Contains("InitialClean"));
        Assert.Contains(applied, m => m.EndsWith("_Baseline"));
    }

    [Fact]
    public async Task PatchAsync_IsIdempotent_SecondRunChangesNothing()
    {
        await using var db = CreateContext("idempotent.db");
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);
        await SqliteSchemaPatcher.PatchAsync(db, NullLogger.Instance);

        var defined = db.Database.GetMigrations().OrderBy(m => m).ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).OrderBy(m => m).ToList();
        Assert.Equal(defined, applied);
        await AssertAllTablesQueryableAsync(db);
    }
}
