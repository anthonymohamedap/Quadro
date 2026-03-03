using QuadroApp.Model.DB;
using QuadroApp.Service.Pricing;
using Xunit;

namespace WorkflowService.Tests;

public class PricingEngineTests
{
    private readonly PricingEngine _sut = new();

    [Fact]
    public void Calculate_Staaflijst_UsesGlobalSettings()
    {
        var offerte = new Offerte
        {
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 1,
                    BreedteCm = 30m,
                    HoogteCm = 40m,
                    TypeLijst = new TypeLijst
                    {
                        BreedteCm = 5,
                        Soort = "HOU",
                        IsStaaflijst = true,
                        PrijsPerMeter = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 3.5m, 20m, 0m, 0m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(116.50m, regel.TotaalExcl);
    }

    [Fact]
    public void Calculate_NonStaaflijst_UsesDefaultSettings()
    {
        var offerte = new Offerte
        {
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 1,
                    BreedteCm = 30m,
                    HoogteCm = 40m,
                    TypeLijst = new TypeLijst
                    {
                        BreedteCm = 5,
                        Soort = "ALU",
                        IsStaaflijst = false,
                        PrijsPerMeter = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 3.5m, 20m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(76.90m, regel.TotaalExcl);
    }
}
