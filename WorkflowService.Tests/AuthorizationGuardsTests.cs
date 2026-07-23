using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Security;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// P3 — borgt dat gevoelige schrijfpaden een permissie afdwingen op de service-laag.
/// De permissie-check is de eerste regel van elke methode, dus deze tests raken de
/// database nooit; ze verifiëren enkel de guard.
/// </summary>
public class AuthorizationGuardsTests
{
    private static TestAuthService Deny() => new() { AlleRechten = false };

    [Fact]
    public async Task Archief_permanent_verwijderen_vereist_recht()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var sut = new OfferteArchiefService(factory, Deny(), NullLogger<OfferteArchiefService>.Instance);

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.VerwijderenAsync(1));
    }

    [Fact]
    public async Task Afwerkingsoptie_verwijderen_vereist_recht()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var sut = new AfwerkingenService(factory, Deny());

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(
            () => sut.DeleteOptieAsync(new AfwerkingsOptie { Id = 1 }));
    }

    [Fact]
    public async Task Afwerkingsvariant_verwijderen_vereist_recht()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        var sut = new AfwerkingenService(factory, Deny());

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.DeleteVariantAsync(1));
    }

    [Fact]
    public async Task Factuur_maken_vereist_recht()
    {
        var factory = DbFactoryBuilder.CreateInMemoryFactory();
        // pricing wordt niet bereikt: de permissie-check gooit als eerste.
        var sut = new FactuurWorkflowService(factory, null!, Deny());

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.MaakFactuurVanOfferteAsync(1));
    }
}
