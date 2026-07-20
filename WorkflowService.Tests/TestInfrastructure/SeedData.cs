using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public static class SeedData
{
    public static async Task<Leverancier> AddLeverancierAsync(IDbContextFactory<AppDbContext> factory, string naam = "LEV")
    {
        await using var db = await factory.CreateDbContextAsync();

        var existing = await db.Leveranciers.SingleOrDefaultAsync(l => l.Naam == naam);
        if (existing is not null)
        {
            return existing;
        }

        var leverancier = new Leverancier { Naam = naam };
        db.Leveranciers.Add(leverancier);
        await db.SaveChangesAsync();
        return leverancier;
    }

    public static async Task<TypeLijst> AddTypeLijstAsync(
        IDbContextFactory<AppDbContext> factory,
        string artikelnummer,
        string leverancierNaam = "LEV")
    {
        await using var db = await factory.CreateDbContextAsync();

        var leverancier = await db.Leveranciers.SingleOrDefaultAsync(l => l.Naam == leverancierNaam);
        if (leverancier is null)
        {
            leverancier = new Leverancier { Naam = leverancierNaam };
            db.Leveranciers.Add(leverancier);
        }

        var lijst = new TypeLijst
        {
            Artikelnummer = artikelnummer,
            Leverancier = leverancier,
            Levcode = "A1",
            BreedteCm = 10,
            Soort = "ALU",
            PrijsPerMeter = 10m,
            VasteKost = 1m,
            WerkMinuten = 5,
            VoorraadMeter = 0m,
            MinimumVoorraad = 0m,
            InventarisKost = 0m
        };

        db.TypeLijsten.Add(lijst);
        await db.SaveChangesAsync();
        return lijst;
    }
}
