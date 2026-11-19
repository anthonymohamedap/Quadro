using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// T-06 uit docs/analyse/Codebase_Analyse_2026-11.md — upgrade van een database van <c>main</c> naar <c>dev</c>.
/// <c>dev</c> brengt twee migraties mee (Verkoopdossier, Momentopname) die ouder zijn dan migraties die
/// via <c>main</c> al in productie staan. EF past ontbrekende migraties toe ongeacht hun datum; deze test
/// bewijst dat dat op SQLite werkt zonder gegevensverlies. PostgreSQL wordt hier niet getest (geen server
/// in de testen, zie T-05); de Npgsql-migraties bevatten dezelfde bewerkingen (enkel kolommen erbij).
/// </summary>
public class UpgradeMainNaarDevTests : IDisposable
{
    /// <summary>Migraties die enkel op dev staan (op main ontbreken ze).</summary>
    private static readonly string[] EnkelOpDev = { "_Verkoopdossier", "_Momentopname" };

    private readonly string _map = Path.Combine(Path.GetTempPath(), "QuadroUpgrade_" + Guid.NewGuid().ToString("N"));

    public UpgradeMainNaarDevTests() => Directory.CreateDirectory(_map);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_map, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={Path.Combine(_map, "upgrade.db")}").Options);

    [Fact]
    public void Migraties_enkel_op_dev_voegen_alleen_kolommen_toe()
    {
        using var db = Context();
        var assembly = db.GetService<IMigrationsAssembly>();
        foreach (var naam in EnkelOpDev)
        {
            var id = assembly.Migrations.Keys.Single(k => k.EndsWith(naam, StringComparison.Ordinal));
            var migratie = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
            Assert.All(migratie.UpOperations, op =>
            {
                var kolom = Assert.IsType<AddColumnOperation>(op);
                Assert.Equal("Offertes", kolom.Table);
            });
        }
    }

    [Fact]
    public async Task Database_van_main_upgradet_naar_dev_zonder_gegevensverlies()
    {
        await using (var db = Context())
        {
            // 1. Database zoals op main: alle migraties behalve de twee van dev, in volgorde.
            var assembly = db.GetService<IMigrationsAssembly>();
            var history = db.GetService<IHistoryRepository>();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            var executor = db.GetService<IMigrationCommandExecutor>();
            var connection = db.GetService<IRelationalConnection>();
            var initializer = db.GetService<IModelRuntimeInitializer>();

            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
            foreach (var (id, type) in assembly.Migrations.OrderBy(m => m.Key))
            {
                if (EnkelOpDev.Any(n => id.EndsWith(n, StringComparison.Ordinal))) continue;
                var migratie = assembly.CreateMigration(type, db.Database.ProviderName!);
                IModel? doel = migratie.TargetModel is IMutableModel m
                    ? initializer.Initialize(m.FinalizeModel())
                    : migratie.TargetModel;
                var opdrachten = generator.Generate(migratie.UpOperations, doel);
                await executor.ExecuteNonQueryAsync(opdrachten, connection);
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "9.0.9")));
            }

            var nogTeDoen = (await db.Database.GetPendingMigrationsAsync()).ToList();
            Assert.Equal(EnkelOpDev.Length, nogTeDoen.Count);

            // Gegevens zoals in productie: een offerte met nummer en totaal (enkel kolommen van main).
            await InsertAsync(db, "Klanten", new() { ["Voornaam"] = "Veerle", ["Achternaam"] = "Gobien" });
            await InsertAsync(db, "Offertes", new()
            {
                ["KlantId"] = 1, ["OfferteNummer"] = 412, ["Datum"] = "2026-11-01 00:00:00",
                ["TotaalInclBtw"] = "121.00", ["Opmerking"] = "uit main"
            });
        }

        // 2. De app van dev start: de twee ontbrekende (oudere) migraties worden toegepast.
        await using (var db = Context())
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());

            var offerte = await db.Offertes.AsNoTracking().SingleAsync();
            Assert.Equal(412, offerte.OfferteNummer);
            Assert.Equal(121.00m, offerte.TotaalInclBtw);
            Assert.Null(offerte.AfgerondOp);
            Assert.Null(offerte.GeannuleerdOp);
            Assert.Equal("uit main", offerte.Opmerking);
        }
    }

    /// <summary>Rij invoegen via SQL; verplichte kolommen zonder standaardwaarde krijgen een neutrale waarde
    /// (zelfde aanpak als in <see cref="OfferteOpslaanHoudtNummerTests"/>).</summary>
    private static async Task InsertAsync(AppDbContext db, string tabel, Dictionary<string, object> waarden)
    {
        var conn = (SqliteConnection)db.Database.GetDbConnection();
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
                    _ => naam.Contains("Datum") || naam.EndsWith("Op") ? "2026-11-01 00:00:00" : "0"
                };
            }
        }
        await using var cmd = conn.CreateCommand();
        var kolommen = waarden.Keys.ToList();
        cmd.CommandText = $"INSERT INTO \"{tabel}\" ({string.Join(", ", kolommen.Select(k => $"\"{k}\""))}) " +
                          $"VALUES ({string.Join(", ", kolommen.Select((_, i) => $"$p{i}"))})";
        for (var i = 0; i < kolommen.Count; i++) cmd.Parameters.AddWithValue($"$p{i}", waarden[kolommen[i]]);
        await cmd.ExecuteNonQueryAsync();
    }
}
