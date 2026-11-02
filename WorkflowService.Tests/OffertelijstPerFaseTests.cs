using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-84 — offertelijst opgesplitst per fase.</summary>
public class OffertelijstPerFaseTests
{
    [Theory]
    [InlineData(OfferteStatus.Concept, null, null, VerkoopFase.OpenOffertes)]
    [InlineData(OfferteStatus.Verzonden, null, null, VerkoopFase.OpenOffertes)]
    [InlineData(OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland, null, VerkoopFase.LopendeWerkbonnen)]
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, VerkoopFase.LopendeWerkbonnen)]
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgewerkt, BestelbonStatus.Draft, VerkoopFase.KlaarOmAfTeHalen)]
    [InlineData(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald, VerkoopFase.Afgerond)]
    [InlineData(OfferteStatus.Geannuleerd, null, null, VerkoopFase.Geannuleerd)]
    public void Fase_volgt_de_stap(OfferteStatus offerte, WerkBonStatus? werkbon, BestelbonStatus? bon, VerkoopFase verwacht)
        => Assert.Equal(verwacht, VerkoopOverzichtService.FaseVan(VerkoopStapResolver.Bepaal(offerte, werkbon, bon, 0m)));

    [Fact]
    public void Afgehaald_maar_niet_betaald_staat_bij_klaar_om_af_te_halen()
        => Assert.Equal(VerkoopFase.KlaarOmAfTeHalen,
            VerkoopOverzichtService.FaseVan(VerkoopStapResolver.Bepaal(OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Geexporteerd, 26.2m)));

    [Fact]
    public async Task Overzicht_leest_stap_afhaaldatum_en_rest_uit_de_database()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "Test", Achternaam = "Quadro", Email = "test@quadro.test" };

            db.Offertes.Add(new Offerte { OfferteNummer = 419, Datum = new DateTime(2026, 10, 26), Klant = klant, TotaalInclBtw = 89m });

            var afgehaald = new Offerte
            {
                OfferteNummer = 398, Datum = new DateTime(2026, 9, 29), Klant = klant, Status = OfferteStatus.Besteld,
                TotaalInclBtw = 76.20m, VoorschotBedrag = 50m, AfhaalDatum = new DateTime(2026, 10, 14),
                WerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald }
            };
            db.Offertes.Add(afgehaald);
            await db.SaveChangesAsync();

            var bon = new Bestelbon
            {
                OfferteId = afgehaald.Id, WerkBonId = afgehaald.WerkBon!.Id, Jaar = 2026, VolgNr = 7, BestelbonNummer = "2026-7",
                KlantNaam = "Test Quadro", TotaalInclBtw = 76.20m, VoorschotBedrag = 50m, Status = BestelbonStatus.Geexporteerd
            };
            db.Bestelbonnen.Add(bon);
            await db.SaveChangesAsync();
            db.Ontvangsten.Add(new Ontvangst { Soort = OntvangstSoort.Betaling, BestelbonId = bon.Id, OfferteId = afgehaald.Id, BedragIncl = 10m });
            await db.SaveChangesAsync();
        }

        var regels = await new VerkoopOverzichtService(scope.Factory).GetOverzichtAsync();

        var open = Assert.Single(regels, r => r.OfferteNummer == 419);
        Assert.Equal(VerkoopFase.OpenOffertes, open.Fase);
        Assert.Equal("Offerte", open.FaseTekst);
        Assert.Equal("Test Quadro", open.KlantNaam);
        Assert.False(open.ToontAfhaalDatum);

        var rest = Assert.Single(regels, r => r.OfferteNummer == 398);
        Assert.Equal(VerkoopFase.KlaarOmAfTeHalen, rest.Fase);
        Assert.Equal(16.20m, rest.Stap.NogTeBetalen);   // 76,20 − 50 voorschot − 10 betaald
        Assert.StartsWith("Afgehaald — nog € 16", rest.FaseTekst);
        Assert.True(rest.ToontAfhaalDatum);
        Assert.Equal(new DateTime(2026, 10, 14), rest.AfhaalDatum);
    }

    private static VerkoopOverzichtRegel Regel(int nr, string klant, OfferteStatus status, WerkBonStatus? wb = null)
        => new(nr, nr, klant, null, DateTime.Today, 10m, null, VerkoopStapResolver.Bepaal(status, wb, null, 10m));

    [Fact]
    public void Zoeken_binnen_tabblad_of_in_alle()
    {
        var regels = new[]
        {
            Regel(419, "Test Klant-C", OfferteStatus.Concept),
            Regel(412, "Test Quadro", OfferteStatus.InProductie, WerkBonStatus.InUitvoering),
            Regel(418, "Test Quadro", OfferteStatus.Verzonden)
        };

        Assert.Equal(new[] { 419, 418 },
            OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, null, zoekInAlle: false).Select(r => r.OfferteNummer));
        Assert.Equal(new[] { 418 },
            OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, "quadro", zoekInAlle: false).Select(r => r.OfferteNummer));
        Assert.Equal(new[] { 412, 418 },
            OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, "quadro", zoekInAlle: true).Select(r => r.OfferteNummer));
        Assert.Equal(new[] { 412 },
            OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, "412", zoekInAlle: true).Select(r => r.OfferteNummer));
        // Zonder zoekterm toont "zoek in alle" gewoon het gekozen tabblad.
        Assert.Equal(2, OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, "", zoekInAlle: true).Count());
    }

    [Fact]
    public void Zoeken_op_bestelbonnummer_US89()
    {
        var regels = new[]
        {
            Regel(421, "Marleen Peeters", OfferteStatus.Besteld, WerkBonStatus.Afgewerkt) with { BestelbonNummer = "2026-20" },
            Regel(422, "Luc Janssens", OfferteStatus.Besteld, WerkBonStatus.Afgewerkt) with { BestelbonNummer = "2026-21" }
        };

        Assert.Equal(new[] { 422 },
            OffertesLijstViewModel.Filter(regels, VerkoopFase.OpenOffertes, "2026-21", zoekInAlle: true).Select(r => r.OfferteNummer));
    }

    [Fact]
    public void Werkbon_en_bestelbon_in_de_lijst_US89()
    {
        var wb = Regel(426, "Tom Jacobs", OfferteStatus.InProductie, WerkBonStatus.InUitvoering)
            with { WerkBonStatus = WerkBonStatus.InUitvoering, AantalTaken = 2, AantalBesteldOfKlaar = 1 };
        Assert.Equal("Werkbon 426 · In uitvoering · 2 taken · 1/2 besteld", wb.WerkbonInfo);
        Assert.Equal("—", wb.BestelbonKolomTekst);

        var offerte = Regel(427, "An Mertens", OfferteStatus.Concept) with { HeeftVoorlopigeBon = true };
        Assert.False(offerte.ToontWerkbonInfo);
        Assert.Equal("voorlopig", offerte.BestelbonKolomTekst);

        Assert.Equal("2026-24", (offerte with { BestelbonNummer = "2026-24" }).BestelbonKolomTekst);
    }

    [Fact]
    public async Task Overzicht_telt_werktaken_per_werkbon_US89()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "Tom", Achternaam = "Jacobs" };
            var offerte = new Offerte { OfferteNummer = 426, Klant = klant, Status = OfferteStatus.InProductie, Datum = DateTime.Today };
            var werkBon = new WerkBon { Offerte = offerte, Status = WerkBonStatus.InUitvoering };
            werkBon.Taken.Add(new WerkTaak { Omschrijving = "a", GeplandVan = DateTime.Today, IsBesteld = true });
            werkBon.Taken.Add(new WerkTaak { Omschrijving = "b", GeplandVan = DateTime.Today });
            db.WerkBonnen.Add(werkBon);
            await db.SaveChangesAsync();
        }

        var regel = Assert.Single(await new VerkoopOverzichtService(scope.Factory).GetOverzichtAsync());
        Assert.Equal("Werkbon 426 · In uitvoering · 2 taken · 1/2 besteld", regel.WerkbonInfo);
    }
}
