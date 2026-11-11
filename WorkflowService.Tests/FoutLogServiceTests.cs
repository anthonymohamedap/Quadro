using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service.Foutlog;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using QuadroApp.ViewModels.Gebruikers;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>FoutLogService, opruimen en het beheerdersscherm Foutmeldingen: rechten, volgorde, filter, kopieren.</summary>
[Collection("StaticAuthState")]
public class FoutLogServiceTests
{
    private static readonly DateTime Nu = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static FoutLog Maak(string hash, DateTime utc, bool bekeken = false, string melding = "m") => new()
    {
        Tijdstip = utc,
        Bron = "UI-thread",
        Melding = melding,
        ExceptionType = "System.Exception",
        Stacktrace = "System.Exception: " + melding + "\n   at A.B()",
        Hash = hash,
        IsBekeken = bekeken,
        Scherm = "HomeViewModel",
        LaatsteKlikken = "[10:00:00] klik 'Opslaan' (Offerte)",
    };

    private static async Task<IAuthService> LoginAsync(SqliteTestDatabase db, GebruikersRol rol)
    {
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.Gebruikers.Add(new Gebruiker
            {
                GebruikersNaam = "u",
                VolledigeNaam = "U",
                WachtwoordHash = PasswordHasher.Hash("wachtwoord1"),
                Rol = rol,
            });
            await ctx.SaveChangesAsync();
        }
        var auth = new AuthService(db.Factory, NullLogger<AuthService>.Instance);
        Assert.Null(await auth.LoginAsync("u", "wachtwoord1"));
        return auth;
    }

    private static async Task Seed(SqliteTestDatabase db, params FoutLog[] logs)
    {
        await using var ctx = await db.Factory.CreateDbContextAsync();
        ctx.FoutLogs.AddRange(logs);
        await ctx.SaveChangesAsync();
    }

    // ── Rechten ──

    [Fact]
    public async Task IsZichtbaarVoor_AdminJa_MedewerkerNee()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var admin = await LoginAsync(db, GebruikersRol.Admin);
        Assert.True(FoutmeldingenViewModel.IsZichtbaarVoor(admin));
        admin.Logout();

        // tweede gebruiker met andere naam
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.Gebruikers.Add(new Gebruiker { GebruikersNaam = "m", VolledigeNaam = "M", WachtwoordHash = PasswordHasher.Hash("wachtwoord1"), Rol = GebruikersRol.Medewerker });
            await ctx.SaveChangesAsync();
        }
        Assert.Null(await admin.LoginAsync("m", "wachtwoord1"));
        Assert.False(FoutmeldingenViewModel.IsZichtbaarVoor(admin));
    }

    [Fact]
    public async Task IsZichtbaarVoor_NietIngelogd_Nee()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var auth = new AuthService(db.Factory, NullLogger<AuthService>.Instance);
        Assert.False(FoutmeldingenViewModel.IsZichtbaarVoor(auth));
    }

    [Fact]
    public async Task Medewerker_KanNietLijstenOfMarkeren()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu));
        var auth = await LoginAsync(db, GebruikersRol.Medewerker);
        var sut = new FoutLogService(db.Factory, auth);

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.LijstAsync(false));
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.MarkeerBekekenAsync(1));

        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.False((await ctx.FoutLogs.SingleAsync()).IsBekeken);
    }

    // ── Service ──

    [Fact]
    public async Task Lijst_Admin_NieuwsteEerst()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu.AddHours(-3), melding: "oud"), Maak("h2", Nu, melding: "nieuw"), Maak("h3", Nu.AddHours(-1), melding: "midden"));
        var sut = new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin));

        var lijst = await sut.LijstAsync(false);

        Assert.Equal(new[] { "nieuw", "midden", "oud" }, lijst.Select(l => l.Melding));
    }

    [Fact]
    public async Task Lijst_EnkelNietBekeken_Filtert()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu, bekeken: true, melding: "gezien"), Maak("h2", Nu.AddHours(-1), melding: "open"));
        var sut = new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin));

        Assert.Equal(new[] { "open" }, (await sut.LijstAsync(true)).Select(l => l.Melding));
        Assert.Equal(2, (await sut.LijstAsync(false)).Count);
    }

    [Fact]
    public async Task Lijst_BeperktTot500Rijen()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Enumerable.Range(0, 520).Select(i => Maak($"h{i}", Nu.AddMinutes(-i))).ToArray());
        var sut = new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin));

        Assert.Equal(500, (await sut.LijstAsync(false)).Count);
    }

    [Fact]
    public async Task MarkeerBekeken_ZetIsBekeken_EnOnbekendIdIsGeenFout()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu));
        var sut = new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin));
        int id;
        await using (var ctx = await db.Factory.CreateDbContextAsync()) id = (await ctx.FoutLogs.SingleAsync()).Id;

        await sut.MarkeerBekekenAsync(id);
        await sut.MarkeerBekekenAsync(id);       // al bekeken: niets
        await sut.MarkeerBekekenAsync(99999);    // bestaat niet: niets

        await using var ctx2 = await db.Factory.CreateDbContextAsync();
        Assert.True((await ctx2.FoutLogs.SingleAsync()).IsBekeken);
    }

    // ── RuimOp ──

    [Fact]
    public async Task RuimOp_VerwijdertEnkelBekekenEnOuderDan180Dagen()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db,
            Maak("oud-bekeken", Nu.AddDays(-181), bekeken: true),
            Maak("oud-open", Nu.AddDays(-181), bekeken: false),
            Maak("recent-bekeken", Nu.AddDays(-179), bekeken: true),
            Maak("recent-open", Nu.AddDays(-1), bekeken: false));

        var n = await CrashLogImporter.RuimOpAsync(db.Factory, Nu, CancellationToken.None);

        Assert.Equal(1, n);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var over = (await ctx.FoutLogs.Select(f => f.Hash).ToListAsync()).OrderBy(h => h).ToList();
        Assert.Equal(new[] { "oud-open", "recent-bekeken", "recent-open" }, over);
    }

    [Fact]
    public async Task RuimOp_LegeTabel_GeeftNul()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        Assert.Equal(0, await CrashLogImporter.RuimOpAsync(db.Factory, Nu, CancellationToken.None));
    }

    // ── ViewModel ──

    private sealed class FakeService : IFoutLogService
    {
        public bool Faalt { get; set; }
        public Task<System.Collections.Generic.List<FoutLog>> LijstAsync(bool enkelNietBekeken, CancellationToken ct = default)
            => Faalt ? throw new InvalidOperationException("stuk") : Task.FromResult(new System.Collections.Generic.List<FoutLog>());
        public Task MarkeerBekekenAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ViewModel_Laden_ToontRijen_EnKopieerGeeftStacktraceEnKlikken()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu));
        var vm = new FoutmeldingenViewModel(new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin)));
        string? gekopieerd = null;
        vm.Klembord = t => { gekopieerd = t; return Task.CompletedTask; };

        await vm.LadenCommand.ExecuteAsync(null);
        Assert.Single(vm.Rijen);
        Assert.False(vm.IsLeeg);
        vm.Geselecteerd = vm.Rijen[0];
        await vm.KopieerCommand.ExecuteAsync(null);

        Assert.NotNull(gekopieerd);
        Assert.Contains("at A.B()", gekopieerd);
        Assert.Contains("klik 'Opslaan' (Offerte)", gekopieerd);
        Assert.Contains("HomeViewModel", gekopieerd);
    }

    [Fact]
    public async Task ViewModel_MarkeerBekeken_BijFilterVerdwijntRij()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu));
        var vm = new FoutmeldingenViewModel(new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin)));
        await vm.LadenCommand.ExecuteAsync(null);
        vm.EnkelNietBekeken = true;
        await vm.LadenCommand.ExecuteAsync(null);
        vm.Geselecteerd = vm.Rijen[0];

        await vm.MarkeerBekekenCommand.ExecuteAsync(null);

        Assert.Empty(vm.Rijen);
        Assert.True(vm.IsLeeg);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        Assert.True((await ctx.FoutLogs.SingleAsync()).IsBekeken);
    }

    [Fact]
    public async Task ViewModel_LadenMislukt_ToontFoutmelding()
    {
        var vm = new FoutmeldingenViewModel(new FakeService { Faalt = true });
        await vm.LadenCommand.ExecuteAsync(null);
        Assert.Contains("stuk", vm.Foutmelding);
        Assert.False(vm.IsBezig);
    }

    [Fact]
    public async Task ViewModel_KlembordFaalt_ToontFoutmelding_ZonderCrash()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await Seed(db, Maak("h1", Nu));
        var vm = new FoutmeldingenViewModel(new FoutLogService(db.Factory, await LoginAsync(db, GebruikersRol.Admin)));
        vm.Klembord = _ => throw new InvalidOperationException("klembord weg");
        await vm.LadenCommand.ExecuteAsync(null);
        vm.Geselecteerd = vm.Rijen[0];

        await vm.KopieerCommand.ExecuteAsync(null);

        Assert.Contains("klembord weg", vm.Foutmelding);
    }

    [Fact]
    public void ViewModel_ZonderSelectie_GeenKopieerOfMarkeer()
    {
        var vm = new FoutmeldingenViewModel(new FakeService());
        Assert.False(vm.KopieerCommand.CanExecute(null));
        Assert.False(vm.MarkeerBekekenCommand.CanExecute(null));
        Assert.Equal("Kies links een foutmelding.", vm.DetailTekst);
    }

    private sealed class TragereEerste : IFoutLogService
    {
        private int _n;
        public TaskCompletionSource<bool> Eerste { get; } = new();
        public async Task<System.Collections.Generic.List<FoutLog>> LijstAsync(bool enkelNietBekeken, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _n) == 1)
            {
                await Eerste.Task;
                return new() { Maak("oud", Nu) };
            }
            return new();
        }
        public Task MarkeerBekekenAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ViewModel_OudereTragereLading_OverschrijftNieuwereNiet()
    {
        var svc = new TragereEerste();
        var vm = new FoutmeldingenViewModel(svc);

        var eerste = vm.LadenCommand.ExecuteAsync(null);      // blijft hangen
        await vm.LadenCommand.ExecuteAsync(null);             // nieuwere: leeg resultaat
        svc.Eerste.SetResult(true);                           // oudere komt nu pas binnen
        await eerste;

        Assert.Empty(vm.Rijen);
        Assert.False(vm.IsBezig);
    }
}
