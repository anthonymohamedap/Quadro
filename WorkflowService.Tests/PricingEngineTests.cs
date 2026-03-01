using QuadroApp.Model.DB;
using QuadroApp.Service.Pricing;
using Xunit;

namespace WorkflowService.Tests;

public class PricingEngineTests
{
    private readonly PricingEngine _sut = new();

    [Fact]
    public void Calculate_TypeLijstOnlyRegel_ComputesExpectedLineAndHeaderTotals()
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
                        PrijsPerMeter = 10m,
                        WinstMargeFactor = 2m,
                        AfvalPercentage = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, uurloon: 60m, btwPercent: 21m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(78.85m, regel.TotaalExcl);
        Assert.Equal(78.85m, regel.SubtotaalExBtw);
        Assert.Equal(16.56m, regel.BtwBedrag);
        Assert.Equal(95.41m, regel.TotaalInclBtw);

        Assert.Equal(78.85m, result.SubtotaalExBtw);
        Assert.Equal(16.56m, result.BtwBedrag);
        Assert.Equal(95.41m, result.TotaalInclBtw);
    }

    [Fact]
    public void Calculate_RegelMetTweeAfwerkingsOpties_ComputesExpectedTotals()
    {
        var offerte = new Offerte
        {
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 2,
                    BreedteCm = 20m,
                    HoogteCm = 10m,
                    AfgesprokenPrijsExcl = 50m,
                    ExtraWerkMinuten = 30,
                    ExtraPrijs = 5m,
                    Korting = 3m,
                    Glas = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 20m,
                        VasteKost = 2m,
                        AfvalPercentage = 10m,
                        WinstMarge = 0.5m,
                        WerkMinuten = 15
                    },
                    Rug = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 10m,
                        VasteKost = 1m,
                        AfvalPercentage = 5m,
                        WinstMarge = 0.2m,
                        WerkMinuten = 0
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, uurloon: 60m, btwPercent: 21m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(221.36m, regel.TotaalExcl);
        Assert.Equal(46.49m, regel.BtwBedrag);
        Assert.Equal(267.85m, regel.TotaalInclBtw);
    }

    [Fact]
    public void Calculate_OfferteKortingMeerprijsEnVoorschotClamp_StaysStable()
    {
        var offerte = new Offerte
        {
            KortingPct = 10m,
            MeerPrijsIncl = 12.1m,
            VoorschotBedrag = 300m,
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 2,
                    BreedteCm = 20m,
                    HoogteCm = 10m,
                    AfgesprokenPrijsExcl = 50m,
                    ExtraWerkMinuten = 30,
                    ExtraPrijs = 5m,
                    Korting = 3m,
                    Glas = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 20m,
                        VasteKost = 2m,
                        AfvalPercentage = 10m,
                        WinstMarge = 0.5m,
                        WerkMinuten = 15
                    },
                    Rug = new AfwerkingsOptie
                    {
                        KostprijsPerM2 = 10m,
                        VasteKost = 1m,
                        AfvalPercentage = 5m,
                        WinstMarge = 0.2m,
                        WerkMinuten = 0
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, uurloon: 60m, btwPercent: 21m);

        Assert.Equal(219.22m, result.SubtotaalExBtw);
        Assert.Equal(48.14m, result.BtwBedrag);
        Assert.Equal(267.36m, result.TotaalInclBtw);
        Assert.Equal(267.36m, result.VoorschotBedrag);
    }
}
