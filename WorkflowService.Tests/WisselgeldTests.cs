using QuadroApp.ViewModels;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-74 — wisselgeld bij een cash winkelverkoop.</summary>
public class WisselgeldTests
{
    [Theory]
    [InlineData(50, 18, 32)]
    [InlineData(20, 18.40, 1.60)]
    [InlineData(18, 18, 0)]
    [InlineData(10, 18, 0)]      // te weinig ontvangen → nooit negatief
    public void Terug_is_ontvangen_min_totaal(double ontvangen, double totaal, double verwacht)
    {
        Assert.Equal((decimal)verwacht, WinkelVerkopenViewModel.BerekenTerug((decimal)ontvangen, (decimal)totaal));
    }

    [Fact]
    public void Zonder_ontvangen_bedrag_is_er_niets_terug()
    {
        Assert.Equal(0m, WinkelVerkopenViewModel.BerekenTerug(null, 18m));
    }
}
