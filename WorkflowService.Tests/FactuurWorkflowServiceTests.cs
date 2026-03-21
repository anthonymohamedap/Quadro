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

        Assert.Equal(25m, factuur.TotaalExclBtw);
        Assert.Equal(30.25m, factuur.TotaalInclBtw);
        var lijn = Assert.Single(factuur.Lijnen);
        Assert.Equal(25m, lijn.PrijsExcl);
        Assert.Equal(30.25m, lijn.TotaalIncl);
    }

    private static FactuurWorkflowService CreateSut(IDbContextFactory<AppDbContext> factory)
    {
        var pricing = new PricingService(
            factory,
            new FixedPricingSettingsProvider(),
            new PricingEngine(),
            NullLogger<PricingService>.Instance);

        return new FactuurWorkflowService(factory, pricing);
    }

    private static async Task<int> SeedOfferteAsync(IDbContextFactory<AppDbContext> factory, bool createWerkBon = false, bool zeroOutTotals = false)
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
