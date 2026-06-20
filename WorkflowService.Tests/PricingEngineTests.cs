using QuadroApp.Model.DB;
using QuadroApp.Service.Pricing;
using Xunit;

namespace WorkflowService.Tests;

public class PricingEngineTests
{
    private readonly PricingEngine _sut = new();

    [Fact]
    public void Calculate_LijstWithOwnPricing_UsesLijstValues()
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
                        WinstFactor = 3.5m,
                        AfvalPercentage = 20m,
                        PrijsPerMeter = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 0m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(101.30m, regel.TotaalExcl);
    }

    [Fact]
    public void Calculate_LijstWithoutPricing_FallsBackToDefault()
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
                        WinstFactor = null,
                        AfvalPercentage = null,
                        PrijsPerMeter = 10m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 0m, 1m, 10m);

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

        var result = _sut.Calculate(offerte, 60m, 21m, 0m, 1m, 10m);

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
                        WinstFactor = 3.5m,
                        AfvalPercentage = 20m,
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

        var result = _sut.Calculate(offerte, 60m, 21m, 0m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        // Afgesproken prijs wordt incl. BTW ingegeven (UI-label "incl. BTW") en door de
        // engine teruggerekend naar excl. (÷ 1,21). Bij 25 incl. × 2 stuks is het
        // incl.-totaal dus exact 50, ongeacht de berekende regelprijs.
        Assert.Equal(50m, regel.TotaalInclBtw);
    }

    // US-22: een afgesproken prijs VERVANGT de berekende regelprijs, en het wissen
    // ervan herstelt het totaal naar de oorspronkelijke berekening.
    // (AfgesprokenPrijsExcl wordt door de gebruiker incl. BTW ingegeven en door de
    //  engine gedeeld door 1+btw → 121 incl. = 100,00 excl.)
    [Fact]
    public void Calculate_AfgesprokenPrijs_ReplacesAndRestoresOnClear()
    {
        OfferteRegel Regel(decimal? afgesproken) => new OfferteRegel
        {
            AantalStuks = 1,
            BreedteCm = 30m,
            HoogteCm = 40m,
            AfgesprokenPrijsExcl = afgesproken,
            TypeLijst = new TypeLijst
            {
                BreedteCm = 5,
                Soort = "HOU",
                WinstFactor = 3.5m,
                AfvalPercentage = 20m,
                PrijsPerMeter = 10m,
                VasteKost = 1m,
                WerkMinuten = 30
            }
        };

        decimal Total(decimal? afgesproken) =>
            _sut.Calculate(new Offerte { Regels = [Regel(afgesproken)] }, 60m, 21m, 0m, 1m, 10m)
                .Regels[0].TotaalExcl;

        var berekend = Total(null);          // pure berekening
        var metAfgesproken = Total(121m);    // 121 incl. → 100,00 excl. (vervangt)
        var naWissen = Total(null);          // terug naar berekening

        Assert.Equal(101.30m, berekend);          // identiek aan de pure-berekening test
        Assert.Equal(100m, metAfgesproken);       // afgesproken prijs vervangt
        Assert.NotEqual(berekend, metAfgesproken);
        Assert.Equal(berekend, naWissen);         // wissen herstelt
    }

    [Fact]
    public void Calculate_LijstWithoutPrijsPerMeter_FallsBackToDefaultPrijsPerMeter()
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
                        WinstFactor = null,
                        AfvalPercentage = null,
                        PrijsPerMeter = 0m,
                        VasteKost = 1m,
                        WerkMinuten = 30
                    }
                }
            ]
        };

        var result = _sut.Calculate(offerte, 60m, 21m, 10m, 1m, 10m);

        var regel = Assert.Single(result.Regels);
        Assert.Equal(51.90m, regel.TotaalExcl);
    }
}
