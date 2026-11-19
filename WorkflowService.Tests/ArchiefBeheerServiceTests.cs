using Microsoft.EntityFrameworkCore;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Security;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Gearchiveerde klanten en lijsten terugzetten of definitief verwijderen (afspraak 19/11/2026).</summary>
[Collection("StaticAuthState")]
public class ArchiefBeheerServiceTests
{
    [Fact]
    public async Task Klant_terugzetten_en_ongebruikte_klant_definitief_verwijderen()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int terug, weg;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var a = new Klant { Voornaam = "Terug", Achternaam = "Klant", IsGearchiveerd = true, GearchiveerdOp = DateTime.UtcNow };
            var b = new Klant { Voornaam = "Weg", Achternaam = "Klant", IsGearchiveerd = true };
            db.Klanten.AddRange(a, b);
            await db.SaveChangesAsync();
            terug = a.Id; weg = b.Id;
        }
        var sut = new ArchiefBeheerService(scope.Factory, new TestAuthService());
        Assert.Equal(2, (await sut.GearchiveerdeKlantenAsync()).Count);

        await sut.ZetKlantTerugAsync(terug);
        await sut.VerwijderKlantDefinitiefAsync(weg);

        await using var check = await scope.Factory.CreateDbContextAsync();
        var actief = Assert.Single(await check.Klanten.ToListAsync());   // query filter: enkel niet-gearchiveerd
        Assert.Equal(terug, actief.Id);
        Assert.Null(actief.GearchiveerdOp);
        Assert.Equal(1, await check.Klanten.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Klant_die_nog_gebruikt_wordt_kan_niet_definitief_weg()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int id;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var k = new Klant { Voornaam = "Met", Achternaam = "Offerte", IsGearchiveerd = true };
            db.Offertes.Add(new Offerte { Klant = k, OfferteNummer = 1, Datum = DateTime.Today, Status = OfferteStatus.Concept });
            await db.SaveChangesAsync();
            id = k.Id;
        }
        var sut = new ArchiefBeheerService(scope.Factory, new TestAuthService());

        Assert.Contains("1 verkoop", await sut.WaaromKlantNietWegAsync(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.VerwijderKlantDefinitiefAsync(id));
        await using var check = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await check.Klanten.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Niet_gearchiveerde_klant_kan_niet_definitief_weg()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int id;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var k = new Klant { Voornaam = "Actief", Achternaam = "Klant" };
            db.Klanten.Add(k); await db.SaveChangesAsync(); id = k.Id;
        }
        var sut = new ArchiefBeheerService(scope.Factory, new TestAuthService());
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.VerwijderKlantDefinitiefAsync(id));
    }

    [Fact]
    public async Task Lijst_terugzetten_en_definitief_verwijderen_enkel_als_ongebruikt()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int vrij, gebruikt;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var l1 = new TypeLijst { Artikelnummer = "VRIJ", Levcode = "X", IsGearchiveerd = true };
            var l2 = new TypeLijst { Artikelnummer = "GEBRUIKT", Levcode = "X", IsGearchiveerd = true };
            db.TypeLijsten.AddRange(l1, l2);
            db.VoorraadAlerts.Add(new VoorraadAlert { TypeLijst = l1 });
            db.VoorraadMutaties.Add(new VoorraadMutatie { TypeLijst = l2, AantalMeter = 1m });
            await db.SaveChangesAsync();
            vrij = l1.Id; gebruikt = l2.Id;
        }
        var sut = new ArchiefBeheerService(scope.Factory, new TestAuthService());
        Assert.Equal(2, (await sut.GearchiveerdeLijstenAsync()).Count);

        Assert.Contains("voorraadbeweging", await sut.WaaromLijstNietWegAsync(gebruikt));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.VerwijderLijstDefinitiefAsync(gebruikt));

        await sut.VerwijderLijstDefinitiefAsync(vrij);
        await sut.ZetLijstTerugAsync(gebruikt);

        await using var check = await scope.Factory.CreateDbContextAsync();
        var over = Assert.Single(await check.TypeLijsten.IgnoreQueryFilters().ToListAsync());
        Assert.Equal("GEBRUIKT", over.Artikelnummer);
        Assert.False(over.IsGearchiveerd);
        Assert.Equal(0, await check.VoorraadAlerts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Zonder_recht_mag_het_niet()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = new ArchiefBeheerService(scope.Factory, new TestAuthService { AlleRechten = false });
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.ZetKlantTerugAsync(1));
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.VerwijderLijstDefinitiefAsync(1));
    }
}
