using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels.Verkoop;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-83 — wat de stappenbalk toont.</summary>
public class StappenBalkViewModelTests
{
    private static readonly IAsyncRelayCommand Volgende = new AsyncRelayCommand(() => Task.CompletedTask);

    [Fact]
    public void Toont_vijf_stappen_met_vinkjes_voor_afgewerkte_stappen()
    {
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, 104.86m), "Werkbon openen", Volgende);

        Assert.Equal(new[] { "Offerte", "Bestelling", "In productie", "Klaar om af te halen", "Afgehaald & betaald" },
            vm.Stappen.Select(s => s.Titel));
        Assert.Equal(new[] { "✓", "✓", "3", "4", "5" }, vm.Stappen.Select(s => s.Bolletje));
        Assert.True(vm.Stappen[2].IsHuidig);
        Assert.All(vm.Stappen, s => Assert.False(string.IsNullOrWhiteSpace(s.Uitleg)));
        Assert.Equal("Stap 3 van 5 · In productie", vm.StatusTekst);
        Assert.True(vm.IsVolgendeStapZichtbaar);
        Assert.Equal("Werkbon openen", vm.VolgendeStapTekst);
    }

    [Fact]
    public void Afgehaald_met_rest_toont_het_bedrag()
    {
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.Besteld, WerkBonStatus.Afgehaald, FactuurStatus.Geexporteerd, 26.2m), "Afrekenen", Volgende);

        Assert.Equal("Afgehaald — nog € 26,20 te betalen", vm.StatusTekst.Replace('.', ','));
        Assert.Equal("Afrekenen", vm.VolgendeStapTekst);
    }

    [Fact]
    public void Afgerond_heeft_geen_volgende_stap()
    {
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, FactuurStatus.Betaald, 0m), null, null);

        Assert.Equal("Afgehaald en volledig betaald", vm.StatusTekst);
        Assert.False(vm.IsVolgendeStapZichtbaar);
        Assert.All(vm.Stappen, s => Assert.True(s.IsKlaar));
    }

    [Fact]
    public void Geannuleerd_is_grijs_zonder_knop()
    {
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.Geannuleerd, null, null, 0m), "Bevestigen als bestelling", Volgende);

        Assert.True(vm.IsGeannuleerd);
        Assert.Equal("Geannuleerd", vm.StatusTekst);
        Assert.False(vm.IsVolgendeStapZichtbaar);
        Assert.All(vm.Stappen, s => Assert.True(s.IsNogTeDoen));
    }

    [Fact]
    public void Bestelbon_kan_al_vanaf_stap_1_naast_de_volgende_stap()
    {
        var bestelbon = new AsyncRelayCommand(() => Task.CompletedTask);
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.Concept, null, null, 154.86m),
            "Bevestigen als bestelling", Volgende, "Bestelbon aanmaken", bestelbon);

        Assert.True(vm.IsVolgendeStapZichtbaar);
        Assert.True(vm.IsNevenActieZichtbaar);
        Assert.Equal("Bestelbon aanmaken", vm.NevenActieTekst);
    }

    [Fact]
    public void Geannuleerd_toont_ook_geen_bestelbonknop()
    {
        var vm = new StappenBalkViewModel();
        vm.Toon(VerkoopStapResolver.Bepaal(OfferteStatus.Geannuleerd, null, null, 0m), "x", Volgende, "Bestelbon afdrukken", Volgende);
        Assert.False(vm.IsNevenActieZichtbaar);
    }
}
