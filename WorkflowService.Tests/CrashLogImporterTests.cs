using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Foutlog;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>crash.log (oud en nieuw formaat) wordt ingelezen in FoutLogs, zonder dubbels en zonder het bestand te wijzigen.</summary>
[Collection("CrashContext")]
public class CrashLogImporterTests : IDisposable
{
    private static readonly string Hash = new('a', 64);
    private static readonly DateTime Nu = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuadroCrashImport_" + Guid.NewGuid().ToString("N"));
    private string Pad => Path.Combine(_dir, "crash.log");

    public CrashLogImporterTests()
    {
        Directory.CreateDirectory(_dir);
        CrashContext.Leeg();
    }

    public void Dispose()
    {
        CrashContext.Leeg();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private const string OudBlok =
        "[07/10/2026 00:32:11] System.InvalidOperationException: kapot\n   at A.B() in C:\\x.cs:line 5\n\n";

    private static string NieuwBlok(string hash = "") =>
        "[2026-11-09 14:00:01] Bron: UI-thread (enkel toast)\nScherm: HomeViewModel\nLaatste acties:\n  [13:59:58] klik 'X' (Home)\n" +
        (hash.Length > 0 ? $"Hash: {hash}\n" : "") +
        "System.NullReferenceException: nul\n   at C.D()\n\n";

    private static string Vol() =>
        "[Config] iets\n" + OudBlok + "[DB] ruis\n" + NieuwBlok(Hash) + "[Config] nog iets\n";

    // ── Parse ──

    [Fact]
    public void Parse_OudEnNieuwFormaatMetRuisertussen()
    {
        var r = CrashLogImporter.Parse(Vol(), "PC1");

        Assert.Equal(2, r.Count);
        Assert.Equal("System.InvalidOperationException", r[0].ExceptionType);
        Assert.Equal("kapot", r[0].Melding);
        Assert.Equal("Geïmporteerd", r[0].Bron);
        Assert.Equal(2026, r[0].Tijdstip.Year);
        Assert.Equal(64, r[0].Hash.Length);
        Assert.Equal("PC1", r[0].Computernaam);
        Assert.Contains("at A.B()", r[0].Stacktrace);
        Assert.DoesNotContain("[DB]", r[0].Stacktrace);

        Assert.Equal("System.NullReferenceException", r[1].ExceptionType);
        Assert.Equal("Geïmporteerd (UI-thread)", r[1].Bron);   // toelichting weggelaten
        Assert.Equal("HomeViewModel", r[1].Scherm);
        Assert.Contains("klik 'X' (Home)", r[1].LaatsteKlikken);
        Assert.Equal(Hash, r[1].Hash);
        Assert.DoesNotContain("[Config]", r[1].Stacktrace);
    }

    [Fact]
    public void Parse_CrLf_WerktOok()
    {
        var r = CrashLogImporter.Parse(Vol().Replace("\n", "\r\n"), "PC1");
        Assert.Equal(2, r.Count);
        Assert.Equal("kapot", r[0].Melding);
    }

    [Fact]
    public void Parse_NieuwBlokZonderHashRegel_BerekentHash()
    {
        var r = CrashLogImporter.Parse(NieuwBlok(), "PC1").Single();
        Assert.Equal(64, r.Hash.Length);
        Assert.NotEqual(Hash, r.Hash);
    }

    [Fact]
    public void Parse_HalveBlokken_HeaderZonderInhoudWordtOvergeslagen()
    {
        var tekst =
            "[07/10/2026 00:32:11] \n" +                                    // enkel header
            "[2026-11-09 14:05:00] Bron: x\nScherm: y\nLaatste acties: (geen)\n" +  // geen exception
            "[2026-11-09 14:10:00] System.Exception: afgekapt\n   at A.B(";     // afgekapte stack op EOF

        var r = CrashLogImporter.Parse(tekst, "PC1");

        var enige = Assert.Single(r);
        Assert.Equal("afgekapt", enige.Melding);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n")]
    [InlineData("willekeurige tekst zonder blokken")]
    [InlineData("[Config] enkel config\n[DB] enkel db\n")]
    public void Parse_GeenBlokken_GeeftLeeg(string tekst)
        => Assert.Empty(CrashLogImporter.Parse(tekst, "PC1"));

    [Fact]
    public void Parse_OnbekendeEersteRegel_TypeOnbekend()
    {
        var r = CrashLogImporter.Parse("[2026-11-09 14:10:00] ?? raar !!\n   at A.B()\n", "PC1").Single();
        Assert.Equal("Onbekend", r.ExceptionType);
        Assert.Equal("?? raar !!", r.Melding);
    }

    [Fact]
    public void Parse_FiltertGevoeligeTekst()
    {
        var r = CrashLogImporter.Parse("[2026-11-09 14:10:00] System.Exception: mail jan@x.be Password=abc;\n   at A.B()\n", "PC1").Single();
        Assert.DoesNotContain("jan@x.be", r.Melding);
        Assert.DoesNotContain("jan@x.be", r.Stacktrace);
        Assert.DoesNotContain("abc", r.Stacktrace);
    }

    // ── LeesStaart ──

    [Fact]
    public void LeesStaart_BestandBestaatNiet_GeeftLeeg()
        => Assert.Equal("", CrashLogImporter.LeesStaart(Path.Combine(_dir, "nietbestaand.log")));

    [Fact]
    public void LeesStaart_KleinBestand_GeeftAlles()
    {
        File.WriteAllText(Pad, Vol());
        Assert.Equal(Vol(), CrashLogImporter.LeesStaart(Pad));
    }

    [Fact]
    public void LeesStaart_GrootBestand_StartBijBlokkop_EnParseertZonderCrash()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 60; i++)
            sb.Append($"[2026-11-09 14:{i:00}:00] System.Exception: fout {i}\n   at A.B{i}() in C:\\x.cs:line {i}\n   at C.D()\n\n");
        File.WriteAllText(Pad, sb.ToString());
        var lengte = new FileInfo(Pad).Length;

        var staart = CrashLogImporter.LeesStaart(Pad, 500);

        Assert.NotEmpty(staart);
        Assert.True(staart.Length < lengte);
        Assert.Matches(@"^\[2026-11-09 14:\d\d:00\] System\.Exception: fout \d+\n", staart);
        Assert.EndsWith(sb.ToString()[^20..], staart);
        var blokken = CrashLogImporter.Parse(staart, "PC1");
        Assert.NotEmpty(blokken);
        Assert.All(blokken, b => Assert.StartsWith("fout ", b.Melding));
    }

    [Fact]
    public void LeesStaart_GrootBestandZonderBlokkop_GeeftLeeg()
    {
        File.WriteAllText(Pad, new string('x', 5000));
        Assert.Equal("", CrashLogImporter.LeesStaart(Pad, 100));
    }

    [Fact]
    public void LeesStaart_BestandInGebruikDoorAnderProces_LeestToch()
    {
        File.WriteAllText(Pad, Vol());
        using var open = new FileStream(Pad, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        Assert.Equal(Vol(), CrashLogImporter.LeesStaart(Pad));
    }

    // ── ImporteerAsync ──

    [Fact]
    public async Task Importeer_VoegtNieuweBlokkenToe_EnLaatBestandOngemoeid()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        File.WriteAllText(Pad, Vol());
        var voor = File.ReadAllBytes(Pad);

        var n = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(2, n);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rijen = await ctx.FoutLogs.OrderBy(f => f.Id).ToListAsync();
        Assert.Equal(new[] { "Geïmporteerd", "Geïmporteerd (UI-thread)" }, rijen.Select(r => r.Bron));
        Assert.Equal("HomeViewModel", rijen[1].Scherm);
        Assert.Equal(voor, File.ReadAllBytes(Pad));
    }

    [Fact]
    public async Task Importeer_TweeKeer_GeenDubbels()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        File.WriteAllText(Pad, Vol());

        var eerste = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);
        var tweede = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(2, eerste);
        Assert.Equal(0, tweede);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal(2, await ctx.FoutLogs.CountAsync());
    }

    [Fact]
    public async Task Importeer_DubbelBlokInBestand_EenRij()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        File.WriteAllText(Pad, OudBlok + OudBlok);

        var n = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(1, n);
    }

    [Fact]
    public async Task Importeer_GeenBestand_GeeftNul()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        Assert.Equal(0, await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu));
    }

    [Fact]
    public async Task Importeer_LaatsteLiveGeschrevenFout_DaarnaGeimporteerd_BlijftEenRij()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        CrashContext.NoteerScherm(new object());
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1");
        Exception ex;
        try { throw new InvalidOperationException("live"); } catch (Exception e) { ex = e; }
        var nuLokaal = Nu.ToLocalTime().AddHours(-1);

        await reg.Registreer(ex, FoutLogBron.UiThread, nuLokaal, "toelichting");
        var n = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(0, n);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Equal(FoutLogBron.UiThread, rij.Bron);   // de live-versie blijft staan
    }

    [Fact]
    public async Task Importeer_FoutNietLiveInDb_WelGeimporteerd_MetOverGenomenSchermEnKlikken()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        CrashContext.NoteerScherm(new object());
        CrashContext.NoteerActie("klik 'Opslaan' (Offerte)");
        var reg = new FoutRegistratie(Pad, () => null, () => null, "PC1", "1");   // DB onbereikbaar
        Exception ex;
        try { throw new InvalidOperationException("offline"); } catch (Exception e) { ex = e; }

        await reg.Registreer(ex, FoutLogBron.Achtergrondthread, Nu.ToLocalTime().AddHours(-2), "x");
        var n = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(1, n);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Equal("Geïmporteerd (Achtergrondthread)", rij.Bron);
        Assert.Equal("Object", rij.Scherm);
        Assert.Contains("klik 'Opslaan' (Offerte)", rij.LaatsteKlikken);
    }

    [Fact]
    public async Task Importeer_OuderDan180Dagen_WordtOvergeslagen()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        File.WriteAllText(Pad, "[2026-01-01 10:00:00] System.Exception: oud\n   at A.B()\n\n" +
                               "[2026-11-01 10:00:00] System.Exception: recent\n   at A.B()\n\n");

        var n = await CrashLogImporter.ImporteerAsync(db.Factory, Pad, "PC1", CancellationToken.None, Nu);

        Assert.Equal(1, n);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal("recent", (await ctx.FoutLogs.SingleAsync()).Melding);
    }
}
