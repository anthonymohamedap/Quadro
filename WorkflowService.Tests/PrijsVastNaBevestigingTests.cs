using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-100 — zolang de klant niet bevestigd heeft, rekent de offerte gewoon live mee; vanaf de
/// bevestiging staat de prijs vast en wordt er enkel nog herberekend als je dat uitdrukkelijk vraagt.
/// </summary>
public class PrijsVastNaBevestigingTests
{
    private sealed class TellendePricing : IPricingService
    {
        public int Aantal { get; private set; }
        public Task BerekenAsync(int offerteId) => Task.CompletedTask;
        public Task BerekenAsync(Offerte offerte) { Aantal++; return Task.CompletedTask; }
    }

    private static (OffertePrijsViewModel Vm, TellendePricing Pricing, TestDialogService Dialogs) Maak(bool prijsVast)
    {
        var pricing = new TellendePricing();
        var dialogs = new TestDialogService();
        var vm = new OffertePrijsViewModel(
            pricing, dialogs, new TestToastService(),
            runFullValidation: _ => Task.FromResult(true),
            refreshLijstPrijzen: () => Task.CompletedTask,
            buildSnapshot: () => new Offerte { Regels = new List<OfferteRegel>() },
            applySnapshot: _ => { },
            isPrijsVast: () => prijsVast);
        return (vm, pricing, dialogs);
    }

    [Fact]
    public async Task Voor_bevestiging_rekent_de_offerte_automatisch()
    {
        var (vm, pricing, _) = Maak(prijsVast: false);
        await vm.BerekenSilentAsync();
        Assert.Equal(1, pricing.Aantal);
    }

    [Fact]
    public async Task Na_bevestiging_geen_automatische_herberekening()
    {
        var (vm, pricing, _) = Maak(prijsVast: true);
        await vm.BerekenSilentAsync();   // o.a. vóór "Bestelbon bekijken" en na elke wijziging
        Assert.Equal(0, pricing.Aantal);
    }

    [Fact]
    public async Task Na_bevestiging_enkel_herberekenen_na_uitdrukkelijke_bevestiging()
    {
        var (vm, pricing, dialogs) = Maak(prijsVast: true);

        dialogs.ConfirmResult = false;
        await vm.BerekenCommand.ExecuteAsync(null);
        Assert.Equal(0, pricing.Aantal);

        dialogs.ConfirmResult = true;
        await vm.BerekenCommand.ExecuteAsync(null);
        Assert.Equal(1, pricing.Aantal);
    }

    [Theory]
    [InlineData(OfferteStatus.Concept, null, null, false)]
    [InlineData(OfferteStatus.Verzonden, null, null, false)]
    [InlineData(OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland, null, true)]
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, true)]
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgewerkt, BestelbonStatus.Draft, true)]
    [InlineData(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald, true)]
    [InlineData(OfferteStatus.Geannuleerd, null, null, false)]
    public void Prijs_vast_vanaf_stap_2(OfferteStatus offerte, WerkBonStatus? werkbon, BestelbonStatus? bon, bool vast)
        => Assert.Equal(vast, OfferteWorkflowViewModel.IsPrijsVastNaBevestiging(
            VerkoopStapResolver.Bepaal(offerte, werkbon, bon, 0m)));

    [Fact]
    public void Voorlopige_bestelbon_voor_bevestiging_zet_de_prijs_niet_vast()
        => Assert.False(OfferteWorkflowViewModel.IsPrijsVastNaBevestiging(
            VerkoopStapResolver.Bepaal(OfferteStatus.Concept, null, BestelbonStatus.Draft, 100m)));
}

/// <summary>US-101 — een verkoop die afgehaald en volledig betaald is, is vergrendeld.</summary>
public class AfgerondeVerkoopVergrendeldTests
{
    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    private static OfferteWorkflowViewModel Maak()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var toast = new TestToastService();
        var workflow = new QuadroApp.Service.WorkflowService(
            factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);
        return new OfferteWorkflowViewModel(
            factory,
            new QuadroApp.Service.OfferteWorkflowService(factory, workflow),
            new QuadroApp.Service.WerkBonWorkflowService(factory, workflow),
            workflow,
            new FakeBestelbonWorkflow(),
            new QuadroApp.Service.BestelbonExportService(factory, System.Array.Empty<IBestelbonExporter>()),
            new TestFilePickerService(),
            new TestDialogService(),
            toast,
            new QuadroApp.Service.OntvangstService(factory, new TestAuthService()),
            new TestPathOpener(),
            new GeenOfferteNavigatie(),
            getOfferteId: () => 0,
            saveAndReload: () => Task.CompletedTask,
            berekenAsync: () => Task.CompletedTask,
            runValidation: _ => Task.FromResult(true));
    }

    private sealed class FakeBestelbonWorkflow : IBestelbonWorkflowService
    {
        public Task<Bestelbon> MaakBestelbonVanOfferteAsync(int offerteId) => Task.FromResult(new Bestelbon());
        public Task<Bestelbon> MaakBestelbonVanWerkBonAsync(int werkBonId) => Task.FromResult(new Bestelbon());
        public Task<Bestelbon?> GetBestelbonAsync(int bestelbonId) => Task.FromResult<Bestelbon?>(null);
        public Task<Bestelbon?> GetBestelbonVoorOfferteAsync(int offerteId) => Task.FromResult<Bestelbon?>(null);
        public Task MarkeerKlaarVoorExportAsync(int bestelbonId) => Task.CompletedTask;
        public Task MarkeerBetaaldAsync(int bestelbonId) => Task.CompletedTask;
        public Task SaveDraftAsync(Bestelbon bestelbon) => Task.CompletedTask;
        public Task HerberekenTotalenAsync(int bestelbonId) => Task.CompletedTask;
        public Task KenVolgnummerToeBijBevestigingAsync(int offerteId) => Task.CompletedTask;
    }

    [Fact]
    public void Lopende_verkoop_is_niet_vergrendeld_maar_prijs_staat_vast()
    {
        var vm = Maak();
        vm.GekoppeldeWerkBon = new WerkBon { Status = WerkBonStatus.InUitvoering };
        vm.ToonStappen(OfferteStatus.InProductie, 104.86m);

        Assert.True(vm.IsPrijsVast);
        Assert.False(vm.IsAfgerond);
        Assert.False(vm.IsVergrendeld);
    }

    [Fact]
    public void Afgehaald_en_betaald_is_vergrendeld_tot_een_admin_ontgrendelt()
    {
        var vm = Maak();
        vm.GekoppeldeWerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald };
        vm.GekoppeldeBestelbon = new Bestelbon { Status = BestelbonStatus.Betaald, VolgNr = 19, BestelbonNummer = "2026-19" };
        vm.ToonStappen(OfferteStatus.Betaald, 0m);

        Assert.True(vm.IsAfgerond);
        Assert.True(vm.IsVergrendeld);

        vm.IsOntgrendeld = true;
        Assert.False(vm.IsVergrendeld);
    }

    [Fact]
    public void Afgehaald_maar_nog_niet_betaald_is_niet_vergrendeld()
    {
        var vm = Maak();
        vm.GekoppeldeWerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald };
        vm.GekoppeldeBestelbon = new Bestelbon { Status = BestelbonStatus.Geexporteerd, VolgNr = 19, BestelbonNummer = "2026-19" };
        vm.ToonStappen(OfferteStatus.Besteld, 26.20m);

        Assert.False(vm.IsVergrendeld);
    }
}
