using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-70 — betalingen op een bestelbon: deelbetalingen, rest, automatisch Betaald.</summary>
public class OntvangstServiceBetalingTests
{
    private static OntvangstService CreateSut(IDbContextFactory<AppDbContext> factory)
    {
        var auth = new TestAuthService();
        var pricing = new PricingService(factory, new VasteInstellingen(), new PricingEngine(), NullLogger<PricingService>.Instance);
        var workflow = new FactuurWorkflowService(factory, pricing, auth);
        return new OntvangstService(factory, auth, workflow);
    }

    private static async Task<int> SeedBestelbonAsync(IDbContextFactory<AppDbContext> factory, decimal totaal = 300m, decimal voorschot = 100m)
    {
        await using var db = await factory.CreateDbContextAsync();
        var offerte = new Offerte
        {
            Datum = DateTime.Today,
            Status = OfferteStatus.Afgewerkt,
            Klant = new Klant { Voornaam = "Els", Achternaam = "Betaalt" },
            TotaalInclBtw = totaal,
            VoorschotBedrag = voorschot
        };
        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();

        var factuur = new Factuur
        {
            OfferteId = offerte.Id,
            Jaar = DateTime.Today.Year,
            VolgNr = 7,
            FactuurNummer = "B-7",
            KlantNaam = "Els Betaalt",
            Status = FactuurStatus.KlaarVoorExport,
            TotaalInclBtw = totaal,
            VoorschotBedrag = voorschot
        };
        db.Facturen.Add(factuur);
        await db.SaveChangesAsync();
        return factuur.Id;
    }

    [Fact]
    public async Task Gesplitste_betaling_cash_en_kaart_zet_bestelbon_op_betaald()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factuurId = await SeedBestelbonAsync(scope.Factory);
        var sut = CreateSut(scope.Factory);

        var na1 = await sut.RegistreerBetalingAsync(factuurId, 50m, DateTime.Today, Betaalwijze.Kontant);
        Assert.Equal(150m, na1.Rest);
        Assert.False(na1.IsBetaaldStatus);

        var na2 = await sut.RegistreerBetalingAsync(factuurId, 150m, DateTime.Today, Betaalwijze.Bancontact);
        Assert.Equal(0m, na2.Rest);
        Assert.True(na2.IsBetaaldStatus);

        await using var db = await scope.Factory.CreateDbContextAsync();
        var factuur = await db.Facturen.AsNoTracking().SingleAsync(f => f.Id == factuurId);
        Assert.Equal(FactuurStatus.Betaald, factuur.Status);

        var betalingen = await sut.GetBetalingenAsync(factuurId);
        Assert.Equal(2, betalingen.Count);
        Assert.All(betalingen, b => Assert.Equal(factuur.OfferteId, b.OfferteId));
    }

    [Fact]
    public async Task Meer_dan_de_rest_betalen_wordt_geweigerd()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factuurId = await SeedBestelbonAsync(scope.Factory);
        var sut = CreateSut(scope.Factory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegistreerBetalingAsync(factuurId, 250m, DateTime.Today, Betaalwijze.Visa));
        Assert.Contains("hoger dan wat nog te betalen is", ex.Message);
    }

    [Fact]
    public async Task Vooraf_betaald_per_overschrijving_telt_mee()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factuurId = await SeedBestelbonAsync(scope.Factory, totaal: 120m, voorschot: 0m);
        var sut = CreateSut(scope.Factory);

        var stand = await sut.RegistreerBetalingAsync(factuurId, 120m, DateTime.Today.AddDays(-3), Betaalwijze.Storting);

        Assert.Equal(0m, stand.Rest);
        var b = Assert.Single(await sut.GetBetalingenAsync(factuurId));
        Assert.Equal(Betaalwijze.Storting, b.Betaalwijze);
        Assert.Equal(DateTime.Today.AddDays(-3), b.Datum);
    }

    [Fact]
    public void Rest_is_nooit_negatief()
    {
        var stand = new BetaalStand(100m, 60m, 50m, false);
        Assert.Equal(0m, stand.Rest);
    }

    private sealed class VasteInstellingen : IPricingSettingsProvider
    {
        public Task<decimal> GetUurloonAsync() => Task.FromResult(60m);
        public Task<decimal> GetBtwPercentAsync() => Task.FromResult(21m);
        public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(0m);
        public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(1m);
        public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(10m);
    }
}
