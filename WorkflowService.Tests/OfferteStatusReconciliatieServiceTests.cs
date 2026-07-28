using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Security;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public class OfferteStatusReconciliatieServiceTests
{
    // ── Mapping (unit) ──────────────────────────────────────────────────────

    [Fact]
    public void Mapping_geen_werkbon_geen_factuur_is_null()
        => Assert.Null(OfferteStatusReconciliatieService.BepaalDoelStatus(null, null));

    [Fact]
    public void Mapping_werkbon_gepland_is_Goedgekeurd()
        => Assert.Equal(OfferteStatus.Goedgekeurd,
            OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = WerkBonStatus.Gepland }, null));

    [Fact]
    public void Mapping_werkbon_inuitvoering_is_InProductie()
        => Assert.Equal(OfferteStatus.InProductie,
            OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = WerkBonStatus.InUitvoering }, null));

    [Fact]
    public void Mapping_werkbon_afgewerkt_is_Gefactureerd()
        => Assert.Equal(OfferteStatus.Gefactureerd,
            OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = WerkBonStatus.Afgewerkt }, null));

    [Fact]
    public void Mapping_bestelbon_bestaat_is_Gefactureerd()
        => Assert.Equal(OfferteStatus.Gefactureerd,
            OfferteStatusReconciliatieService.BepaalDoelStatus(null, new Factuur { Status = FactuurStatus.KlaarVoorExport }));

    [Fact]
    public void Mapping_bestelbon_betaald_is_Betaald()
        => Assert.Equal(OfferteStatus.Betaald,
            OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = WerkBonStatus.Afgewerkt },
                new Factuur { Status = FactuurStatus.Betaald }));

    [Fact]
    public void Mapping_geannuleerde_bestelbon_telt_niet()
        => Assert.Equal(OfferteStatus.Goedgekeurd,
            OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = WerkBonStatus.Gepland },
                new Factuur { Status = FactuurStatus.Geannuleerd }));

    // ── Reconciliatie (integratie) ──────────────────────────────────────────

    [Fact]
    public async Task Reconcilieer_corrigeert_en_is_idempotent()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var (o1, o2, o3, o4, o5) = await SeedAsync(scope.Factory);
        var sut = new OfferteStatusReconciliatieService(scope.Factory, new TestAuthService());

        var aantal = await sut.ReconcilieerAlleAsync();
        Assert.Equal(3, aantal); // o1, o3, o4 wijzigen

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            Assert.Equal(OfferteStatus.Betaald,      (await db.Offertes.FindAsync(o1))!.Status);
            Assert.Equal(OfferteStatus.Geannuleerd,  (await db.Offertes.FindAsync(o2))!.Status); // ongemoeid
            Assert.Equal(OfferteStatus.InProductie,  (await db.Offertes.FindAsync(o3))!.Status);
            Assert.Equal(OfferteStatus.Goedgekeurd,  (await db.Offertes.FindAsync(o4))!.Status);
            Assert.Equal(OfferteStatus.Betaald,      (await db.Offertes.FindAsync(o5))!.Status); // nooit terug
        }

        // Idempotent: tweede keer verandert niets.
        Assert.Equal(0, await sut.ReconcilieerAlleAsync());
    }

    [Fact]
    public async Task Reconcilieer_vereist_GebruikersBeheren()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new OfferteStatusReconciliatieService(scope.Factory, new TestAuthService { AlleRechten = false });

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.ReconcilieerAlleAsync());
    }

    private static async Task<(int, int, int, int, int)> SeedAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        // o1: Concept + werkbon Afgewerkt + factuur Betaald  -> Betaald
        var o1 = new Offerte { Datum = System.DateTime.Today, Status = OfferteStatus.Concept, TotaalInclBtw = 100m };
        // o2: Geannuleerd + werkbon Afgewerkt                 -> ongemoeid
        var o2 = new Offerte { Datum = System.DateTime.Today, Status = OfferteStatus.Geannuleerd, TotaalInclBtw = 100m };
        // o3: Goedgekeurd + werkbon InUitvoering              -> InProductie
        var o3 = new Offerte { Datum = System.DateTime.Today, Status = OfferteStatus.Goedgekeurd, TotaalInclBtw = 100m };
        // o4: Concept + werkbon Gepland                       -> Goedgekeurd
        var o4 = new Offerte { Datum = System.DateTime.Today, Status = OfferteStatus.Concept, TotaalInclBtw = 100m };
        // o5: Betaald + werkbon Gepland                       -> nooit terug (blijft Betaald)
        var o5 = new Offerte { Datum = System.DateTime.Today, Status = OfferteStatus.Betaald, TotaalInclBtw = 100m };

        db.Offertes.AddRange(o1, o2, o3, o4, o5);
        await db.SaveChangesAsync();

        db.WerkBonnen.AddRange(
            new WerkBon { OfferteId = o1.Id, Status = WerkBonStatus.Afgewerkt, TotaalPrijsIncl = 100m },
            new WerkBon { OfferteId = o2.Id, Status = WerkBonStatus.Afgewerkt, TotaalPrijsIncl = 100m },
            new WerkBon { OfferteId = o3.Id, Status = WerkBonStatus.InUitvoering, TotaalPrijsIncl = 100m },
            new WerkBon { OfferteId = o4.Id, Status = WerkBonStatus.Gepland, TotaalPrijsIncl = 100m },
            new WerkBon { OfferteId = o5.Id, Status = WerkBonStatus.Gepland, TotaalPrijsIncl = 100m });

        db.Facturen.Add(new Factuur
        {
            OfferteId = o1.Id, Jaar = 2026, VolgNr = 1, FactuurNummer = "B-1",
            KlantNaam = "X", FactuurDatum = System.DateTime.Today, VervalDatum = System.DateTime.Today,
            Status = FactuurStatus.Betaald
        });

        await db.SaveChangesAsync();
        return (o1.Id, o2.Id, o3.Id, o4.Id, o5.Id);
    }
}
