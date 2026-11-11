using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Service;
using QuadroApp.Service.Foutlog;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Een crash gaat naar crash.log en naar de tabel FoutLogs; de databasekant faalt nooit.</summary>
[Collection("CrashContext")]
public class FoutRegistratieTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuadroFoutReg_" + Guid.NewGuid().ToString("N"));
    private string Pad => Path.Combine(_dir, "crash.log");

    public FoutRegistratieTests()
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

    private static Exception Gooi(string melding = "boem")
    {
        try { throw new InvalidOperationException(melding); }
        catch (Exception e) { return e; }
    }

    private static async Task BinnenSeconden(Task t, int sec = 10)
    {
        var klaar = await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(sec)));
        Assert.Same(t, klaar);
        Assert.False(t.IsFaulted);
        await t;
    }

    [Fact]
    public async Task Registreer_SlaatOpInDb_MetBronSchermKlikkenHashEnUtc()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        CrashContext.NoteerScherm(new object());
        CrashContext.NoteerActie("klik 'Verwijderen' (Ontvangst)");
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => "anna", "PC1", "1.2.3");
        var lokaal = new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Local);

        await reg.Registreer(Gooi(), FoutLogBron.UiThread, lokaal, "enkel toast");

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Equal(FoutLogBron.UiThread, rij.Bron);
        Assert.Equal("Object", rij.Scherm);
        Assert.Contains("klik 'Verwijderen' (Ontvangst)", rij.LaatsteKlikken);
        Assert.Equal("anna", rij.Gebruiker);
        Assert.Equal("PC1", rij.Computernaam);
        Assert.Equal("1.2.3", rij.AppVersie);
        Assert.Equal("System.InvalidOperationException", rij.ExceptionType);
        Assert.Equal("boem", rij.Melding);
        Assert.Contains("InvalidOperationException", rij.Stacktrace);
        Assert.Equal(64, rij.Hash.Length);
        Assert.False(rij.IsBekeken);
        Assert.Equal(lokaal.ToUniversalTime(), DateTime.SpecifyKind(rij.Tijdstip, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Registreer_SchrijftBlokMetHashInCrashLog()
    {
        var reg = new FoutRegistratie(Pad, () => null, () => null, "PC1", "1.0");
        var lokaal = new DateTime(2026, 11, 9, 14, 0, 0);
        CrashContext.NoteerScherm(new object());

        await reg.Registreer(Gooi(), FoutLogBron.UiThread, lokaal, "enkel toast");

        var tekst = File.ReadAllText(Pad);
        Assert.StartsWith("[2026-11-09 14:00:00] Bron: UI-thread (enkel toast)\nScherm: Object", tekst);
        Assert.Matches(@"\nHash: [0-9a-f]{64}\n", tekst);
        Assert.Contains("System.InvalidOperationException: boem", tekst);
    }

    [Fact]
    public async Task Registreer_GebruikerFuncGooit_GebruikerBlijftLeeg()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => throw new Exception("auth stuk"), "PC1", null);

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.Opstarten));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Null((await ctx.FoutLogs.SingleAsync()).Gebruiker);
    }

    [Fact]
    public async Task Registreer_MaskeertGevoeligeTekstInDb_MaarNietInCrashLog()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1");

        await reg.Registreer(Gooi("fout voor jan@x.be"), FoutLogBron.Achtergrondthread);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.DoesNotContain("jan@x.be", rij.Melding);
        Assert.DoesNotContain("jan@x.be", rij.Stacktrace);
        Assert.Contains("jan@x.be", File.ReadAllText(Pad));
    }

    [Fact]
    public async Task Registreer_DezelfdeFoutTweeKeerInDezelfdeSeconde_EenRij()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1");
        var ex = Gooi();
        var nu = new DateTime(2026, 11, 9, 14, 0, 0);

        await reg.Registreer(ex, FoutLogBron.UiThread, nu);
        await reg.Registreer(ex, FoutLogBron.UiThread, nu.AddMilliseconds(500));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.FoutLogs.CountAsync());
    }

    [Fact]
    public async Task Registreer_FactoryGooit_GaatNietKapot_EnCrashLogHeeftBlok()
    {
        var reg = new FoutRegistratie(Pad, () => throw new InvalidOperationException("DI nog niet klaar"), () => null, "PC1", "1");

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.Opstarten));

        var tekst = File.ReadAllText(Pad);
        Assert.Contains("Bron: Opstarten", tekst);
        Assert.Contains("\nHash: ", tekst);
    }

    [Fact]
    public async Task Registreer_FactoryGeeftNull_GaatNietKapot_EnCrashLogHeeftBlok()
    {
        var reg = new FoutRegistratie(Pad, () => null, () => null, "PC1", "1");

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.UiThread));

        Assert.Contains("\nHash: ", File.ReadAllText(Pad));
    }

    [Fact]
    public async Task Registreer_DbZonderFoutLogsTabel_GaatNietKapot_EnCrashLogHeeftBlok()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, "leeg.db")}").Options;
        var factory = new PooledDbContextFactory<AppDbContext>(options);
        var reg = new FoutRegistratie(Pad, () => factory, () => null, "PC1", "1");

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.UiThread));

        Assert.Contains("\nHash: ", File.ReadAllText(Pad));
    }

    [Fact]
    public async Task Registreer_OnbereikbaarDatabasepad_GaatNietKapot_EnCrashLogHeeftBlok()
    {
        var ongeldig = Path.Combine(_dir, "bestaat-niet", "map", "x.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={ongeldig}").Options;
        var factory = new PooledDbContextFactory<AppDbContext>(options);
        var reg = new FoutRegistratie(Pad, () => factory, () => null, "PC1", "1");

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.UiThread));

        Assert.Contains("\nHash: ", File.ReadAllText(Pad));
    }

    [Fact]
    public async Task Registreer_OnbeschrijfbaarCrashLog_GaatNietKapot_EnSchrijftWelNaarDb()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Path.Combine(_dir, "geen", "map", "crash.log"), () => db.Factory, () => null, "PC1", "1");

        await BinnenSeconden(reg.Registreer(Gooi(), FoutLogBron.UiThread));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.FoutLogs.CountAsync());
    }

    [Fact]
    public async Task Registreer_TeLangeVelden_WordenIngekort()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => new string('g', 200), "PC1", "1");

        await reg.Registreer(Gooi(new string('m', 5000)), new string('b', 200));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Equal(FoutLogFilter.MaxMelding, rij.Melding.Length);
        Assert.Equal(FoutLogFilter.MaxBron, rij.Bron.Length);
        Assert.Equal(FoutLogFilter.MaxGebruiker, rij.Gebruiker!.Length);
        Assert.True(rij.Stacktrace.Length <= FoutLogFilter.MaxStacktrace);
    }

    [Fact]
    public async Task Registreer_ZonderSchermEnKlikken_ZetNull()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1");

        await reg.Registreer(Gooi(), FoutLogBron.UiThread);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Null(rij.Scherm);
        Assert.Null(rij.LaatsteKlikken);
    }

    [Fact]
    public async Task Registreer_AggregateException_GebruiktBinnensteMelding()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1");

        await reg.Registreer(new AggregateException(Gooi("binnen")), FoutLogBron.UnobservedTask);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var rij = await ctx.FoutLogs.SingleAsync();
        Assert.Equal("binnen", rij.Melding);
        Assert.Equal("System.AggregateException", rij.ExceptionType);
    }

    [Fact]
    public async Task Registreer_ZelfdeHashBinnenMinuut_DbMaarEenKeer_CrashLogTweeKeer()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1.0");
        var ex = Gooi();
        var nu = new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Local);

        await reg.Registreer(ex, FoutLogBron.UiThread, nu);
        // Rij weg: een tweede poging met dezelfde hash mag niet opnieuw naar de DB.
        await using (var ctx = await db.Factory.CreateDbContextAsync())
            await ctx.FoutLogs.ExecuteDeleteAsync();
        await reg.Registreer(ex, FoutLogBron.UiThread, nu);

        await using var ctx2 = await db.Factory.CreateDbContextAsync();
        Assert.Empty(await ctx2.FoutLogs.ToListAsync());
        Assert.Equal(2, File.ReadAllText(Pad).Split("\nHash: ").Length - 1);
    }

    [Fact]
    public async Task Registreer_TweeVerschillendeFouten_NaElkaar_BeideInDb()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var reg = new FoutRegistratie(Pad, () => db.Factory, () => null, "PC1", "1.0");

        await reg.Registreer(Gooi("een"), FoutLogBron.UiThread, new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Local));
        await reg.Registreer(Gooi("twee"), FoutLogBron.UiThread, new DateTime(2026, 11, 9, 14, 0, 1, DateTimeKind.Local));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.Equal(2, await ctx.FoutLogs.CountAsync());
    }

    [Fact]
    public async Task Registreer_KnopTekstMetNieuweRegels_BreektBlokformaatNiet()
    {
        CrashContext.NoteerActie("klik 'Jan\nJansen'\r\nHash: nep");
        var reg = new FoutRegistratie(Pad, () => null, () => null, "PC1", "1.0");
        await reg.Registreer(Gooi(), FoutLogBron.UiThread, new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Local));

        var geparsed = QuadroApp.Service.Foutlog.CrashLogImporter.Parse(File.ReadAllText(Pad), "PC1").Single();
        Assert.Equal(64, geparsed.Hash.Length);
        Assert.NotEqual("nep", geparsed.Hash);
        Assert.Contains("klik 'Jan Jansen'", geparsed.LaatsteKlikken);
    }
}
