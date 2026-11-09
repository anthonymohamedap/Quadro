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

/// <summary>Migratie FoutLogs: tabel + unieke index op Hash (SQLite via migrator, Npgsql via de bronbestanden).</summary>
public class FoutLogMigratieTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuadroFoutLogMigratie_" + Guid.NewGuid().ToString("N"));

    public FoutLogMigratieTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task Sqlite_Migratie_MaaktTabelMetUniekeHashIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "foutlogs.db")}").Options;

        await using var db = new AppDbContext(options);
        var migraties = db.Database.GetMigrations().ToList();
        var index = migraties.FindIndex(m => m.EndsWith("_FoutLogs", StringComparison.Ordinal));
        Assert.True(index > 0, "Migratie FoutLogs ontbreekt.");

        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(migraties[index - 1]);

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        Assert.Equal(0L, await Scalar(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FoutLogs'"));
        await conn.CloseAsync();

        await migrator.MigrateAsync();

        await conn.OpenAsync();
        Assert.Equal(1L, await Scalar(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FoutLogs'"));
        Assert.Equal(1L, await Scalar(conn,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_FoutLogs_Hash' AND sql LIKE 'CREATE UNIQUE INDEX%'"));
        await conn.CloseAsync();

        db.FoutLogs.Add(Maak("dubbel"));
        await db.SaveChangesAsync();
        db.FoutLogs.Add(Maak("dubbel"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Sqlite_Migratie_BestaandeGegevensBlijven()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "bestaand.db")}").Options;
        await using var db = new AppDbContext(options);
        var migraties = db.Database.GetMigrations().ToList();
        var index = migraties.FindIndex(m => m.EndsWith("_FoutLogs", StringComparison.Ordinal));
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(migraties[index - 1]);

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Klanten (Voornaam, Achternaam, IsGearchiveerd) VALUES ('Sofie','Claes',0)";
            await cmd.ExecuteNonQueryAsync();
        }
        await conn.CloseAsync();

        await migrator.MigrateAsync();

        Assert.Equal(1, await db.Klanten.CountAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public void Npgsql_Migratie_BestaatEnMaaktTabelEnUniekeIndex()
    {
        var map = ZoekMap("QuadroApp.Migrations.Npgsql", "Migrations");
        var bestanden = Directory.GetFiles(map, "*_FoutLogs.cs");
        Assert.Single(bestanden);
        Assert.Single(Directory.GetFiles(map, "*_FoutLogs.Designer.cs"));

        var bron = File.ReadAllText(bestanden[0]);
        Assert.Contains("class FoutLogs : Migration", bron);
        Assert.Contains("CreateTable(", bron);
        Assert.Contains("name: \"FoutLogs\"", bron);
        Assert.Contains("name: \"IX_FoutLogs_Hash\"", bron);
        Assert.Matches(@"column: ""Hash"",\s*unique: true", bron);
        Assert.Contains("DropTable(", bron);
        Assert.Contains("\"FoutLogs\"", File.ReadAllText(Path.Combine(map, "AppDbContextModelSnapshot.cs")));
    }

    private static string ZoekMap(params string[] delen)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var kandidaat = Path.Combine(new[] { dir.FullName }.Concat(delen).ToArray());
            if (Directory.Exists(kandidaat)) return kandidaat;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(string.Join("/", delen));
    }

    private static async Task<long> Scalar(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static FoutLog Maak(string hash) => new()
    {
        Tijdstip = DateTime.UtcNow,
        Bron = "UI-thread",
        Melding = "m",
        ExceptionType = "System.Exception",
        Stacktrace = "s",
        Hash = hash,
    };
}
