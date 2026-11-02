using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// US-103 (P15) — zodra er een werkbon is, kan een offerte niet meer met de hand terug naar
/// Concept of Verzonden (dat liet de werkbon gewoon bestaan, buiten de statusmachine om).
/// </summary>
public class OfferteTerugNaarConceptTests
{
    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    private static async Task<OfferteViewModel> LaadAsync(OfferteStatus status, bool metWerkBon)
    {
        var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var f = scope.Factory;
        int id;
        await using (var db = await f.CreateDbContextAsync())
        {
            foreach (var (c, n) in new[] { ('G', "Glas"), ('P', "Passe-partout"), ('D', "Diepte"), ('O', "Opkleven"), ('R', "Rug") })
                db.AfwerkingsGroepen.Add(new AfwerkingsGroep { Code = c, Naam = n });
            var offerte = new Offerte
            {
                OfferteNummer = 412, Datum = DateTime.Today, Status = status,
                Klant = new Klant { Voornaam = "Test", Achternaam = "Quadro" }
            };
            if (metWerkBon) offerte.WerkBon = new WerkBon { Status = WerkBonStatus.Gepland };
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
        return vm;
    }

    [Fact]
    public async Task Bevestigd_met_werkbon_kan_niet_meer_terug()
    {
        var vm = await LaadAsync(OfferteStatus.Goedgekeurd, metWerkBon: true);

        Assert.False(vm.KanTerugNaarConcept);
        Assert.False(vm.KanTerugNaarVerzonden);

        await vm.TerugNaarConceptCommand.ExecuteAsync(null);   // ook via de code geweigerd
        Assert.Equal(OfferteStatus.Goedgekeurd, vm.Offerte!.Status);
    }

    [Fact]
    public async Task Goedgekeurd_zonder_werkbon_mag_nog_terug()
    {
        var vm = await LaadAsync(OfferteStatus.Goedgekeurd, metWerkBon: false);
        Assert.True(vm.KanTerugNaarConcept);
        Assert.True(vm.KanTerugNaarVerzonden);
    }

    [Fact]
    public async Task Verzonden_mag_terug_naar_concept()
    {
        var vm = await LaadAsync(OfferteStatus.Verzonden, metWerkBon: false);
        Assert.True(vm.KanTerugNaarConcept);
    }
}
