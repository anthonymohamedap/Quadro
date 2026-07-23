using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-38 — bewaakt de concurrency-hardening: het RowVersion-token op TypeLijst
/// (voorraad read-modify-write) en de unieke factuurnummer-constraints.
///
/// Let op: SQLite werkt een rowversion-token niet automatisch bij (dat gebeurt pas
/// op PostgreSQL via xmin). Daarom borgen we de token-configuratie via de EF-model-
/// metadata en toetsen we het afdwingbare gedrag (de unieke index) op runtime.
/// </summary>
public class ConcurrencyTests
{
    // ── Model-metadata: bewaakt dat de wiring niet stilletjes wegvalt ──────────

    [Fact]
    public async Task TypeLijst_RowVersion_is_concurrency_token()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await dbScope.Factory.CreateDbContextAsync();

        var prop = db.Model.FindEntityType(typeof(TypeLijst))!.FindProperty(nameof(TypeLijst.RowVersion));

        Assert.NotNull(prop);
        Assert.True(prop!.IsConcurrencyToken, "TypeLijst.RowVersion moet een concurrency-token zijn (US-38).");
    }

    [Fact]
    public async Task Factuur_has_unique_indexes_on_number_fields()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await dbScope.Factory.CreateDbContextAsync();

        var entity = db.Model.FindEntityType(typeof(Factuur))!;
        var indexes = entity.GetIndexes().ToList();

        Assert.Contains(indexes, i =>
            i.IsUnique &&
            i.Properties.Count == 1 &&
            i.Properties[0].Name == nameof(Factuur.FactuurNummer));

        Assert.Contains(indexes, i =>
            i.IsUnique &&
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == nameof(Factuur.Jaar)) &&
            i.Properties.Any(p => p.Name == nameof(Factuur.VolgNr)));
    }

    // ── Runtime: de unieke index wordt door de database afgedwongen ────────────

    [Fact]
    public async Task Duplicate_FactuurNummer_is_rejected()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Facturen.Add(NieuweFactuur("2026-1", jaar: 2026, volgNr: 1));
            await db.SaveChangesAsync();
        }

        await using (var db = await factory.CreateDbContextAsync())
        {
            // Zelfde nummer, ander (Jaar, VolgNr) → alleen de FactuurNummer-index botst.
            db.Facturen.Add(NieuweFactuur("2026-1", jaar: 2026, volgNr: 2));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Duplicate_Jaar_VolgNr_is_rejected()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Facturen.Add(NieuweFactuur("2026-1", jaar: 2026, volgNr: 1));
            await db.SaveChangesAsync();
        }

        await using (var db = await factory.CreateDbContextAsync())
        {
            // Ander nummer, zelfde (Jaar, VolgNr) → de (Jaar, VolgNr)-index botst.
            db.Facturen.Add(NieuweFactuur("2026-999", jaar: 2026, volgNr: 1));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private static Factuur NieuweFactuur(string nummer, int jaar, int volgNr) => new()
    {
        FactuurNummer = nummer,
        Jaar = jaar,
        VolgNr = volgNr,
        DocumentType = "Factuur",
        KlantNaam = "Testklant",
        FactuurDatum = new DateTime(jaar, 1, 1),
        Status = FactuurStatus.Draft
    };
}
