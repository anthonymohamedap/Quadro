using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-79 — enkel nog Bancontact, Cash, Credit card en Overschrijving.</summary>
public class BetaalwijzeTests : IDisposable
{
    private readonly string _dir;

    public BetaalwijzeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuadroBetaalwijzeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* idem */ }
    }

    [Fact]
    public void Enkel_de_vier_betaalwijzen_in_deze_volgorde()
    {
        Assert.Equal(
            new[] { Betaalwijze.Bancontact, Betaalwijze.Cash, Betaalwijze.CreditCard, Betaalwijze.Overschrijving },
            Enum.GetValues<Betaalwijze>());
    }

    [Theory]
    [InlineData(Betaalwijze.Bancontact, "Bancontact")]
    [InlineData(Betaalwijze.Cash, "Cash")]
    [InlineData(Betaalwijze.CreditCard, "Credit card")]
    [InlineData(Betaalwijze.Overschrijving, "Overschrijving")]
    public void Label_is_leesbaar(Betaalwijze betaalwijze, string verwacht)
        => Assert.Equal(verwacht, betaalwijze.Label());

    [Fact]
    public void Nieuwe_ontvangst_staat_standaard_op_bancontact()
        => Assert.Equal(Betaalwijze.Bancontact, new Ontvangst().Betaalwijze);

    [Fact]
    public async Task Migratie_zet_oude_betaalwijzen_om()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "betaalwijzen.db")}")
            .Options;
        await using var db = new AppDbContext(options);

        var migraties = db.Database.GetMigrations().ToList();
        var index = migraties.FindIndex(m => m.EndsWith("_BetaalwijzenVereenvoudigen", StringComparison.Ordinal));
        Assert.True(index > 0, "Migratie BetaalwijzenVereenvoudigen ontbreekt.");

        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(migraties[index - 1]);

        foreach (var oud in new[] { "Kontant", "Cheque", "Proton", "Visa", "Bancontact", "Storting" })
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Ontvangsten\" (\"Datum\", \"Soort\", \"Betaalwijze\", \"BedragIncl\", \"Aantal\", \"PrijsPerStukIncl\", \"KortingPct\", \"AangemaaktOp\", \"Omschrijving\") " +
                "VALUES ('2026-10-01', 'Winkelverkoop', {0}, 10, 1, 10, 0, '2026-10-01', {0})", oud);
        }

        await migrator.MigrateAsync();

        var perOmschrijving = await db.Ontvangsten.AsNoTracking()
            .ToDictionaryAsync(o => o.Omschrijving!, o => o.Betaalwijze);

        Assert.Equal(Betaalwijze.Cash, perOmschrijving["Kontant"]);
        Assert.Equal(Betaalwijze.Cash, perOmschrijving["Cheque"]);
        Assert.Equal(Betaalwijze.Cash, perOmschrijving["Proton"]);
        Assert.Equal(Betaalwijze.CreditCard, perOmschrijving["Visa"]);
        Assert.Equal(Betaalwijze.Bancontact, perOmschrijving["Bancontact"]);
        Assert.Equal(Betaalwijze.Overschrijving, perOmschrijving["Storting"]);
    }
}
