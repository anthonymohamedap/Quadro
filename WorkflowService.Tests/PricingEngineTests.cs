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
        Assert.Equal(101.30m, regel.TotaalExcl);
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
        Assert.Equal(51.90m, regel.TotaalExcl);
    }

    [Fact]
    public void Calculate_AfwerkingsOptie_UsesAreaMarginWasteFixedCostAndLabor()
    {
        var offerte = new Offerte
        {
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 1,
                    BreedteCm = 100m,
                    HoogteCm = 50m,
                    Glas = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 10m,
                        WinstMarge = 2m,
                        AfvalPercentage = 20m,
                        VasteKost = 3m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 3.5m, 20m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(44m, regel.TotaalExcl);
    }

    [Fact]
    public void Calculate_AfgesprokenPrijs_OverridesCalculatedRegel()
    {
        var offerte = new Offerte
        {
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 2,
                    BreedteCm = 30m,
                    HoogteCm = 40m,
                    AfgesprokenPrijsExcl = 25m,
                    ExtraPrijs = 99m,
                    Korting = 10m,
                    ExtraWerkMinuten = 60,
                    TypeLijst = new TypeLijst
                    {
                        BreedteCm = 5,
                        Soort = "HOU",
                        IsStaaflijst = true,
                        PrijsPerMeter = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    },
                    Glas = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 100m,
                        WinstMarge = 2m,
                        AfvalPercentage = 10m,
                        VasteKost = 5m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 3.5m, 20m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(50m, regel.TotaalExcl);
    }
}
