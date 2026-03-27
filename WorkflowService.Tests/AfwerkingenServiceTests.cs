using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Validation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

public class AfwerkingenServiceTests
{
    [Fact]
    public async Task Validator_allows_same_family_with_different_color()
    {
        var factory = CreateFactory();
        await SeedAfwerkingAsync(factory, '1', "Blauw");

        var validator = new AfwerkingsOptieValidator(factory);
        var result = await validator.ValidateCreateAsync(new AfwerkingsOptie
        {
            AfwerkingsGroepId = 1,
            Naam = "Familie 1 zwart",
            Volgnummer = '1',
            Kleur = "Zwart"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_rejects_duplicate_color_in_same_family()
    {
        var factory = CreateFactory();
        await SeedAfwerkingAsync(factory, '1', "Blauw");

        var validator = new AfwerkingsOptieValidator(factory);
        var result = await validator.ValidateCreateAsync(new AfwerkingsOptie
        {
            AfwerkingsGroepId = 1,
            Naam = "Familie 1 blauw duplicate",
            Volgnummer = '1',
            Kleur = "Blauw"
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task SaveOptieAsync_syncs_pricing_across_family_variants()
    {
        var factory = CreateFactory();
        var eersteId = await SeedAfwerkingAsync(factory, '1', "Blauw");
        var tweedeId = await SeedAfwerkingAsync(factory, '1', "Rood");

        var service = new AfwerkingenService(factory);

        await service.SaveOptieAsync(new AfwerkingsOptie
        {
            Id = eersteId,
            AfwerkingsGroepId = 1,
            Naam = "Familie 1 blauw",
            Volgnummer = '1',
            Kleur = "Blauw",
            KostprijsPerM2 = 12.5m,
            WinstMarge = 0.35m,
            AfvalPercentage = 4m,
            VasteKost = 7m,
            WerkMinuten = 20
        });

        await using var db = await factory.CreateDbContextAsync();
        var opties = await db.AfwerkingsOpties
            .Where(x => x.Id == eersteId || x.Id == tweedeId)
            .OrderBy(x => x.Kleur)
            .ToListAsync();

        Assert.Equal(2, opties.Count);
        Assert.All(opties, optie =>
        {
            Assert.Equal(12.5m, optie.KostprijsPerM2);
            Assert.Equal(0.35m, optie.WinstMarge);
            Assert.Equal(4m, optie.AfvalPercentage);
            Assert.Equal(7m, optie.VasteKost);
            Assert.Equal(20, optie.WerkMinuten);
        });
    }

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PooledDbContextFactory<AppDbContext>(options);
    }

    private static async Task<int> SeedAfwerkingAsync(IDbContextFactory<AppDbContext> factory, char volgnummer, string kleur)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (!await db.AfwerkingsGroepen.AnyAsync(x => x.Id == 1))
        {
            db.AfwerkingsGroepen.Add(new AfwerkingsGroep
            {
                Id = 1,
                Code = 'G',
                Naam = "Glas"
            });
            await db.SaveChangesAsync();
        }

        var optie = new AfwerkingsOptie
        {
            AfwerkingsGroepId = 1,
            Naam = $"Familie {volgnummer} {kleur}",
            Volgnummer = volgnummer,
            Kleur = kleur,
            KostprijsPerM2 = 5m,
            WinstMarge = 0.25m,
            AfvalPercentage = 1m,
            VasteKost = 2m,
            WerkMinuten = 10
        };

        db.AfwerkingsOpties.Add(optie);
        await db.SaveChangesAsync();
        return optie.Id;
    }
}
