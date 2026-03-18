using QuadroApp.Model.DB;
using QuadroApp.Validation;
using QuadroApp.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public sealed class BulkLijstenViewModelTests
{
    [Fact]
    public async Task ExecuteActionAsync_werkt_meerdere_velden_bij()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var validator = new TypeLijstValidator(factory);
        var toast = new TestToastService();

        var leverancierA = await SeedData.AddLeverancierAsync(factory, "Lev A");
        var leverancierB = await SeedData.AddLeverancierAsync(factory, "Lev B");
        var lijst1 = await SeedData.AddTypeLijstAsync(factory, "ART-001", leverancierA.Naam);
        var lijst2 = await SeedData.AddTypeLijstAsync(factory, "ART-002", leverancierA.Naam);

        var vm = new BulkLijstenViewModel(factory, validator, toast);
        await vm.InitializeAsync();
        vm.UpdateSelectedLijsten(vm.FilteredLijsten.Where(x => x.Id == lijst1.Id || x.Id == lijst2.Id));

        vm.BijwerkLeverancier = true;
        vm.GeselecteerdeBulkLeverancier = leverancierB;
        vm.BijwerkSoort = true;
        vm.NieuweSoort = "PVC";
        vm.BijwerkPrijsPerMeter = true;
        vm.GebruikPercentage = true;
        vm.PrijsWijzigingPct = 10m;
        vm.BijwerkVasteKost = true;
        vm.NieuweVasteKost = 4.25m;
        vm.BijwerkIsDealer = true;
        vm.NieuweIsDealer = true;

        await vm.ExecuteActionCommand.ExecuteAsync(null);

        await using var db = await factory.CreateDbContextAsync();
        var lijsten = db.TypeLijsten.Where(x => x.Id == lijst1.Id || x.Id == lijst2.Id).ToList();

        Assert.All(lijsten, lijst =>
        {
            Assert.Equal(leverancierB.Id, lijst.LeverancierId);
            Assert.Equal("PVC", lijst.Soort);
            Assert.Equal(11m, lijst.PrijsPerMeter);
            Assert.Equal(4.25m, lijst.VasteKost);
            Assert.True(lijst.IsDealer);
        });

        Assert.Contains(toast.SuccessMessages, message => message.Contains("prijs per meter", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteActionAsync_blokkeert_dubbele_artikelnummers_in_batch()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var validator = new TypeLijstValidator(factory);
        var toast = new TestToastService();

        var lijst1 = await SeedData.AddTypeLijstAsync(factory, "ART-100");
        var lijst2 = await SeedData.AddTypeLijstAsync(factory, "ART-200");

        var vm = new BulkLijstenViewModel(factory, validator, toast);
        await vm.InitializeAsync();
        vm.UpdateSelectedLijsten(vm.FilteredLijsten.Where(x => x.Id == lijst1.Id || x.Id == lijst2.Id));

        vm.BijwerkArtikelnummer = true;
        vm.NieuwArtikelnummer = "ART-DUBBEL";

        await vm.ExecuteActionCommand.ExecuteAsync(null);

        await using var db = await factory.CreateDbContextAsync();
        var dbLijst1 = await db.TypeLijsten.FindAsync(lijst1.Id);
        var dbLijst2 = await db.TypeLijsten.FindAsync(lijst2.Id);

        Assert.Equal("ART-100", dbLijst1!.Artikelnummer);
        Assert.Equal("ART-200", dbLijst2!.Artikelnummer);
        Assert.Contains(toast.ErrorMessages, message => message.Contains("dubbele artikelnummer", System.StringComparison.OrdinalIgnoreCase));
    }
}
