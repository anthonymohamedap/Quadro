using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Pricing;
using System;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

public class PricingSettingsProviderTests
{
    [Fact]
    public async Task ReturnsFallbackForMissingOrInvalidValues()
    {
        var factory = new PooledDbContextFactory<AppDbContext>(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Instellingen.Add(new Instelling { Sleutel = "DefaultWinstFactor", Waarde = "invalid" });
            await db.SaveChangesAsync();
        }

        var sut = new PricingSettingsProvider(factory);
        // Terugvalwaarden moeten gelijk zijn aan AppSettingsProvider (Instellingen-scherm).
        Assert.Equal(45m, await sut.GetUurloonAsync());
        Assert.Equal(0m, await sut.GetDefaultPrijsPerMeterAsync());
        Assert.Equal(2.5m, await sut.GetDefaultWinstFactorAsync());
        Assert.Equal(10m, await sut.GetDefaultAfvalPercentageAsync());
    }
}
