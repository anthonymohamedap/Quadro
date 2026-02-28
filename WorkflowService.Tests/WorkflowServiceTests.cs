using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using Xunit;

namespace WorkflowService.Tests;

public class WorkflowServiceTests
{
    [Fact]
    public async Task Valid_status_transition_succeeds()
    {
        var factory = CreateFactory();
        await SeedOfferteAsync(factory, 1, OfferteStatus.Concept);

        var sut = new QuadroApp.Service.WorkflowService(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance);

        await sut.ChangeOfferteStatusAsync(1, OfferteStatus.Verzonden);

        await using var db = await factory.CreateDbContextAsync();
        var offerte = await db.Offertes.FindAsync(1);
        Assert.NotNull(offerte);
        Assert.Equal(OfferteStatus.Verzonden, offerte!.Status);
    }

    [Fact]
    public async Task Invalid_transition_throws_exception()
    {
        var factory = CreateFactory();
        await SeedOfferteAsync(factory, 2, OfferteStatus.Concept);

        var sut = new QuadroApp.Service.WorkflowService(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChangeOfferteStatusAsync(2, OfferteStatus.Betaald));
    }

    [Fact]
    public async Task WerkBon_is_created_when_Offerte_becomes_Goedgekeurd()
    {
        var factory = CreateFactory();
        await SeedOfferteAsync(factory, 3, OfferteStatus.Verzonden);

        var sut = new QuadroApp.Service.WorkflowService(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance);

        await sut.ChangeOfferteStatusAsync(3, OfferteStatus.Goedgekeurd);

        await using var db = await factory.CreateDbContextAsync();
        var count = await db.WerkBonnen.CountAsync(w => w.OfferteId == 3);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Duplicate_WerkBon_is_not_created_if_already_exists()
    {
        var factory = CreateFactory();
        await SeedOfferteAsync(factory, 4, OfferteStatus.Verzonden, createWerkBon: true);

        var sut = new QuadroApp.Service.WorkflowService(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance);

        await sut.ChangeOfferteStatusAsync(4, OfferteStatus.Goedgekeurd);

        await using var db = await factory.CreateDbContextAsync();
        var count = await db.WerkBonnen.CountAsync(w => w.OfferteId == 4);
        Assert.Equal(1, count);
    }

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PooledDbContextFactory<AppDbContext>(options);
    }

    private static async Task SeedOfferteAsync(IDbContextFactory<AppDbContext> factory, int id, OfferteStatus status, bool createWerkBon = false)
    {
        await using var db = await factory.CreateDbContextAsync();

        var offerte = new Offerte
        {
            Id = id,
            Datum = DateTime.UtcNow,
            Status = status,
            TotaalInclBtw = 100
        };

        db.Offertes.Add(offerte);

        if (createWerkBon)
        {
            db.WerkBonnen.Add(new WerkBon
            {
                OfferteId = id,
                Status = WerkBonStatus.Gepland,
                TotaalPrijsIncl = 100
            });
        }

        await db.SaveChangesAsync();
    }
}
