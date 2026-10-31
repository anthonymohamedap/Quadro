using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-76 — afrekenen bij afhaling vanuit de werkbon.</summary>
public class AfrekenenBijAfhalingTests
{
    private static async Task<(int WerkBonId, int BestelbonId)> SeedAsync(
        IDbContextFactory<AppDbContext> factory, WerkBonStatus status = WerkBonStatus.Afgewerkt,
        decimal totaal = 300m, decimal voorschot = 100m)
    {
        await using var db = await factory.CreateDbContextAsync();
        var offerte = new Offerte
        {
            Datum = DateTime.Today,
            Status = OfferteStatus.Besteld,
            Klant = new Klant { Voornaam = "Ann", Achternaam = "Afhaal" },
            TotaalInclBtw = totaal,
            VoorschotBedrag = voorschot
        };
        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();

        var werkBon = new WerkBon { OfferteId = offerte.Id, Status = status, TotaalPrijsIncl = totaal };
        db.WerkBonnen.Add(werkBon);
        await db.SaveChangesAsync();

        var bestelbon = new Bestelbon
        {
            OfferteId = offerte.Id, WerkBonId = werkBon.Id, Jaar = DateTime.Today.Year, VolgNr = 21,
            BestelbonNummer = "2026-21", KlantNaam = "Ann Afhaal", Status = BestelbonStatus.KlaarVoorExport,
            TotaalInclBtw = totaal, VoorschotBedrag = voorschot
        };
        db.Bestelbonnen.Add(bestelbon);
        await db.SaveChangesAsync();
        return (werkBon.Id, bestelbon.Id);
    }

    private static OntvangstService Sut(IDbContextFactory<AppDbContext> f) => new(f, new TestAuthService());

    [Fact]
    public async Task Volledig_betalen_zet_werkbon_afgehaald_en_bestelbon_en_offerte_betaald()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var (wbId, fId) = await SeedAsync(scope.Factory);

        var stand = await Sut(scope.Factory).GetAfrekenStandAsync(wbId);
        Assert.NotNull(stand);
        Assert.Equal(200m, stand!.Stand.Rest);

        var na = await Sut(scope.Factory).AfrekenenBijAfhalingAsync(wbId, 200m, DateTime.Today, Betaalwijze.Bancontact);
        Assert.Equal(0m, na.Stand.Rest);

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(WerkBonStatus.Afgehaald, (await db.WerkBonnen.SingleAsync(w => w.Id == wbId)).Status);
        var bestelbon = await db.Bestelbonnen.SingleAsync(f => f.Id == fId);
        Assert.Equal(BestelbonStatus.Betaald, bestelbon.Status);
        Assert.Equal(OfferteStatus.Betaald, (await db.Offertes.SingleAsync(o => o.Id == bestelbon.OfferteId)).Status);
        var betaling = await db.Ontvangsten.SingleAsync();
        Assert.Equal(OntvangstSoort.Betaling, betaling.Soort);
        Assert.Equal(fId, betaling.BestelbonId);
    }

    [Fact]
    public async Task Gesplitst_betalen_eerst_cash_dan_kaart()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var (wbId, fId) = await SeedAsync(scope.Factory);
        var sut = Sut(scope.Factory);

        var na1 = await sut.AfrekenenBijAfhalingAsync(wbId, 50m, DateTime.Today, Betaalwijze.Cash);
        Assert.Equal(150m, na1.Stand.Rest);

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            Assert.Equal(WerkBonStatus.Afgehaald, (await db.WerkBonnen.SingleAsync(w => w.Id == wbId)).Status);
            Assert.Equal(BestelbonStatus.KlaarVoorExport, (await db.Bestelbonnen.SingleAsync(f => f.Id == fId)).Status);
        }

        var na2 = await sut.AfrekenenBijAfhalingAsync(wbId, 150m, DateTime.Today, Betaalwijze.CreditCard);
        Assert.Equal(0m, na2.Stand.Rest);
        Assert.True(na2.Stand.IsBetaaldStatus);
    }

    [Fact]
    public async Task Te_veel_betalen_verandert_niets()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var (wbId, _) = await SeedAsync(scope.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sut(scope.Factory).AfrekenenBijAfhalingAsync(wbId, 250m, DateTime.Today, Betaalwijze.Cash));

        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(WerkBonStatus.Afgewerkt, (await db.WerkBonnen.SingleAsync(w => w.Id == wbId)).Status);
        Assert.Equal(0, await db.Ontvangsten.CountAsync());
    }

    [Fact]
    public async Task Werkbon_die_nog_niet_afgewerkt_is_kan_niet_afgerekend_worden()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var (wbId, _) = await SeedAsync(scope.Factory, WerkBonStatus.InUitvoering);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sut(scope.Factory).AfrekenenBijAfhalingAsync(wbId, 10m, DateTime.Today, Betaalwijze.Cash));
    }

    [Fact]
    public async Task Geen_bestelbon_geeft_geen_afrekenstand()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int wbId;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var offerte = new Offerte { Datum = DateTime.Today, Status = OfferteStatus.InProductie, Klant = new Klant { Voornaam = "X", Achternaam = "Y" } };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();
            var wb = new WerkBon { OfferteId = offerte.Id, Status = WerkBonStatus.Afgewerkt };
            db.WerkBonnen.Add(wb);
            await db.SaveChangesAsync();
            wbId = wb.Id;
        }

        Assert.Null(await Sut(scope.Factory).GetAfrekenStandAsync(wbId));
    }
}
