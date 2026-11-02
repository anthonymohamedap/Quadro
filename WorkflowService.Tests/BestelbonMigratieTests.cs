using System;
using System.Collections.Generic;
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

/// <summary>
/// US-99 fase C — de migratie HernoemFactuurNaarBestelbon hernoemt tabellen en kolommen
/// zonder gegevensverlies: bestelbonnen, lijnen, betalingen, nummers en koppelingen blijven identiek.
/// </summary>
public class BestelbonMigratieTests : IDisposable
{
    private readonly string _dir;

    public BestelbonMigratieTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuadroBestelbonMigratie_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); }
        catch { /* tijdelijke map */ }
    }

    [Fact]
    public async Task Migratie_hernoemt_zonder_gegevensverlies()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "bestelbon.db")}")
            .Options;

        await using (var db = new AppDbContext(options))
        {
            var migraties = db.Database.GetMigrations().ToList();
            var index = migraties.FindIndex(m => m.EndsWith("_HernoemFactuurNaarBestelbon", StringComparison.Ordinal));
            Assert.True(index > 0, "Migratie HernoemFactuurNaarBestelbon ontbreekt.");

            var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(migraties[index - 1]);

            // ── Oude database: tabellen Facturen / FactuurLijnen, kolommen FactuurId enz. ──
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            await conn.OpenAsync();

            var klantId = await InsertAsync(conn, "Klanten", new() { ["Voornaam"] = "Sofie", ["Achternaam"] = "Claes" });
            var offerteId = await InsertAsync(conn, "Offertes", new() { ["KlantId"] = klantId, ["OfferteNummer"] = 423, ["Status"] = "Besteld" });
            var werkBonId = await InsertAsync(conn, "WerkBonnen", new() { ["OfferteId"] = offerteId, ["Status"] = "Afgehaald" });
            var bonId = await InsertAsync(conn, "Facturen", new()
            {
                ["OfferteId"] = offerteId, ["WerkBonId"] = werkBonId, ["Jaar"] = 2026, ["VolgNr"] = 23,
                ["FactuurNummer"] = "2026-23", ["FactuurDatum"] = "2026-10-17 00:00:00", ["KlantNaam"] = "Sofie Claes",
                ["TotaalInclBtw"] = "245.0", ["VoorschotBedrag"] = "100.0", ["Status"] = "KlaarVoorExport",
                ["DocumentType"] = "Bestelbon"
            });
            var oudeBonId = await InsertAsync(conn, "Facturen", new()
            {
                ["Jaar"] = 2025, ["VolgNr"] = 7, ["FactuurNummer"] = "2025-7", ["FactuurDatum"] = "2025-03-01 00:00:00",
                ["KlantNaam"] = "Oude klant", ["TotaalInclBtw"] = "50.0", ["Status"] = "Betaald", ["DocumentType"] = "Factuur"
            });
            await InsertAsync(conn, "FactuurLijnen", new()
            {
                ["FactuurId"] = bonId, ["Omschrijving"] = "Kader 50×70", ["Aantal"] = "1.0", ["TotaalIncl"] = "245.0", ["Sortering"] = 1
            });
            await InsertAsync(conn, "Ontvangsten", new()
            {
                ["FactuurId"] = bonId, ["OfferteId"] = offerteId, ["Soort"] = "Betaling", ["Betaalwijze"] = "Cash",
                ["BedragIncl"] = "45.0", ["Datum"] = "2026-10-31 00:00:00", ["Omschrijving"] = "Deelbetaling"
            });
            await InsertAsync(conn, "AuditLogs", new() { ["EntiteitType"] = "Factuur", ["EntiteitId"] = bonId.ToString(), ["Actie"] = "Gewijzigd" });
            await InsertAsync(conn, "AuditLogs", new() { ["EntiteitType"] = "FactuurLijn", ["EntiteitId"] = "1", ["Actie"] = "Toegevoegd" });
            await InsertAsync(conn, "AuditLogs", new() { ["EntiteitType"] = "Klant", ["EntiteitId"] = klantId.ToString(), ["Actie"] = "Gewijzigd" });
            await conn.CloseAsync();

            await migrator.MigrateAsync();
        }

        // ── Na de migratie: alles terug via het (nieuwe) model ──
        await using (var db = new AppDbContext(options))
        {
            var bon = await db.Bestelbonnen.AsNoTracking()
                .Include(b => b.Lijnen)
                .SingleAsync(b => b.BestelbonNummer == "2026-23");
            Assert.Equal(new DateTime(2026, 10, 17), bon.BestelbonDatum);
            Assert.Equal((2026, 23), (bon.Jaar, bon.VolgNr));
            Assert.Equal(245m, bon.TotaalInclBtw);
            Assert.Equal(100m, bon.VoorschotBedrag);
            Assert.Equal(BestelbonStatus.KlaarVoorExport, bon.Status);
            Assert.NotNull(bon.OfferteId);
            Assert.NotNull(bon.WerkBonId);

            var lijn = Assert.Single(bon.Lijnen);
            Assert.Equal("Kader 50×70", lijn.Omschrijving);

            var betaling = Assert.Single(await db.Ontvangsten.AsNoTracking().Where(o => o.BestelbonId == bon.Id).ToListAsync());
            Assert.Equal(45m, betaling.BedragIncl);
            Assert.Equal(Betaalwijze.Cash, betaling.Betaalwijze);

            // AC 4: oude bon met documenttype "Factuur" is omgezet.
            var oud = await db.Bestelbonnen.AsNoTracking().SingleAsync(b => b.BestelbonNummer == "2025-7");
            Assert.Equal("Bestelbon", oud.DocumentType);
            Assert.Equal(2, await db.Bestelbonnen.CountAsync());

            // Auditlog volgt mee; andere entiteiten blijven ongemoeid.
            var types = await db.AuditLogs.AsNoTracking().Select(a => a.EntiteitType).OrderBy(t => t).ToListAsync();
            Assert.Equal(new[] { "Bestelbon", "BestelbonLijn", "Klant" }, types);

            // Sleutels werken nog: een lijn bij een onbestaande bon wordt geweigerd…
            db.BestelbonLijnen.Add(new BestelbonLijn { BestelbonId = 9999, Omschrijving = "x" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = new AppDbContext(options))
        {
            // …en een bon verwijderen zet de betaling op NULL (SetNull) en verwijdert de lijnen (Cascade).
            var bon = await db.Bestelbonnen.SingleAsync(b => b.BestelbonNummer == "2025-7");
            db.Bestelbonnen.Remove(bon);
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Bestelbonnen.CountAsync());

            // Oude tabellen zijn weg, de nieuwe indexen bestaan.
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            await conn.OpenAsync();
            Assert.Equal(0L, await ScalarAsync(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Facturen','FactuurLijnen')"));
            Assert.Equal(1L, await ScalarAsync(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Bestelbonnen_BestelbonNummer'"));
            Assert.Equal(1L, await ScalarAsync(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_Ontvangsten_BestelbonId'"));
        }
    }

    [Fact]
    public async Task Opstarten_na_de_migratie_werkt_nog_de_healer_overslaat_de_oude_tabel()
    {
        // De healer (SqliteSchemaPatcher) draait bij elke start. Na de hernoeming mag hij niet meer
        // proberen kolommen aan "Facturen" toe te voegen (die tabel bestaat dan niet meer).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "opstarten.db")}")
            .Options;

        await using var db = new AppDbContext(options);
        await SqliteSchemaPatcher.PatchAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);   // verse db
        await SqliteSchemaPatcher.PatchAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);   // tweede start

        Assert.Equal(0, await db.Bestelbonnen.CountAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    // ── Hulp: rij invoegen met dummywaarden voor verplichte kolommen zonder standaardwaarde ──

    private static async Task<long> InsertAsync(SqliteConnection conn, string tabel, Dictionary<string, object> waarden)
    {
        await using (var info = conn.CreateCommand())
        {
            info.CommandText = $"SELECT name, type, \"notnull\", dflt_value, pk FROM pragma_table_info('{tabel}')";
            await using var r = await info.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var naam = r.GetString(0);
                var verplicht = r.GetInt64(2) == 1 && r.IsDBNull(3) && r.GetInt64(4) == 0;
                if (!verplicht || waarden.ContainsKey(naam)) continue;
                waarden[naam] = r.GetString(1).ToUpperInvariant() switch
                {
                    "INTEGER" => 0,
                    "BLOB" => Array.Empty<byte>(),
                    _ => naam.Contains("Datum") || naam.EndsWith("Op") ? "2026-10-01 00:00:00" : "0"
                };
            }
        }

        await using var cmd = conn.CreateCommand();
        var kolommen = waarden.Keys.ToList();
        cmd.CommandText = $"INSERT INTO \"{tabel}\" ({string.Join(", ", kolommen.Select(k => $"\"{k}\""))}) " +
                          $"VALUES ({string.Join(", ", kolommen.Select((_, i) => $"$p{i}"))}); SELECT last_insert_rowid();";
        for (var i = 0; i < kolommen.Count; i++)
            cmd.Parameters.AddWithValue($"$p{i}", waarden[kolommen[i]]);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> ScalarAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
