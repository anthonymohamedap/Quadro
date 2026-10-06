using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-66 — DbContext round-trip voor <see cref="Ontvangst"/> (centraal ontvangstenregister):
/// enums als string, winkelverkoop-totaal, en koppeling naar offerte die NULL wordt bij verwijderen.</summary>
public class OntvangstTests
{
    [Fact]
    public async Task Winkelverkoop_persisteert_Soort_en_Betaalwijze_als_string()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();

        var verkoop = new Ontvangst
        {
            Soort = OntvangstSoort.Winkelverkoop,
            Datum = new DateTime(2026, 9, 10),
            Omschrijving = "Ophangsysteem",
            Aantal = 3m,
            PrijsPerStukIncl = 36.20m,
            Betaalwijze = Betaalwijze.Bancontact
        };
        verkoop.BedragIncl = verkoop.TotaalIncl;

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            db.Ontvangsten.Add(verkoop);
            await db.SaveChangesAsync();
        }

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var herladen = await db.Ontvangsten.AsNoTracking().SingleAsync(v => v.Id == verkoop.Id);

            Assert.Equal(OntvangstSoort.Winkelverkoop, herladen.Soort);
            Assert.Equal(Betaalwijze.Bancontact, herladen.Betaalwijze);
            Assert.Equal(108.60m, herladen.BedragIncl);   // zelfde als de kassa-foto: 3 × 36.20

            var raw = await db.Database
                .SqlQueryRaw<string>("SELECT Betaalwijze || '/' || Soort AS Value FROM Ontvangsten WHERE Id = {0}", verkoop.Id)
                .SingleAsync();
            Assert.Equal("Bancontact/Winkelverkoop", raw);
        }
    }

    [Theory]
    [InlineData(2, 93.35, 0, 186.70)]
    [InlineData(1, 99.85, 10, 89.87)]    // 89.865 → 89.87
    [InlineData(2, 34.41, 100, 0)]
    public void TotaalIncl_is_aantal_maal_prijs_min_korting(double aantal, double prijs, double korting, double verwacht)
    {
        var o = new Ontvangst { Aantal = (decimal)aantal, PrijsPerStukIncl = (decimal)prijs, KortingPct = (decimal)korting };
        Assert.Equal((decimal)verwacht, o.TotaalIncl);
    }

    [Fact]
    public async Task Koppeling_naar_offerte_wordt_null_als_offerte_verdwijnt()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int ontvangstId;

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "An", Achternaam = "Test" };
            var offerte = new Offerte { Datum = DateTime.Today, Klant = klant };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();

            var o = new Ontvangst { Soort = OntvangstSoort.Voorschot, OfferteId = offerte.Id, BedragIncl = 50m, Betaalwijze = Betaalwijze.Kontant };
            db.Ontvangsten.Add(o);
            await db.SaveChangesAsync();
            ontvangstId = o.Id;

            db.Offertes.Remove(offerte);
            await db.SaveChangesAsync();
        }

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var o = await db.Ontvangsten.AsNoTracking().SingleAsync(x => x.Id == ontvangstId);
            Assert.Null(o.OfferteId);          // ontvangst blijft voor de boekhouding
            Assert.Equal(50m, o.BedragIncl);
        }
    }
}
