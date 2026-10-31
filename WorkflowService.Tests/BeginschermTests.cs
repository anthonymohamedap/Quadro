using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-86 — beginscherm: "Vandaag af te halen" en "Nog te betalen".</summary>
public class BeginschermTests
{
    private static readonly DateTime Vandaag = new(2026, 11, 12);

    private static VerkoopOverzichtRegel Regel(int nr, string klant, OfferteStatus status, WerkBonStatus? wb,
        BestelbonStatus? bon, decimal rest, DateTime? afhaal)
        => new(nr, nr, klant, null, Vandaag.AddDays(-14), 100m, afhaal, VerkoopStapResolver.Bepaal(status, wb, bon, rest));

    [Fact]
    public void Vandaag_af_te_halen_enkel_klaar_of_afgehaald_met_rest_en_afhaaldatum_vandaag()
    {
        var regels = new[]
        {
            Regel(1, "Peeters", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 50m, Vandaag),
            Regel(2, "Janssens", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 0m, Vandaag.AddHours(15)),   // uur telt niet
            Regel(3, "Claes", OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Geexporteerd, 20m, Vandaag),
            Regel(4, "Maes", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 80m, Vandaag.AddDays(1)),        // morgen
            Regel(5, "Wouters", OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, 80m, Vandaag),           // nog niet klaar
            Regel(6, "Aerts", OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald, 0m, Vandaag),     // afgerond
            Regel(7, "Smets", OfferteStatus.Geannuleerd, WerkBonStatus.Afgewerkt, null, 10m, Vandaag),                // geannuleerd
            Regel(8, "Leys", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 10m, null),                      // geen datum
        };

        var blokken = BeginschermBlokken.Bepaal(regels, Vandaag);

        // Gesorteerd op klant.
        Assert.Equal(new[] { 3, 2, 1 }, blokken.VandaagAfTeHalen.Select(r => r.OfferteNummer));
    }

    [Fact]
    public void Nog_te_betalen_enkel_afgehaald_met_rest_oudste_eerst()
    {
        var regels = new[]
        {
            Regel(1, "Claes", OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Geexporteerd, 20m, Vandaag),
            Regel(2, "Wouters", OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.KlaarVoorExport, 80m, Vandaag.AddDays(-6)),
            Regel(3, "Aerts", OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald, 0m, Vandaag.AddDays(-3)),
            Regel(4, "Peeters", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 50m, Vandaag),            // nog niet afgehaald
            Regel(5, "Leys", OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Geexporteerd, 0m, null) // rest 0
        };

        var blokken = BeginschermBlokken.Bepaal(regels, Vandaag);

        Assert.Equal(new[] { 2, 1 }, blokken.NogTeBetalen.Select(r => r.OfferteNummer));
    }

    [Fact]
    public void Leeg_als_er_niets_is()
    {
        var blokken = BeginschermBlokken.Bepaal(Array.Empty<VerkoopOverzichtRegel>(), Vandaag);
        Assert.Empty(blokken.VandaagAfTeHalen);
        Assert.Empty(blokken.NogTeBetalen);
    }

    [Fact]
    public void Teksten_voor_het_beginscherm()
    {
        var oude = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-BE");
        try
        {
            var klaar = Regel(421, "Peeters", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 136.5m, Vandaag);
            Assert.Equal("Offerte 421", klaar.BonTekst);
            Assert.Equal("nog € 136,50", klaar.NogTeBetalenTekst);
            Assert.Equal("Klaar", klaar.StapKortTekst);
            Assert.Equal("€ 136,50", klaar.BedragTekst);

            var afgehaald = Regel(423, "Claes", OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Geexporteerd, 145m, Vandaag)
                with { BestelbonNummer = "2026-23" };
            Assert.Equal("Offerte 423 · Bestelbon 2026-23", afgehaald.BonTekst);
            Assert.Equal("Al afgehaald", afgehaald.StapKortTekst);
            Assert.Equal("afgehaald 12/11", afgehaald.AfhaalDatumTekst);

            var betaald = Regel(422, "Janssens", OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, null, 0m, Vandaag);
            Assert.Equal("betaald", betaald.NogTeBetalenTekst);
        }
        finally { CultureInfo.CurrentCulture = oude; }
    }

    [Fact]
    public async Task Overzicht_toont_bestelbonnummer_maar_niet_voor_een_voorlopige_bon()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "Test", Achternaam = "Quadro" };
            var definitief = new Offerte
            {
                OfferteNummer = 423, Datum = Vandaag, Klant = klant, Status = OfferteStatus.Besteld, TotaalInclBtw = 245m,
                WerkBon = new WerkBon { Status = WerkBonStatus.Afgehaald }
            };
            var voorlopig = new Offerte { OfferteNummer = 424, Datum = Vandaag, Klant = klant, TotaalInclBtw = 50m };
            db.Offertes.AddRange(definitief, voorlopig);
            await db.SaveChangesAsync();

            db.Bestelbonnen.AddRange(
                new Bestelbon { OfferteId = definitief.Id, WerkBonId = definitief.WerkBon!.Id, Jaar = 2026, VolgNr = 23,
                    BestelbonNummer = "2026-23", KlantNaam = "Test Quadro", TotaalInclBtw = 245m, Status = BestelbonStatus.Geexporteerd },
                new Bestelbon { OfferteId = voorlopig.Id, Jaar = 2026, VolgNr = null, BestelbonNummer = "V424",
                    KlantNaam = "Test Quadro", TotaalInclBtw = 50m, Status = BestelbonStatus.Draft });
            await db.SaveChangesAsync();
        }

        var regels = await new VerkoopOverzichtService(scope.Factory).GetOverzichtAsync();

        Assert.Equal("2026-23", Assert.Single(regels, r => r.OfferteNummer == 423).BestelbonNummer);
        Assert.Null(Assert.Single(regels, r => r.OfferteNummer == 424).BestelbonNummer);
    }

    // ── US-105 — nieuwe indeling van het beginscherm ─────────────────────────────

    [Theory]
    [InlineData(7, "Goedemorgen")]
    [InlineData(11, "Goedemorgen")]
    [InlineData(12, "Goedemiddag")]
    [InlineData(17, "Goedemiddag")]
    [InlineData(18, "Goedenavond")]
    public void Begroeting_volgt_het_uur(int uur, string verwacht)
        => Assert.Equal(verwacht, Beginscherm.Begroeting(new DateTime(2026, 11, 12, uur, 30, 0)));

    [Fact]
    public void Datum_en_bedrag_in_het_Nederlands()
    {
        Assert.Equal("Donderdag 12 november 2026", Beginscherm.DatumTekst(Vandaag));
        Assert.Equal("€ 1.417,50", Beginscherm.Bedrag(1417.5m));
    }

    [Theory]
    [InlineData(0.5, "net")]
    [InlineData(5, "5 min")]
    [InlineData(180, "3 u")]
    [InlineData(2 * 24 * 60 + 5, "2 d")]
    public void Waarschuwing_toont_hoe_lang_geleden(double minuten, string verwacht)
    {
        var nu = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(verwacht, Beginscherm.Geleden(nu.AddMinutes(-minuten), nu));
    }

    [Fact]
    public void Waarschuwing_toont_het_label_niet_de_enumnaam()
    {
        var regel = WaarschuwingRegel.Van(new VoorraadAlert
        {
            AlertType = VoorraadAlertType.OrderOverdue, Bericht = "Bestelling B-1 is over tijd.",
            AangemaaktOp = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc)
        }, new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Leverancier te laat", regel.Soort);
        Assert.Equal("1 u", regel.Wanneer);
    }
}
