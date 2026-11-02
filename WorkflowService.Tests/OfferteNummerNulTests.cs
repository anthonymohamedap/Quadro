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
using QuadroApp.Service;
using QuadroApp.ViewModels;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-81 — nooit meer "offertenr. 0", en een waarschuwing als er afgehaald wordt vóór de besteldatum.</summary>
public class OfferteNummerNulTests : IDisposable
{
    private readonly string _dir;

    public OfferteNummerNulTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "QuadroOfferteNummerTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task Migratie_geeft_offertes_met_nummer_0_een_uniek_nummer_boven_alle_bestaande()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "nummers.db")}")
            .Options;
        await using var db = new AppDbContext(options);

        var migraties = db.Database.GetMigrations().ToList();
        var index = migraties.FindIndex(m => m.EndsWith("_OfferteNummerNulCorrigeren", StringComparison.Ordinal));
        Assert.True(index > 0, "Migratie OfferteNummerNulCorrigeren ontbreekt.");

        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(migraties[index - 1]);

        // Met SQL invoegen (niet via het huidige model): latere migraties voegen kolommen toe die in
        // deze oudere database nog niet bestaan (bv. US-87).
        foreach (var (nummer, opmerking) in new[] { (7, "zeven"), (0, "nul-a"), (0, "nul-b") })
            await InsertAsync(db, "Offertes", new() { ["OfferteNummer"] = nummer, ["Datum"] = "2026-10-01 00:00:00", ["Opmerking"] = opmerking });
        await InsertAsync(db, "OfferteArchieven", new() { ["OfferteNummer"] = 12, ["OrigineleOfferteId"] = 99, ["KlantNaam"] = "Archief" });

        await migrator.MigrateAsync();

        var nummers = await db.Offertes.AsNoTracking().ToDictionaryAsync(o => o.Opmerking!, o => o.OfferteNummer);
        Assert.Equal(7, nummers["zeven"]);
        Assert.True(nummers["nul-a"] > 12);
        Assert.True(nummers["nul-b"] > 12);
        Assert.NotEqual(nummers["nul-a"], nummers["nul-b"]);
    }

    [Fact]
    public void Geen_waarschuwing_als_afhalen_op_of_na_de_besteldatum()
    {
        Assert.Null(AfhaalDatumControle.Waarschuwing(new DateTime(2026, 10, 26), new DateTime(2026, 10, 26)));
        Assert.Null(AfhaalDatumControle.Waarschuwing(new DateTime(2026, 10, 26), new DateTime(2026, 10, 30)));
        Assert.Null(AfhaalDatumControle.Waarschuwing(new DateTime(2026, 10, 26), null));
    }

    [Fact]
    public void Waarschuwing_als_afhalen_voor_de_besteldatum()
    {
        var tekst = AfhaalDatumControle.Waarschuwing(new DateTime(2026, 10, 26), new DateTime(2026, 10, 24));
        Assert.Equal("Let op: de afhaaldatum (24/10/2026) ligt vóór de besteldatum (26/10/2026).", tekst);
    }

    [Fact]
    public void Vroegste_afhaaldatum_kijkt_ook_naar_de_inlijstingen()
    {
        var bestelbon = new Bestelbon
        {
            AfhaalDatum = new DateTime(2026, 10, 30),
            Lijnen = new List<BestelbonLijn>
            {
                new() { Omschrijving = "TEST-0303 | 40x50 cm | titel:Schilderij | afhaal:2026-10-24" },
                new() { Omschrijving = "Standaardkader C2 | titel:Trouwfoto" }
            }
        };

        Assert.Equal(new DateTime(2026, 10, 24), AfhaalDatumControle.VroegsteAfhaalDatum(bestelbon));
    }

    [Fact]
    public void Bestelbon_gegevens_toont_de_waarschuwing_en_volgt_de_besteldatum()
    {
        var bestelbon = new Bestelbon
        {
            BestelbonNummer = "2026-19",
            BestelbonDatum = new DateTime(2026, 10, 26),
            VervalDatum = new DateTime(2026, 11, 25),
            AfhaalDatum = new DateTime(2026, 10, 24)
        };
        var vm = new BestelbonGegevensDialogViewModel(bestelbon);

        Assert.True(vm.HasAfhaalWaarschuwing);

        vm.BestelbonDatum = new DateTimeOffset(new DateTime(2026, 10, 23));
        Assert.False(vm.HasAfhaalWaarschuwing);
    }

    /// <summary>Rij invoegen met dummywaarden voor verplichte kolommen zonder standaardwaarde.</summary>
    private static async Task InsertAsync(AppDbContext db, string tabel, System.Collections.Generic.Dictionary<string, object> waarden)
    {
        var conn = (Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        await using (var info = conn.CreateCommand())
        {
            info.CommandText = $"SELECT name, type, \"notnull\", dflt_value, pk FROM pragma_table_info('{tabel}')";
            await using var r = await info.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var naam = r.GetString(0);
                if (r.GetInt64(2) != 1 || !r.IsDBNull(3) || r.GetInt64(4) != 0 || waarden.ContainsKey(naam)) continue;
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
        cmd.CommandText = $"INSERT INTO \"{tabel}\" ({string.Join(", ", kolommen.Select(k => $"\"{k}\""))}) VALUES ({string.Join(", ", kolommen.Select((_, i) => $"$p{i}"))})";
        for (var i = 0; i < kolommen.Count; i++) cmd.Parameters.AddWithValue($"$p{i}", waarden[kolommen[i]]);
        await cmd.ExecuteNonQueryAsync();
    }
}
