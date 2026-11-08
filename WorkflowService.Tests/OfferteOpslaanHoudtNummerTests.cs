using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// Fix "Werkbon 0": een bestaande offerte opslaan schreef alle velden weg, ook die niet in de
/// opslag-kopie stonden; het offertenummer werd zo 0. Opslaan mag enkel de velden van het scherm wijzigen.
/// </summary>
public class OfferteOpslaanHoudtNummerTests
{
    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Bestaande_offerte_opslaan_houdt_het_offertenummer()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var f = scope.Factory;
        int id;
        await using (var db = await f.CreateDbContextAsync())
        {
            foreach (var (c, n) in new[] { ('G', "Glas"), ('P', "Passe-partout"), ('D', "Diepte"), ('O', "Opkleven"), ('R', "Rug") })
                db.AfwerkingsGroepen.Add(new AfwerkingsGroep { Code = c, Naam = n });
            var offerte = new Offerte
            {
                OfferteNummer = 412, Datum = DateTime.Today, Status = OfferteStatus.Concept,
                Klant = new Klant { Voornaam = "Marleen", Achternaam = "Peeters" },
                Regels = { new OfferteRegel { Titel = "Trouwfoto", AantalStuks = 1, BreedteCm = 50, HoogteCm = 70 } }
            };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();
            id = offerte.Id;
        }

        var toast = new TestToastService();
        var auth = new TestAuthService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var pricing = new PricingService(f, new PricingSettingsProvider(f), new PricingEngine(), NullLogger<PricingService>.Instance);
        var bestelbonWf = new BestelbonWorkflowService(f, pricing, auth);
        var vm = new OfferteViewModel(f, new TestNavigationService(), new TestDialogService(), pricing,
            new OfferteWorkflowService(f, wf), new WerkBonWorkflowService(f, wf), wf, bestelbonWf,
            new BestelbonExportService(f, Array.Empty<IBestelbonExporter>()), new TestFilePickerService(),
            new OfferteValidator(f), toast, new KlantValidator(f), new KlantDialogService(),
            new OntvangstService(f, auth, bestelbonWf), new TestPathOpener(), new GeenOfferteNavigatie(), auth);
        await vm.InitializeAsync();
        await vm.LoadAsync(id);

        vm.Regels.First().Titel = "Trouwfoto groot";
        await vm.SaveCoreAsync(reloadAfterSave: true);
        await vm.SaveCoreAsync(reloadAfterSave: true);   // twee keer: ook na herladen

        await using var check = await f.CreateDbContextAsync();
        var bewaard = await check.Offertes.Include(o => o.Regels).SingleAsync(o => o.Id == id);
        Assert.Equal(412, bewaard.OfferteNummer);
        Assert.Equal("Trouwfoto groot", bewaard.Regels.Single().Titel);
    }

    [Fact]
    public async Task Migratie_herstelt_nummers_0_die_door_opslaan_ontstonden()
    {
        var map = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuadroNummerOpslaan_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(map);
        try
        {
            var options = new DbContextOptionsBuilder<QuadroApp.Data.AppDbContext>()
                .UseSqlite($"Data Source={System.IO.Path.Combine(map, "n.db")}").Options;
            await using var db = new QuadroApp.Data.AppDbContext(options);
            var migraties = db.Database.GetMigrations().ToList();
            var index = migraties.FindIndex(m => m.EndsWith("_OfferteNummerNulNaOpslaan", StringComparison.Ordinal));
            Assert.True(index > 0, "Migratie OfferteNummerNulNaOpslaan ontbreekt.");
            var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
                .GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
            await migrator.MigrateAsync(migraties[index - 1]);

            // Via SQL: het EF-model kan intussen kolommen hebben die op dit schema nog niet bestaan.
            await InsertAsync(db, "Offertes", new() { ["OfferteNummer"] = 50, ["Datum"] = "2026-10-01 00:00:00", ["Opmerking"] = "vijftig" });
            await InsertAsync(db, "Offertes", new() { ["OfferteNummer"] = 0, ["Datum"] = "2026-10-01 00:00:00", ["Opmerking"] = "gewist" });

            await migrator.MigrateAsync();

            var nummers = await db.Offertes.AsNoTracking().ToDictionaryAsync(o => o.Opmerking!, o => o.OfferteNummer);
            Assert.Equal(50, nummers["vijftig"]);
            Assert.True(nummers["gewist"] > 50);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { System.IO.Directory.Delete(map, true); } catch (System.IO.IOException) { }
        }
    }

    private static async Task InsertAsync(QuadroApp.Data.AppDbContext db, string tabel, System.Collections.Generic.Dictionary<string, object> waarden)
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
