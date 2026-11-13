using QuadroApp.Model.DB;
using QuadroApp.Service.Pricing;
using QuadroApp.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Feedback Veerle (12/11): opslaan zonder "Ververs", en de prijs van standaardkaders incl. btw.</summary>
public class FeedbackVeerle1211Tests
{
    [Fact]
    public async Task Winkelverkoop_staat_na_opslaan_meteen_in_de_lijst()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var vm = new WinkelVerkopenViewModel(scope.Factory,
            new TestNavigationService(), new TestDialogService(), new TestToastService(), new TestAuthService());
        await vm.InitializeAsync();
        Assert.Empty(vm.GefilterdeVerkopen);

        vm.NieuwCommand.Execute(null);
        vm.GeselecteerdeVerkoop!.Omschrijving = "Wenskaart";
        vm.GeselecteerdeVerkoop.Aantal = 2;
        vm.GeselecteerdeVerkoop.PrijsPerStukIncl = 3.50m;
        await vm.SaveCommand.ExecuteAsync(null);

        var verkoop = Assert.Single(vm.GefilterdeVerkopen);   // zonder op "Ververs" te klikken
        Assert.Equal(7.00m, verkoop.BedragIncl);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Standaardkader_staat_na_opslaan_meteen_in_de_lijst_met_prijs_incl()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var vm = new KantKlaarKaderenViewModel(scope.Factory, new TestNavigationService(),
            new TestDialogService(), new TestToastService());
        await vm.InitializeAsync();

        vm.NieuwCommand.Execute(null);
        vm.GeselecteerdeKader!.Naam = "C2";
        vm.GeselecteerdeKader.PrijsPerStukIncl = 10.00m;
        await vm.SaveCommand.ExecuteAsync(null);

        var kader = Assert.Single(vm.GefilterdeKaders);
        Assert.Equal(10.00m, kader.PrijsPerStukIncl);
        Assert.InRange(kader.PrijsPerStukExcl, 8.26m, 8.27m);
    }

    [Fact]
    public void Prijs_incl_btw_komt_exact_terug_in_de_offerte()
    {
        // Elke prijs van € 0,01 tot € 600,00 die Veerle incl. btw ingeeft, moet in de offerte
        // (prijsberekening ongewijzigd, rekent met de prijs excl.) exact zo terugkomen.
        var engine = new PricingEngine();
        var fout = new List<string>();
        for (var cent = 1; cent <= 60_000; cent++)
        {
            var incl = cent / 100m;
            var kader = new KantKlaarKader { Naam = "C2" };
            kader.PrijsPerStukIncl = incl;
            var offerte = new Offerte
            {
                Regels = [new OfferteRegel { AantalStuks = 1, BreedteCm = 50, HoogteCm = 70, KantKlaarKader = kader }]
            };
            var regel = engine.Calculate(offerte, 45m, 21m, 0m, 1m, 0m).Regels.Single();
            if (regel.TotaalInclBtw != incl) fout.Add($"{incl} → {regel.TotaalInclBtw}");
        }
        Assert.Empty(fout);
    }

    [Theory]
    [InlineData(37.19, 45.00)]   // bestaande prijs (2 decimalen) blijft hetzelfde bedrag tonen
    [InlineData(20, 24.20)]
    [InlineData(0, 0)]
    public void Bestaande_prijzen_excl_tonen_de_juiste_prijs_incl(decimal excl, decimal incl)
        => Assert.Equal(incl, new KantKlaarKader { PrijsPerStukExcl = excl }.PrijsPerStukIncl);
}
