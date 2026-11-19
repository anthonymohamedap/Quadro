using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Verkoop;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using QuadroApp.ViewModels.Verkoop;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-91 — stap 1 Offerte in het dossier: zelfde offerte-VM, terugkijken, bevestigen, automatisch bewaren.</summary>
public class DossierStap1Tests
{
    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    private sealed class Provider(IDbContextFactory<AppDbContext> f) : IServiceProvider
    {
        public object? GetService(Type t) => t == typeof(OfferteViewModel) ? MaakVm(f) : null;
    }

    private static OfferteViewModel MaakVm(IDbContextFactory<AppDbContext> f)
    {
        var toast = new TestToastService();
        var auth = new TestAuthService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var pricing = new PricingService(f, new PricingSettingsProvider(f), new PricingEngine(), NullLogger<PricingService>.Instance);
        var bestelbonWf = new BestelbonWorkflowService(f, pricing, auth);
        return new OfferteViewModel(f, new TestNavigationService(), new TestDialogService(), pricing,
            new OfferteWorkflowService(f, wf), new WerkBonWorkflowService(f, wf), wf, bestelbonWf,
            new BestelbonExportService(f, Array.Empty<IBestelbonExporter>()), new TestFilePickerService(),
            new OfferteValidator(f), toast, new KlantValidator(f), new KlantDialogService(),
            new OntvangstService(f, auth, bestelbonWf), new TestPathOpener(), new GeenOfferteNavigatie(), auth);
    }

    private static async Task<(SqliteTestDatabase Scope, int Id)> SeedAsync(OfferteStatus status = OfferteStatus.Concept)
    {
        var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await scope.Factory.CreateDbContextAsync();
        foreach (var (c, n) in new[] { ('G', "Glas"), ('P', "Passe-partout"), ('D', "Diepte"), ('O', "Opkleven"), ('R', "Rug") })
            db.AfwerkingsGroepen.Add(new AfwerkingsGroep { Code = c, Naam = n });
        var offerte = new Offerte
        {
            OfferteNummer = 419, Datum = DateTime.Today, Status = status,
            Klant = new Klant { Voornaam = "Test", Achternaam = "Quadro" },
            Regels = { new OfferteRegel { Titel = "Trouwfoto", AantalStuks = 1, BreedteCm = 50, HoogteCm = 70 } }
        };
        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();
        return (scope, offerte.Id);
    }

    private static async Task<OfferteViewModel> LaadVmAsync(IDbContextFactory<AppDbContext> f, int id)
    {
        var vm = MaakVm(f);
        await vm.InitializeAsync();
        await vm.LoadAsync(id);
        return vm;
    }

    [Fact]
    public async Task Vingerafdruk_verandert_bij_een_wijziging_en_niet_bij_herladen()
    {
        var (scope, id) = await SeedAsync();
        await using var _ = scope;
        var vm = await LaadVmAsync(scope.Factory, id);
        var voor = vm.Vingerafdruk();

        Assert.Equal(voor, (await LaadVmAsync(scope.Factory, id)).Vingerafdruk());

        vm.Regels.First().Titel = "Poster";
        Assert.NotEqual(voor, vm.Vingerafdruk());
    }

    [Fact]
    public async Task Terugkijken_maakt_de_offerte_alleen_lezen()
    {
        var (scope, id) = await SeedAsync();
        await using var _ = scope;
        var vm = await LaadVmAsync(scope.Factory, id);
        Assert.True(vm.KanBewerken);

        vm.AlleenBekijken = true;
        Assert.False(vm.KanBewerken);
        Assert.False(vm.IsAlleenLezen);   // geen "afgerond"-melding: enkel bekijken
    }

    [Fact]
    public async Task Bevestigen_in_het_dossier_opent_geen_planning_maar_meldt_bevestigd()
    {
        var (scope, id) = await SeedAsync();
        await using var _ = scope;
        var vm = await LaadVmAsync(scope.Factory, id);
        vm.Workflow.PlanningOpenenNaBevestigen = false;
        var bevestigd = false;
        vm.Workflow.Bevestigd += () => bevestigd = true;

        await vm.BevestigenCommand.ExecuteAsync(null);

        Assert.True(bevestigd);
        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.True(await db.WerkBonnen.AnyAsync(w => w.OfferteId == id));
    }

    [Fact]
    public async Task Dossier_bewaart_stap_1_automatisch_bij_verlaten_als_er_iets_gewijzigd_is()
    {
        var (scope, id) = await SeedAsync();
        await using var _ = scope;
        var f = scope.Factory;
        var toast = new TestToastService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var dossier = new VerkoopDossierViewModel(f, new VerkoopOverzichtService(f), new VerkoopDossierService(f, wf),
            new TestNavigationService(), new GeenOfferteNavigatie(), wf, new WerkBonWorkflowService(f, wf),
            new TestDialogService(), toast, services: new Provider(f));
        await dossier.ReceiveAsync(id);
        await dossier.InitializeAsync();

        Assert.True(dossier.IsStap1Zichtbaar);
        Assert.Equal("bewaard ✓", dossier.BewaardTekst);

        dossier.Stap1!.Regels.First().Titel = "Poster A3";
        await dossier.BewaarStap1Async();

        await using var db = await f.CreateDbContextAsync();
        Assert.Equal("Poster A3", (await db.OfferteRegels.SingleAsync()).Titel);
        Assert.Equal("bewaard ✓", dossier.BewaardTekst);
    }

    [Fact]
    public async Task Nieuwe_verkoop_start_in_het_dossier_en_gaat_na_bevestigen_naar_stap_2()
    {
        // Fix 08/11: "+ Nieuwe verkoop" → stap 1 van het dossier, niet het oude offertescherm.
        var (scope, _) = await SeedAsync();
        await using var __ = scope;
        var f = scope.Factory;
        await using (var db = await f.CreateDbContextAsync())
        {
            db.Klanten.Add(new Klant { Voornaam = "An", Achternaam = "Mertens" });
            await db.SaveChangesAsync();
        }

        var toast = new TestToastService();
        var wf = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        var dossier = new VerkoopDossierViewModel(f, new VerkoopOverzichtService(f), new VerkoopDossierService(f, wf),
            new TestNavigationService(), new GeenOfferteNavigatie(), wf, new WerkBonWorkflowService(f, wf),
            new TestDialogService(), toast, services: new Provider(f));
        await dossier.ReceiveAsync(0);
        await dossier.InitializeAsync();

        Assert.True(dossier.IsNieuw);
        Assert.True(dossier.IsStap1Zichtbaar);
        Assert.Equal("Nieuwe verkoop", dossier.Titel);
        Assert.Equal("Bevestigen → Werkbon ▶", dossier.HoofdKnopTekst);

        var vm = dossier.Stap1!;
        vm.SelectedKlant = vm.GefilterdeKlanten.First(k => k.Achternaam == "Mertens");
        vm.RegelToevoegenCommand.Execute(null);
        vm.Regels.First().Titel = "Poster";

        await dossier.HoofdKnopCommand.ExecuteAsync(null);   // Bevestigen → Werkbon
        for (var i = 0; i < 50 && dossier.Info?.Huidig != VerkoopStap.Werkbon; i++) await Task.Delay(100);

        Assert.False(dossier.IsNieuw);
        Assert.Equal(VerkoopStap.Werkbon, dossier.Info!.Huidig);
        Assert.StartsWith("Verkoop ", dossier.Titel);
        await using var check = await f.CreateDbContextAsync();
        var offerte = await check.Offertes.Include(o => o.WerkBon).SingleAsync(o => o.Id == dossier.OfferteId);
        Assert.True(offerte.OfferteNummer > 0, $"nummer={offerte.OfferteNummer} id={offerte.Id} titel={dossier.Titel}");
        Assert.NotNull(offerte.WerkBon);
    }

    [Fact]
    public async Task Offerte_opslaan_houdt_de_velden_van_het_dossier()
    {
        // Fix 08/11: opslaan schreef ook velden weg die het offertescherm niet kent (nummer, overslaan, momentopname).
        var (scope, id) = await SeedAsync();
        await using var _ = scope;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var o = await db.Offertes.SingleAsync(x => x.Id == id);
            o.OvergeslagenStappen = QuadroApp.Model.DB.VerkoopStapFlags.Werkbon;
            o.OverslaanReden = "Standaardkader uit voorraad";
            o.Momentopname = "{}";
            await db.SaveChangesAsync();
        }

        var vm = await LaadVmAsync(scope.Factory, id);
        vm.Regels.First().Titel = "Gewijzigd";
        await vm.SaveCoreAsync(reloadAfterSave: true);

        await using var check = await scope.Factory.CreateDbContextAsync();
        var bewaard = await check.Offertes.SingleAsync(x => x.Id == id);
        Assert.Equal(419, bewaard.OfferteNummer);
        Assert.Equal(QuadroApp.Model.DB.VerkoopStapFlags.Werkbon, bewaard.OvergeslagenStappen);
        Assert.Equal("Standaardkader uit voorraad", bewaard.OverslaanReden);
        Assert.Equal("{}", bewaard.Momentopname);
    }
}
