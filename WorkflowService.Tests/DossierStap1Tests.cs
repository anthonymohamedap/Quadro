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
}
