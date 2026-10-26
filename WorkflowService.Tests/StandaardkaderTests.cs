using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Converters;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-80 — standaardkader: omschrijving zichtbaar en afwerkingen mogelijk.</summary>
public class StandaardkaderTests
{
    private static KantKlaarKader C2(string beschrijving) => new() { Naam = "C2", Beschrijving = beschrijving, PrijsPerStukExcl = 45m };

    [Fact]
    public void Weergave_is_naam_en_omschrijving()
    {
        Assert.Equal("C2 – Alu zwart 50×70", C2("Alu zwart 50×70").Weergave);
        Assert.Equal("C2", new KantKlaarKader { Naam = "C2" }.Weergave);
        Assert.Equal("C2", new KantKlaarKader { Naam = "C2", Beschrijving = "  " }.Weergave);
    }

    [Fact]
    public void Regel_toont_standaardkader_met_omschrijving()
    {
        Assert.Equal("Standaardkader C2 – Alu zwart 50×70",
            new OfferteRegel { KantKlaarKader = C2("Alu zwart 50×70") }.StandaardkaderLabel);
        Assert.Equal("", new OfferteRegel().StandaardkaderLabel);
    }

    [Theory]
    [InlineData("c2", true)]
    [InlineData("c2 zwart", true)]
    [InlineData("zwart 50", true)]
    [InlineData("ZWART c2", true)]
    [InlineData("c2 zilver", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Zoeken_op_naam_en_omschrijving(string? zoekterm, bool verwacht)
        => Assert.Equal(verwacht, OfferteRegelViewModel.PastBijZoekterm(C2("Alu zwart 50×70"), zoekterm));

    [Fact]
    public async Task Bestelbon_toont_omschrijving_afmeting_en_afwerkingen()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int offerteId;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var groep = new AfwerkingsGroep { Code = 'Z', Naam = "Testglas" };
            var glas = new AfwerkingsOptie
            {
                AfwerkingsGroep = groep, Naam = "Helder glas 2 mm", Volgnummer = '1',
                KostprijsPerM2 = 10m, WinstMarge = 2m, AfvalPercentage = 10m
            };
            var offerte = new Offerte
            {
                Datum = DateTime.Today,
                Klant = new Klant { Voornaam = "Test", Achternaam = "Quadro" },
                Regels =
                {
                    new OfferteRegel
                    {
                        AantalStuks = 1, BreedteCm = 50, HoogteCm = 70, Titel = "Trouwfoto",
                        KantKlaarKader = C2("Alu zwart 50×70"), Glas = glas
                    }
                }
            };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();
            offerteId = offerte.Id;
        }

        var pricing = new PricingService(scope.Factory, new FixedPricingSettingsProvider(), new PricingEngine(), NullLogger<PricingService>.Instance);
        var factuur = await new FactuurWorkflowService(scope.Factory, pricing, new TestAuthService()).MaakFactuurVanOfferteAsync(offerteId);

        var lijn = Assert.Single(factuur.Lijnen, l => l.Omschrijving.Contains("standaardkader"));
        Assert.StartsWith("C2 – Alu zwart 50×70 | standaardkader 50x70 cm", lijn.Omschrijving);
        Assert.Contains("titel:Trouwfoto", lijn.Omschrijving);
        Assert.Contains("glas:", lijn.Omschrijving);
        Assert.True(lijn.TotaalExcl > 45m, "Glas telt mee bovenop de stukprijs van het kader.");

        // Bestelbonscherm: leesbare omschrijving.
        var tekst = (string)new FactuurOmschrijvingConverter().Convert(lijn.Omschrijving, typeof(string), null, CultureInfo.InvariantCulture)!;
        Assert.Contains("Standaardkader C2 – Alu zwart 50×70", tekst);
        Assert.Contains("50x70 cm", tekst);
        Assert.DoesNotContain("standaardkader 50x70", tekst);
    }

    [Fact]
    public void Oude_bestelbonlijn_zonder_afmeting_blijft_leesbaar()
    {
        var tekst = (string)new FactuurOmschrijvingConverter()
            .Convert("Classic A4 zwart | standaardkader | titel:Foto", typeof(string), null, CultureInfo.InvariantCulture)!;
        Assert.Contains("Standaardkader Classic A4 zwart", tekst);
        Assert.False(FactuurWorkflowService.IsStandaardkaderSegment("40x50 cm"));
        Assert.True(FactuurWorkflowService.IsStandaardkaderSegment("standaardkader"));
        Assert.True(FactuurWorkflowService.IsStandaardkaderSegment("standaardkader 50x70 cm"));
    }

    private sealed class FixedPricingSettingsProvider : IPricingSettingsProvider
    {
        public Task<decimal> GetUurloonAsync() => Task.FromResult(60m);
        public Task<decimal> GetBtwPercentAsync() => Task.FromResult(21m);
        public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(0m);
        public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(1m);
        public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(10m);
    }
}
