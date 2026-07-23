using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// P2 — borgt dat een productie-installatie (alleen SeedReferenceData) GEEN demo-data
/// krijgt, en dat SeedDemoData de demo-entiteiten wél aanmaakt.
/// </summary>
public class SeederTests
{
    [Fact]
    public async Task SeedReferenceData_seeds_reference_but_no_demo()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await dbScope.Factory.CreateDbContextAsync();

        DbSeeder.SeedReferenceData(db);

        // Referentiedata aanwezig
        Assert.Equal(5, await db.AfwerkingsGroepen.CountAsync());
        Assert.True(await db.Instellingen.AnyAsync(i => i.Sleutel == "Uurloon"));
        Assert.True(await db.Instellingen.AnyAsync(i => i.Sleutel == "BtwPercent"));

        // Geen enkele demo-entiteit
        Assert.Equal(0, await db.Klanten.CountAsync());
        Assert.Equal(0, await db.Leveranciers.CountAsync());
        Assert.Equal(0, await db.TypeLijsten.CountAsync());
        Assert.Equal(0, await db.AfwerkingsOpties.CountAsync());
    }

    [Fact]
    public async Task SeedDemoData_seeds_demo_entities()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await dbScope.Factory.CreateDbContextAsync();

        DbSeeder.SeedReferenceData(db);
        DbSeeder.SeedDemoData(db);

        Assert.True(await db.Klanten.CountAsync() > 0);
        Assert.Equal(4, await db.Leveranciers.CountAsync());
        Assert.Equal(10, await db.TypeLijsten.CountAsync());
        Assert.True(await db.AfwerkingsOpties.CountAsync() > 0);
    }

    [Fact]
    public async Task SeedReferenceData_is_idempotent()
    {
        await using var dbScope = await DbFactoryBuilder.CreateSqliteAsync();
        await using var db = await dbScope.Factory.CreateDbContextAsync();

        DbSeeder.SeedReferenceData(db);
        DbSeeder.SeedReferenceData(db);

        Assert.Equal(5, await db.AfwerkingsGroepen.CountAsync());
        Assert.Equal(1, await db.Instellingen.CountAsync(i => i.Sleutel == "Uurloon"));
    }
}
