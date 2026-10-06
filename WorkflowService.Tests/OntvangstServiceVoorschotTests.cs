using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-69 — voorschotten via het ontvangstenregister.</summary>
public class OntvangstServiceVoorschotTests
{
    private static async Task<int> SeedOfferteAsync(IDbContextFactory<AppDbContext> factory, decimal legacyVoorschot = 0m, bool metBestelbon = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var offerte = new Offerte
        {
            Datum = DateTime.Today,
            Klant = new Klant { Voornaam = "Jan", Achternaam = "Voorschot" },
            TotaalInclBtw = 300m,
            VoorschotBedrag = legacyVoorschot,
            IsVoorschotBetaald = legacyVoorschot > 0m
        };
        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();

        if (metBestelbon)
        {
            db.Facturen.Add(new Factuur
            {
                OfferteId = offerte.Id,
                Jaar = DateTime.Today.Year,
                VolgNr = 1,
                FactuurNummer = "T-1",
                KlantNaam = "Jan Voorschot",
                Status = FactuurStatus.Draft,
                TotaalInclBtw = 300m,
                VoorschotBedrag = legacyVoorschot
            });
            await db.SaveChangesAsync();
        }
        return offerte.Id;
    }

    [Fact]
    public async Task Registreren_telt_op_en_bewaart_datum_en_betaalwijze()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedOfferteAsync(scope.Factory);
        var sut = new OntvangstService(scope.Factory, new TestAuthService());

        await sut.RegistreerVoorschotAsync(id, 50m, new DateTime(2026, 9, 1), Betaalwijze.Kontant);
        var totaal = await sut.RegistreerVoorschotAsync(id, 25m, new DateTime(2026, 9, 2), Betaalwijze.Bancontact);

        Assert.Equal(75m, totaal);
        var lijst = await sut.GetVoorschottenAsync(id);
        Assert.Equal(2, lijst.Count);
        Assert.Equal(Betaalwijze.Bancontact, lijst[0].Betaalwijze);   // nieuwste eerst
        Assert.All(lijst, v => Assert.Equal(OntvangstSoort.Voorschot, v.Soort));

        await using var db = await scope.Factory.CreateDbContextAsync();
        var offerte = await db.Offertes.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal(75m, offerte.VoorschotBedrag);
        Assert.True(offerte.IsVoorschotBetaald);
    }

    [Fact]
    public async Task Oud_voorschot_zonder_ontvangst_blijft_behouden()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedOfferteAsync(scope.Factory, legacyVoorschot: 100m);
        var sut = new OntvangstService(scope.Factory, new TestAuthService());

        var totaal = await sut.RegistreerVoorschotAsync(id, 20m, DateTime.Today, Betaalwijze.Visa);

        Assert.Equal(120m, totaal);
    }

    [Fact]
    public async Task Verwijderen_verlaagt_voorschot_en_synct_bestelbon()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedOfferteAsync(scope.Factory, metBestelbon: true);
        var sut = new OntvangstService(scope.Factory, new TestAuthService());

        await sut.RegistreerVoorschotAsync(id, 80m, DateTime.Today, Betaalwijze.Bancontact);
        await sut.RegistreerVoorschotAsync(id, 20m, DateTime.Today, Betaalwijze.Kontant);

        await using (var db = await scope.Factory.CreateDbContextAsync())
            Assert.Equal(100m, (await db.Facturen.AsNoTracking().SingleAsync(f => f.OfferteId == id)).VoorschotBedrag);

        var eerste = (await sut.GetVoorschottenAsync(id)).Single(v => v.BedragIncl == 80m);
        var nieuw = await sut.VerwijderAsync(eerste.Id);

        Assert.Equal(20m, nieuw);
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            Assert.Equal(20m, (await db.Offertes.AsNoTracking().SingleAsync(o => o.Id == id)).VoorschotBedrag);
            Assert.Equal(20m, (await db.Facturen.AsNoTracking().SingleAsync(f => f.OfferteId == id)).VoorschotBedrag);
            Assert.Single(await db.Ontvangsten.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task Nul_of_negatief_voorschot_wordt_geweigerd()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedOfferteAsync(scope.Factory);
        var sut = new OntvangstService(scope.Factory, new TestAuthService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegistreerVoorschotAsync(id, 0m, DateTime.Today, Betaalwijze.Kontant));
    }
}
