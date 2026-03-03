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
            db.Instellingen.Add(new Instelling { Sleutel = "StaaflijstWinstFactor", Waarde = "invalid" });
            await db.SaveChangesAsync();
        }

        var sut = new PricingSettingsProvider(factory);
        Assert.Equal(3.5m, await sut.GetStaaflijstWinstFactorAsync());
        Assert.Equal(20m, await sut.GetStaaflijstAfvalPercentageAsync());
    }
}
