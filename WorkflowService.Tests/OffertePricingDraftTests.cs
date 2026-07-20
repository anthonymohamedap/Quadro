using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Validation;
using System;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public class OffertePricingDraftTests
{
    [Fact]
    public async Task ValidateForPricing_AllowsMissingKlant()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var validator = new OfferteValidator(factory);
        var offerte = CreateOfferte(typeLijst: new TypeLijst { Id = 1, BreedteCm = 5, PrijsPerMeter = 10m, WerkMinuten = 0 });
        offerte.KlantId = null;

        var result = await validator.ValidateForPricingAsync(offerte);

        Assert.DoesNotContain(result.Items, i => i.Field == nameof(Offerte.KlantId));
    }

    [Fact]
    public async Task ValidateForPricing_AllowsMissingTypeLijst_WhenAfgesprokenPrijsIsSet()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var validator = new OfferteValidator(factory);
        var offerte = CreateOfferte(typeLijst: null, afgesprokenPrijsExcl: 25m);

        var result = await validator.ValidateForPricingAsync(offerte);

        Assert.DoesNotContain(result.Items, i => i.Field.Contains(nameof(OfferteRegel.TypeLijstId), StringComparison.Ordinal) && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task ValidateForPricing_AllowsMissingTypeLijst_ForPartialDraftCalculation()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var validator = new OfferteValidator(factory);
        var offerte = CreateOfferte(typeLijst: null);

        var result = await validator.ValidateForPricingAsync(offerte);

        Assert.DoesNotContain(result.Items, i => i.Field.Contains(nameof(OfferteRegel.TypeLijstId), StringComparison.Ordinal));
    }

    [Fact]
    public async Task PricingService_BerekenAsync_UpdatesDraftOfferteWithoutSaving()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var service = new PricingService(
            factory,
            new FixedPricingSettingsProvider(),
            new PricingEngine(),
            NullLogger<PricingService>.Instance);

        var offerte = CreateOfferte(new TypeLijst
        {
            BreedteCm = 5,
            Soort = "HOU",
            WinstFactor = 3.5m,
            AfvalPercentage = 20m,
            PrijsPerMeter = 10m,
            VasteKost = 1m,
            WerkMinuten = 30
        });

        await service.BerekenAsync(offerte);

        var regel = Assert.Single(offerte.Regels);
        Assert.Equal(101.30m, regel.TotaalExcl);
        Assert.Equal(101.30m, offerte.SubtotaalExBtw);
        Assert.Equal(21.27m, offerte.BtwBedrag);
        Assert.Equal(122.57m, offerte.TotaalInclBtw);
    }

    private static Offerte CreateOfferte(TypeLijst? typeLijst, decimal? afgesprokenPrijsExcl = null)
    {
        return new Offerte
        {
            Datum = DateTime.Today,
            Regels =
            [
                new OfferteRegel
                {
                    AantalStuks = 1,
                    BreedteCm = 30m,
                    HoogteCm = 40m,
                    TypeLijst = typeLijst,
                    TypeLijstId = typeLijst?.Id,
                    AfgesprokenPrijsExcl = afgesprokenPrijsExcl
                }
            ]
        };
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
