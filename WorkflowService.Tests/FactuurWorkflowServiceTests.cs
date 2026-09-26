using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public class FactuurWorkflowServiceTests
{
    [Fact]
    public async Task MaakFactuurVanOfferteAsync_creates_factuur_without_werkbon()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory);
        var sut = CreateSut(factory);

        var factuur = await sut.MaakFactuurVanOfferteAsync(offerteId);

        Assert.Equal(offerteId, factuur.OfferteId);
        Assert.Null(factuur.WerkBonId);
        Assert.Equal(FactuurStatus.Draft, factuur.Status);
        Assert.NotEmpty(factuur.Lijnen);
        Assert.Equal("Factuur", factuur.DocumentType);
    }

    [Fact]
    public async Task GetFactuurVoorOfferteAsync_returns_factuur_created_from_offerte()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory);
        var sut = CreateSut(factory);

        var created = await sut.MaakFactuurVanOfferteAsync(offerteId);
        var loaded = await sut.GetFactuurVoorOfferteAsync(offerteId);

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal(offerteId, loaded.OfferteId);
    }

    [Fact]
    public async Task MaakFactuurVanWerkBonAsync_backfills_offerte_link()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory, createWerkBon: true);
        var sut = CreateSut(factory);

        await using var db = await factory.CreateDbContextAsync();
        var werkBonId = await db.WerkBonnen.Where(x => x.OfferteId == offerteId).Select(x => x.Id).SingleAsync();
        await db.DisposeAsync();

        var factuur = await sut.MaakFactuurVanWerkBonAsync(werkBonId);

        Assert.Equal(offerteId, factuur.OfferteId);
        Assert.Equal(werkBonId, factuur.WerkBonId);
    }

    [Fact]
    public async Task MaakFactuurVanOfferteAsync_recalculates_when_offerte_totals_are_zero()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory, zeroOutTotals: true);
        var sut = CreateSut(factory);

        var factuur = await sut.MaakFactuurVanOfferteAsync(offerteId);

        // Afgesproken prijs (25) is incl. BTW → regel-incl. = 25, excl. = 25 / 1,21 = 20,66.
        Assert.Equal(20.66m, factuur.TotaalExclBtw);
        Assert.Equal(25m, factuur.TotaalInclBtw);
        var lijn = Assert.Single(factuur.Lijnen);
        Assert.Equal(20.66m, lijn.TotaalExcl);
        Assert.Equal(25m, lijn.TotaalIncl);
    }

    [Fact]
    public async Task Meerprijs_zit_in_totaal_maar_niet_als_zichtbare_regel()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory, meerPrijsIncl: 50m);
        var sut = CreateSut(factory);

        var factuur = await sut.MaakFactuurVanOfferteAsync(offerteId);

        // De meerprijs-regel bestaat wél in de data (zodat het totaal klopt),
        // maar wordt op de bestelbon-PDF én in de preview niet getoond (US-26).
        var meerprijsLijn = factuur.Lijnen.SingleOrDefault(
            l => l.Omschrijving.Equals("Meerprijs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(meerprijsLijn);

        // Het bedrag zit verrekend in het eindtotaal: totaal incl. bevat de 50 meerprijs.
        var totaalVanZichtbareLijnen = factuur.Lijnen
            .Where(l => !l.Omschrijving.Equals("Meerprijs", StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.TotaalIncl);
        Assert.Equal(totaalVanZichtbareLijnen + meerprijsLijn!.TotaalIncl, factuur.TotaalInclBtw);
    }

    [Fact]
    public async Task RegelKorting_staat_als_tag_op_de_bestellijn_US63()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory, extraRegelKortingPct: 10m);
        var sut = CreateSut(factory);

        var factuur = await sut.MaakFactuurVanOfferteAsync(offerteId);

        // De regel met korting draagt de tag; de afgesproken-prijs-regel niet.
        var metKorting = factuur.Lijnen.Where(l => l.Omschrijving.Contains("korting:10")).ToList();
        Assert.Single(metKorting);
        Assert.Equal(90m, metKorting[0].TotaalExcl);   // lijnbedrag is al NA korting
    }

    [Fact]
    public async Task Inleg_staat_als_tag_op_de_bestellijn_US71()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = dbScope.Factory;
        var offerteId = await SeedOfferteAsync(factory, metInleg: true);
        var sut = CreateSut(factory);

        var factuur = await sut.MaakFactuurVanOfferteAsync(offerteId);

        Assert.Single(factuur.Lijnen, l => l.Omschrijving.Contains("inleg:20×30 cm · nr. INL-7"));
    }

    [Theory]
    [InlineData(20.0, 30.0, "INL-7", "20×30 cm · nr. INL-7")]
    [InlineData(20.5, 30.0, null, "20.5×30 cm")]
    [InlineData(null, null, "INL-7", "nr. INL-7")]
    [InlineData(null, null, null, null)]
    [InlineData(20.0, null, null, null)]   // halve maat telt niet als inlegmaat
    public void InlegOmschrijving_toont_enkel_ingevulde_delen_US71(double? b, double? h, string? nr, string? verwacht)
    {
        var regel = new OfferteRegel
        {
            InlegBreedteCm = b is null ? null : (decimal)b,
            InlegHoogteCm = h is null ? null : (decimal)h,
            InlegTypeLijst = nr is null ? null : new TypeLijst { Artikelnummer = nr, Levcode = "TST" }
        };

        Assert.Equal(verwacht, FactuurWorkflowService.InlegOmschrijving(regel));
    }

    private static FactuurWorkflowService CreateSut(IDbContextFactory<AppDbContext> factory)
    {
        var pricing = new PricingService(
            factory,
            new FixedPricingSettingsProvider(),
            new PricingEngine(),
            NullLogger<PricingService>.Instance);

        return new FactuurWorkflowService(factory, pricing, new TestAuthService());
    }

    private static async Task<int> SeedOfferteAsync(IDbContextFactory<AppDbContext> factory, bool createWerkBon = false, bool zeroOutTotals = false, decimal meerPrijsIncl = 0m, decimal? extraRegelKortingPct = null, bool metInleg = false)
    {
        await using var db = await factory.CreateDbContextAsync();

        var klant = new Klant
        {
            Voornaam = "Jan",
            Achternaam = "Klant",
            Straat = "Teststraat",
            Nummer = "1",
            Postcode = "3200",
            Gemeente = "Aarschot",
            BtwNummer = "BE0123456789"
        };

        var offerte = new Offerte
        {
            Datum = DateTime.Today,
            Status = OfferteStatus.Concept,
            Klant = klant,
            TotaalInclBtw = 121m,
            SubtotaalExBtw = 100m,
            BtwBedrag = 21m,
            MeerPrijsIncl = meerPrijsIncl,
            Regels =
            {
                new OfferteRegel
                {
                    AantalStuks = 1,
                    BreedteCm = 10,
                    HoogteCm = 20,
                    AfgesprokenPrijsExcl = 25m,
                    TotaalExcl = zeroOutTotals ? 0m : 25m,
                    SubtotaalExBtw = zeroOutTotals ? 0m : 25m,
                    BtwBedrag = zeroOutTotals ? 0m : 5.25m,
                    TotaalInclBtw = zeroOutTotals ? 0m : 30.25m
                }
            }
        };

        if (extraRegelKortingPct is decimal pct)
        {
            // US-63: een tweede, berekende regel (geen afgesproken prijs) met korting in %.
            offerte.Regels.Add(new OfferteRegel
            {
                AantalStuks = 1,
                BreedteCm = 30,
                HoogteCm = 40,
                KortingPct = pct,
                TotaalExcl = 90m,
                SubtotaalExBtw = 90m,
                BtwBedrag = 18.90m,
                TotaalInclBtw = 108.90m
            });
        }

        if (metInleg)
        {
            // US-71: een berekende regel met inlegmaat + inleg-nummer uit de lijstencatalogus.
            offerte.Regels.Add(new OfferteRegel
            {
                AantalStuks = 1,
                BreedteCm = 30,
                HoogteCm = 40,
                InlegBreedteCm = 20,
                InlegHoogteCm = 30,
                InlegTypeLijst = new TypeLijst { Artikelnummer = "INL-7", Levcode = "TST", BreedteCm = 2, Soort = "HOU" },
                TotaalExcl = 150m,
                SubtotaalExBtw = 150m,
                BtwBedrag = 31.50m,
                TotaalInclBtw = 181.50m
            });
        }

        if (zeroOutTotals)
        {
            offerte.SubtotaalExBtw = 0m;
            offerte.BtwBedrag = 0m;
            offerte.TotaalInclBtw = 0m;
        }

        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();

        if (createWerkBon)
        {
            db.WerkBonnen.Add(new WerkBon
            {
                OfferteId = offerte.Id,
                Status = WerkBonStatus.Afgewerkt,
                TotaalPrijsIncl = offerte.TotaalInclBtw
            });
            await db.SaveChangesAsync();
        }

        return offerte.Id;
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
