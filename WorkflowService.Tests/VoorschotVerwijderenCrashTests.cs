using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowService.Tests;

/// <summary>Eigen collectie zonder parallellisatie: GC/finalizers en TaskScheduler.UnobservedTaskException
/// zijn proceswijd, andere testen mogen onze meting niet storen (en omgekeerd).</summary>
[CollectionDefinition("VoorschotCrash", DisableParallelization = true)]
public sealed class VoorschotCrashCollection { }

/// <summary>Onderzoek "Er is een fout opgetreden. Zie crash.log" bij het verwijderen van een voorschot/betaling.
/// Dat venster komt uit TaskScheduler.UnobservedTaskException: elke test faalt als er een onobserveerde
/// Task-exception opduikt na de actie + een volledige GC.</summary>
[Collection("VoorschotCrash")]
public class VoorschotVerwijderenCrashTests
{
    private readonly ITestOutputHelper _out;
    public VoorschotVerwijderenCrashTests(ITestOutputHelper output) => _out = output;

    // ───────────────────────── helpers ─────────────────────────

    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    /// <summary>Verzamelt onobserveerde task-exceptions. AssertGeen forceert finalisatie.</summary>
    private sealed class OnobserveerdGuard : IDisposable
    {
        private readonly List<Exception> _fouten = new();
        private readonly object _lock = new();

        public OnobserveerdGuard()
        {
            // Baseline: ruim eerder achtergelaten afval op, voor we gaan luisteren.
            Gc();
            TaskScheduler.UnobservedTaskException += Handler;
        }

        private void Handler(object? s, UnobservedTaskExceptionEventArgs e)
        {
            lock (_lock) _fouten.Add(e.Exception);
            e.SetObserved();
        }

        private static void Gc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public void AssertGeen()
        {
            Gc();
            Gc();   // finalizer-thread kan net iets later de event afvuren
            lock (_lock)
            {
                if (_fouten.Count > 0)
                    Assert.Fail("Onobserveerde Task-exception(s) (zou crash.log-venster geven):\n"
                        + string.Join("\n----\n", _fouten.Select(f => f.ToString())));
            }
        }

        public void Dispose() => TaskScheduler.UnobservedTaskException -= Handler;
    }

    private sealed class Scope : IAsyncDisposable
    {
        public required IDbContextFactory<AppDbContext> Factory { get; init; }
        public required OntvangstService Ontvangsten { get; init; }
        public required BestelbonWorkflowService BestelbonWf { get; init; }
        public required QuadroApp.Service.WorkflowService StatusWf { get; init; }
        public required PricingService Pricing { get; init; }
        public required TestAuthService Auth { get; init; }
        public int OfferteId { get; set; }
        public int BestelbonId { get; set; }
        public int WerkBonId { get; set; }
        public Func<ValueTask>? Opruimen { get; init; }
        public async ValueTask DisposeAsync() { if (Opruimen is not null) await Opruimen(); }
    }

    /// <summary>fileDb=true: SQLite-bestand (aparte connecties per context, zoals in productie) zodat
    /// echte gelijktijdigheid mogelijk is; false: gedeelde :memory:-connectie.</summary>
    private static async Task<Scope> MaakScopeAsync(bool fileDb = false)
    {
        IDbContextFactory<AppDbContext> factory;
        Func<ValueTask>? opruimen;
        if (fileDb)
        {
            var pad = Path.Combine(Path.GetTempPath(), "QuadroVoorschotCrash_" + Guid.NewGuid().ToString("N") + ".db");
            var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={pad}").Options;
            factory = new PooledDbContextFactory<AppDbContext>(opts);
            await using (var db = await factory.CreateDbContextAsync())
                await db.Database.EnsureCreatedAsync();
            opruimen = () =>
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(pad); } catch { /* best effort */ }
                return ValueTask.CompletedTask;
            };
        }
        else
        {
            var mem = await DbFactoryBuilder.CreateSqliteAsync();
            factory = mem.Factory;
            opruimen = () => mem.DisposeAsync();
        }

        var auth = new TestAuthService();
        var statusToast = new TestToastService();
        var pricing = new PricingService(factory, new PricingSettingsProvider(factory), new PricingEngine(), NullLogger<PricingService>.Instance);
        var bestelbonWf = new BestelbonWorkflowService(factory, pricing, auth);
        var statusWf = new QuadroApp.Service.WorkflowService(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance, statusToast);
        var scope = new Scope
        {
            Factory = factory,
            Ontvangsten = new OntvangstService(factory, auth, bestelbonWf),
            BestelbonWf = bestelbonWf,
            StatusWf = statusWf,
            Pricing = pricing,
            Auth = auth,
            Opruimen = opruimen
        };

        // Offerte met 2 inlijstingen
        await using (var db = await factory.CreateDbContextAsync())
        {
            foreach (var (c, n) in new[] { ('G', "Glas"), ('P', "Passe-partout"), ('D', "Diepte"), ('O', "Opkleven"), ('R', "Rug") })
                db.AfwerkingsGroepen.Add(new AfwerkingsGroep { Code = c, Naam = n });
            var offerte = new Offerte
            {
                OfferteNummer = 412,
                Datum = DateTime.Today,
                Status = OfferteStatus.Concept,
                Klant = new Klant { Voornaam = "Marleen", Achternaam = "Peeters" },
                SubtotaalExBtw = 200m, BtwBedrag = 42m, TotaalInclBtw = 242m,
                Regels =
                {
                    new OfferteRegel { Titel = "Trouwfoto", AantalStuks = 1, BreedteCm = 50, HoogteCm = 70, AfgesprokenPrijsExcl = 100m, TotaalExcl = 100m, SubtotaalExBtw = 100m, BtwBedrag = 21m, TotaalInclBtw = 121m },
                    new OfferteRegel { Titel = "Diploma", AantalStuks = 1, BreedteCm = 30, HoogteCm = 40, AfgesprokenPrijsExcl = 100m, TotaalExcl = 100m, SubtotaalExBtw = 100m, BtwBedrag = 21m, TotaalInclBtw = 121m }
                }
            };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();
            scope.OfferteId = offerte.Id;
        }

        // 2 voorschotten via de service
        await scope.Ontvangsten.RegistreerVoorschotAsync(scope.OfferteId, 50m, DateTime.Today, Betaalwijze.Cash);
        await scope.Ontvangsten.RegistreerVoorschotAsync(scope.OfferteId, 30m, DateTime.Today, Betaalwijze.Bancontact);

        // Bevestigd (werkbon) + bestelbon, via de echte workflow
        var offerteWf = new OfferteWorkflowService(factory, statusWf);
        scope.WerkBonId = await offerteWf.BevestigAsync(scope.OfferteId);
        var bon = await bestelbonWf.MaakBestelbonVanWerkBonAsync(scope.WerkBonId);
        scope.BestelbonId = bon.Id;
        return scope;
    }

    private static async Task<List<Ontvangst>> VoorschottenAsync(Scope s)
    {
        await using var db = await s.Factory.CreateDbContextAsync();
        return await db.Ontvangsten.AsNoTracking().Where(o => o.OfferteId == s.OfferteId && o.Soort == OntvangstSoort.Voorschot).OrderBy(o => o.Id).ToListAsync();
    }

    private static async Task<(Offerte offerte, Bestelbon bon)> StandAsync(Scope s)
    {
        await using var db = await s.Factory.CreateDbContextAsync();
        return (await db.Offertes.AsNoTracking().SingleAsync(o => o.Id == s.OfferteId),
                await db.Bestelbonnen.AsNoTracking().SingleAsync(b => b.Id == s.BestelbonId));
    }

    private static async Task WachtOpAsync(Func<bool> voorwaarde, int maxMs = 5000)
    {
        var tot = Environment.TickCount64 + maxMs;
        while (!voorwaarde() && Environment.TickCount64 < tot) await Task.Delay(20);
    }

    private static async Task<Exception?> ProbeerAsync(Func<Task> actie)
    {
        try { await actie(); return null; }
        catch (Exception ex) { return ex; }
    }

    // ───────────────────────── 0. uitgangssituatie ─────────────────────────

    [Fact]
    public void Guard_detecteert_een_onobserveerde_faulted_task()
    {
        // Controle op de meetlat zelf: zonder dit zou "geen fouten" ook kunnen betekenen "guard werkt niet".
        using var guard = new OnobserveerdGuard();
        static void Maak() { _ = Task.Run(() => throw new InvalidOperationException("canary")); }
        Maak();
        System.Threading.Thread.Sleep(200);
        var ex = Assert.ThrowsAny<Exception>(() => guard.AssertGeen());
        Assert.Contains("canary", ex.Message);
    }

    [Fact]
    public async Task Uitgangssituatie_klopt()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var (o, b) = await StandAsync(s);
        _out.WriteLine($"offerte {o.Status} voorschot {o.VoorschotBedrag}; bon {b.Status} nr {b.BestelbonNummer} totaal {b.TotaalInclBtw} voorschot {b.VoorschotBedrag}");
        Assert.Equal(80m, o.VoorschotBedrag);
        Assert.Equal(80m, b.VoorschotBedrag);
        Assert.Equal(2, (await VoorschottenAsync(s)).Count);
        guard.AssertGeen();
    }

    // ───────────────────────── 1. service-pad ─────────────────────────

    [Fact]
    public async Task Voorschot_verwijderen_pad_bestelbon_en_pad_offerte()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var v = await VoorschottenAsync(s);

        // Alle Ontvangsten hebben OfferteId (pad offerte); een ontvangst met BestelbonId (pad bestelbon) zetten we er bij
        await using (var db = await s.Factory.CreateDbContextAsync())
        {
            var tweede = await db.Ontvangsten.SingleAsync(o => o.Id == v[1].Id);
            tweede.BestelbonId = s.BestelbonId;
            await db.SaveChangesAsync();
        }

        var n1 = await s.Ontvangsten.VerwijderAsync(v[0].Id);   // pad zonder BestelbonId
        var n2 = await s.Ontvangsten.VerwijderAsync(v[1].Id);   // pad met BestelbonId
        Assert.Equal(30m, n1);
        Assert.Equal(0m, n2);

        var (o, b) = await StandAsync(s);
        Assert.Equal(0m, o.VoorschotBedrag);
        Assert.False(o.IsVoorschotBetaald);
        Assert.Equal(0m, b.VoorschotBedrag);
        Assert.Empty(await VoorschottenAsync(s));
        guard.AssertGeen();
    }

    [Fact]
    public async Task Beide_voorschotten_na_elkaar_verwijderen()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        foreach (var v in await VoorschottenAsync(s))
            await s.Ontvangsten.VerwijderAsync(v.Id);

        var (o, b) = await StandAsync(s);
        Assert.Equal(0m, o.VoorschotBedrag);
        Assert.Equal(0m, b.VoorschotBedrag);
        guard.AssertGeen();
    }

    [Fact]
    public async Task Beide_voorschotten_gelijktijdig_verwijderen_WhenAll()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync(fileDb: true);
        var v = await VoorschottenAsync(s);

        var taken = v.Select(x => ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(x.Id))).ToArray();
        var fouten = await Task.WhenAll(taken);
        foreach (var f in fouten) _out.WriteLine(f is null ? "ok" : $"FOUT {f.GetType().Name}: {f.Message}");

        var (o, b) = await StandAsync(s);
        var over = (await VoorschottenAsync(s)).Sum(x => x.BedragIncl);
        _out.WriteLine($"na afloop: ontvangsten over {over}, offerte voorschot {o.VoorschotBedrag}, bon voorschot {b.VoorschotBedrag}");
        Assert.Equal(over, o.VoorschotBedrag);   // lost-update controle
        guard.AssertGeen();
    }

    [Fact]
    public async Task Hetzelfde_voorschot_twee_keer_gelijktijdig_verwijderen_dubbelklik()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync(fileDb: true);
        var v = (await VoorschottenAsync(s))[0];

        var fouten = await Task.WhenAll(
            ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id)),
            ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id)));
        foreach (var f in fouten) _out.WriteLine(f is null ? "ok" : $"FOUT {f.GetType().Name}: {f.Message}");

        var (o, _) = await StandAsync(s);
        _out.WriteLine($"offerte voorschot na dubbelklik: {o.VoorschotBedrag} (verwacht 30)");
        Assert.Equal(30m, o.VoorschotBedrag);   // mag niet twee keer afgetrokken zijn
        guard.AssertGeen();
    }

    [Fact]
    public async Task Hetzelfde_voorschot_twee_keer_gelijktijdig_verwijderen_dubbelklik_gedeelde_connectie()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync(fileDb: false);
        var v = (await VoorschottenAsync(s))[0];

        var fouten = await Task.WhenAll(
            ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id)),
            ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id)));
        foreach (var f in fouten) _out.WriteLine(f is null ? "ok" : $"FOUT {f.GetType().Name}: {f.Message}");
        guard.AssertGeen();
    }

    [Fact]
    public async Task Bestelbon_betaald_en_dan_voorschot_verwijderen_heropent_bestelbon()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await s.BestelbonWf.MarkeerKlaarVoorExportAsync(s.BestelbonId);
        var stand = await s.Ontvangsten.GetBetaalStandAsync(s.BestelbonId);
        _out.WriteLine($"stand voor betaling: totaal {stand.TotaalIncl} voorschot {stand.Voorschot} rest {stand.Rest}");
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, stand.Rest, DateTime.Today, Betaalwijze.Overschrijving);

        var (o0, b0) = await StandAsync(s);
        Assert.Equal(BestelbonStatus.Betaald, b0.Status);
        _out.WriteLine($"offerte status {o0.Status}");

        var v = (await VoorschottenAsync(s))[0];
        await s.Ontvangsten.VerwijderAsync(v.Id);

        var (o, b) = await StandAsync(s);
        _out.WriteLine($"na verwijderen: bon {b.Status}, offerte {o.Status}");
        Assert.NotEqual(BestelbonStatus.Betaald, b.Status);
        guard.AssertGeen();
    }

    [Fact]
    public async Task Bestelbon_betaald_en_dan_betaling_verwijderen_heropent_bestelbon()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await s.BestelbonWf.MarkeerKlaarVoorExportAsync(s.BestelbonId);
        var stand = await s.Ontvangsten.GetBetaalStandAsync(s.BestelbonId);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, stand.Rest, DateTime.Today, Betaalwijze.Cash);
        var betaling = (await s.Ontvangsten.GetBetalingenAsync(s.BestelbonId)).Single();

        await s.Ontvangsten.VerwijderAsync(betaling.Id);

        var (_, b) = await StandAsync(s);
        Assert.NotEqual(BestelbonStatus.Betaald, b.Status);
        guard.AssertGeen();
    }

    [Theory]
    [InlineData(BestelbonStatus.Geannuleerd)]
    [InlineData(BestelbonStatus.Geexporteerd)]
    public async Task Voorschot_verwijderen_bij_bestelbonstatus(BestelbonStatus status)
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await using (var db = await s.Factory.CreateDbContextAsync())
        {
            var b = await db.Bestelbonnen.SingleAsync(x => x.Id == s.BestelbonId);
            b.Status = status;
            if (status == BestelbonStatus.Geexporteerd) b.ExportPad = "C:\\export\\bon.pdf";
            await db.SaveChangesAsync();
        }

        var v = (await VoorschottenAsync(s))[0];
        var fout = await ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id));
        _out.WriteLine(fout is null ? "geen fout" : $"FOUT {fout.GetType().Name}: {fout.Message}");
        Assert.Null(fout);

        var (o, bon) = await StandAsync(s);
        _out.WriteLine($"bon {bon.Status} voorschot {bon.VoorschotBedrag}; offerte voorschot {o.VoorschotBedrag}");
        Assert.Equal(30m, o.VoorschotBedrag);
        guard.AssertGeen();
    }

    [Fact]
    public async Task Offerte_intussen_gewijzigd_door_ander_scherm()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var v = (await VoorschottenAsync(s))[0];

        // ander scherm wijzigt de offerte net voor het verwijderen
        await using (var db = await s.Factory.CreateDbContextAsync())
        {
            var o = await db.Offertes.SingleAsync(x => x.Id == s.OfferteId);
            o.Opmerking = "intussen gewijzigd";
            await db.SaveChangesAsync();
        }

        var fout = await ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id));
        _out.WriteLine(fout is null ? "geen fout" : $"FOUT {fout.GetType().Name}: {fout.Message}");
        Assert.Null(fout);
        guard.AssertGeen();
    }

    [Fact]
    public async Task Verwijderen_en_registreren_gelijktijdig_op_dezelfde_offerte()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync(fileDb: true);
        var v = (await VoorschottenAsync(s))[0];

        var fouten = await Task.WhenAll(
            ProbeerAsync(() => s.Ontvangsten.VerwijderAsync(v.Id)),
            ProbeerAsync(() => s.Ontvangsten.RegistreerVoorschotAsync(s.OfferteId, 10m, DateTime.Today, Betaalwijze.CreditCard)));
        foreach (var f in fouten) _out.WriteLine(f is null ? "ok" : $"FOUT {f.GetType().Name}: {f.Message}");

        var (o, _) = await StandAsync(s);
        var som = (await VoorschottenAsync(s)).Sum(x => x.BedragIncl);
        _out.WriteLine($"offerte voorschot {o.VoorschotBedrag}, som ontvangsten {som}");
        guard.AssertGeen();
    }

    [Fact]
    public async Task Ontvangst_al_weg_VerwijderAsync_geeft_null()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var v = (await VoorschottenAsync(s))[0];
        await s.Ontvangsten.VerwijderAsync(v.Id);

        decimal? resultaat = -1m;
        var fout = await ProbeerAsync(async () => resultaat = await s.Ontvangsten.VerwijderAsync(v.Id));
        _out.WriteLine(fout is null ? $"resultaat: {(resultaat?.ToString() ?? "null")}" : $"FOUT {fout.GetType().Name}: {fout.Message}");
        Assert.Null(fout);
        Assert.Null(resultaat);
        guard.AssertGeen();
    }

    [Fact]
    public async Task Ontvangst_intussen_door_ander_scherm_verwijderd_geeft_DbUpdateConcurrencyException()
    {
        // Context A leest de ontvangst, een ander scherm verwijdert hem, A verwijdert daarna: zo ziet de race
        // tussen lezen en SaveChanges in VerwijderAsync eruit.
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync(fileDb: true);
        var v = (await VoorschottenAsync(s))[0];

        await using var a = await s.Factory.CreateDbContextAsync();
        var inA = await a.Ontvangsten.SingleAsync(o => o.Id == v.Id);

        await s.Ontvangsten.VerwijderAsync(v.Id);   // "ander scherm"

        a.Ontvangsten.Remove(inA);
        var fout = await ProbeerAsync(() => a.SaveChangesAsync());
        _out.WriteLine(fout is null ? "geen fout" : $"FOUT {fout.GetType().FullName}: {fout.Message}");
        Assert.IsType<DbUpdateConcurrencyException>(fout);
        guard.AssertGeen();
    }

    // ───────────────────────── 3. viewmodel-pad ─────────────────────────

    private async Task<(BestelbonnenViewModel vm, TestToastService toast)> MaakBestelbonVmAsync(Scope s)
    {
        var toast = new TestToastService();
        var vm = new BestelbonnenViewModel(s.Factory, s.BestelbonWf,
            new BestelbonExportService(s.Factory, Array.Empty<IBestelbonExporter>()), toast,
            new TestNavigationService(), s.Ontvangsten, new TestPathOpener());
        await vm.InitializeAsync();
        vm.GeselecteerdeBestelbon = vm.Bestelbonnen.Single(b => b.Id == s.BestelbonId);
        await WachtOpAsync(() => vm.Stand is not null);
        return (vm, toast);
    }

    private async Task<(OfferteViewModel vm, TestToastService toast, TestDialogService dlg)> MaakOfferteVmAsync(Scope s)
    {
        var toast = new TestToastService();
        var dlg = new TestDialogService { ConfirmResult = true };
        var vm = new OfferteViewModel(s.Factory, new TestNavigationService(), dlg, s.Pricing,
            new OfferteWorkflowService(s.Factory, s.StatusWf), new WerkBonWorkflowService(s.Factory, s.StatusWf), s.StatusWf, s.BestelbonWf,
            new BestelbonExportService(s.Factory, Array.Empty<IBestelbonExporter>()), new TestFilePickerService(),
            new OfferteValidator(s.Factory), toast, new KlantValidator(s.Factory), new KlantDialogService(),
            s.Ontvangsten, new TestPathOpener(), new GeenOfferteNavigatie(), s.Auth);
        await vm.InitializeAsync();
        await vm.LoadAsync(s.OfferteId);
        await WachtOpAsync(() => vm.Voorschotten.Count == 2);
        return (vm, toast, dlg);
    }

    private void ToonToasts(string naam, TestToastService t)
        => _out.WriteLine($"{naam}: errors=[{string.Join(" | ", t.ErrorMessages)}] success=[{string.Join(" | ", t.SuccessMessages)}]");

    [Fact]
    public async Task OfferteVm_VerwijderVoorschotCommand_ExecuteAsync()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var (vm, toast, _) = await MaakOfferteVmAsync(s);
        Assert.Equal(2, vm.Voorschotten.Count);

        await vm.VerwijderVoorschotCommand.ExecuteAsync(vm.Voorschotten[0]);
        await Task.Delay(300);   // fire-and-forget werk (OnOfferteChanged → RunAsync) laten eindigen
        ToonToasts("OfferteVm", toast);

        Assert.Empty(toast.ErrorMessages);
        Assert.Single(vm.Voorschotten);
        guard.AssertGeen();
    }

    [Fact]
    public async Task OfferteVm_beide_voorschotten_via_ICommand_Execute_twee_keer_snel()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var (vm, toast, _) = await MaakOfferteVmAsync(s);
        var a = vm.Voorschotten[0];
        var b = vm.Voorschotten[1];

        ICommand cmd = vm.VerwijderVoorschotCommand;
        cmd.Execute(a);
        cmd.Execute(b);
        await Task.Delay(1000);
        ToonToasts("OfferteVm 2x snel", toast);

        var (o, _) = await StandAsync(s);
        _out.WriteLine($"DB voorschot {o.VoorschotBedrag}, ontvangsten {(await VoorschottenAsync(s)).Count}, scherm {vm.Voorschotten.Count}");
        Assert.Empty(toast.ErrorMessages);
        guard.AssertGeen();
    }

    [Fact]
    public async Task OfferteVm_zelfde_voorschot_twee_keer_via_ICommand_Execute_dubbelklik()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        var (vm, toast, _) = await MaakOfferteVmAsync(s);
        var a = vm.Voorschotten[0];

        ICommand cmd = vm.VerwijderVoorschotCommand;
        cmd.Execute(a);
        cmd.Execute(a);
        await Task.Delay(1000);
        ToonToasts("OfferteVm dubbelklik", toast);
        var (o, _) = await StandAsync(s);
        _out.WriteLine($"DB voorschot {o.VoorschotBedrag}");
        Assert.Empty(toast.ErrorMessages);
        guard.AssertGeen();
    }

    [Fact]
    public async Task BestelbonVm_VerwijderBetalingCommand_betaling_en_voorschot_ExecuteAsync()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await s.BestelbonWf.MarkeerKlaarVoorExportAsync(s.BestelbonId);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, 62m, DateTime.Today, Betaalwijze.Cash);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, 40m, DateTime.Today, Betaalwijze.Bancontact);

        var (vm, toast) = await MaakBestelbonVmAsync(s);
        await WachtOpAsync(() => vm.Betalingen.Count == 2);
        Assert.Equal(2, vm.Betalingen.Count);

        await vm.VerwijderBetalingCommand.ExecuteAsync(vm.Betalingen[0]);
        await Task.Delay(300);

        // een voorschot-Ontvangst door hetzelfde commando halen
        var voorschot = (await VoorschottenAsync(s))[0];
        await vm.VerwijderBetalingCommand.ExecuteAsync(voorschot);
        await Task.Delay(300);
        ToonToasts("BestelbonVm", toast);

        Assert.Empty(toast.ErrorMessages);
        guard.AssertGeen();
    }

    [Fact]
    public async Task BestelbonVm_VerwijderBetaling_op_Betaald_bon_via_ICommand_twee_keer_snel()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await s.BestelbonWf.MarkeerKlaarVoorExportAsync(s.BestelbonId);
        var st = await s.Ontvangsten.GetBetaalStandAsync(s.BestelbonId);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, 100m, DateTime.Today, Betaalwijze.Cash);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, st.Rest - 100m, DateTime.Today, Betaalwijze.Bancontact);

        var (vm, toast) = await MaakBestelbonVmAsync(s);
        await WachtOpAsync(() => vm.Betalingen.Count == 2);
        var a = vm.Betalingen[0];
        var b = vm.Betalingen[1];

        ICommand cmd = vm.VerwijderBetalingCommand;
        cmd.Execute(a);
        cmd.Execute(b);
        await Task.Delay(1500);
        ToonToasts("BestelbonVm 2x snel", toast);
        var (_, bon) = await StandAsync(s);
        _out.WriteLine($"bon status {bon.Status}; betalingen scherm {vm.Betalingen.Count}; DB {(await s.Ontvangsten.GetBetalingenAsync(s.BestelbonId)).Count}");
        Assert.Empty(toast.ErrorMessages);
        guard.AssertGeen();
    }

    [Fact]
    public async Task BestelbonVm_zelfde_betaling_dubbelklik_via_ICommand()
    {
        using var guard = new OnobserveerdGuard();
        await using var s = await MaakScopeAsync();
        await s.BestelbonWf.MarkeerKlaarVoorExportAsync(s.BestelbonId);
        await s.Ontvangsten.RegistreerBetalingAsync(s.BestelbonId, 62m, DateTime.Today, Betaalwijze.Cash);

        var (vm, toast) = await MaakBestelbonVmAsync(s);
        await WachtOpAsync(() => vm.Betalingen.Count == 1);
        var a = vm.Betalingen[0];

        ICommand cmd = vm.VerwijderBetalingCommand;
        cmd.Execute(a);
        cmd.Execute(a);
        await Task.Delay(1000);
        ToonToasts("BestelbonVm dubbelklik", toast);
        Assert.Empty(toast.ErrorMessages);
        guard.AssertGeen();
    }
}
