using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels.Verkoop;
using System;
using System.Linq;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-90 — dossier-schil: chips, stappenbalk, hoofdknop, kopregel.</summary>
public class VerkoopDossierViewModelTests
{
    [Fact]
    public void Chips_zijn_gestreept_zolang_het_document_niet_bestaat()
    {
        // Bedragen zoals in de app (nl-BE, komma).
        var vorige = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-BE");
        try { ChipsControleren(); }
        finally { System.Globalization.CultureInfo.CurrentCulture = vorige; }
    }

    private static void ChipsControleren()
    {
        var chips = VerkoopDossierViewModel.BouwChips(419, heeftWerkBon: false, bestelbon: null, voorschot: 0m);
        Assert.Equal(new[] { "Offerte 419", "Werkbon 419", "Bestelbon", "Voorschot" }, chips.Select(c => c.Tekst));
        Assert.Equal(new[] { true, false, false, false }, chips.Select(c => c.Bestaat));

        var bon = new Bestelbon { VolgNr = 24, BestelbonNummer = "2026-24" };
        chips = VerkoopDossierViewModel.BouwChips(423, true, bon, 100m);
        Assert.Equal(new[] { "Offerte 423", "Werkbon 423", "Bestelbon 2026-24", "Voorschot € 100,00" }, chips.Select(c => c.Tekst));
        Assert.All(chips, c => Assert.True(c.Bestaat));

        Assert.Equal("Bestelbon voorlopig", VerkoopDossierViewModel.BouwChips(427, false, new Bestelbon(), 0m)[2].Tekst);
    }

    [Fact]
    public void Stappenbalk_toont_klaar_huidig_en_terug_gekeken()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, 100m);
        var offerteDatum = new DateTime(2026, 10, 19);
        var stappen = VerkoopDossierViewModel.BouwStappen(info, VerkoopStap.InProductie, offerteDatum, new DateTime(2026, 11, 2));

        Assert.Equal("19/10", stappen[0].Onderschrift);
        Assert.Equal("02/11", stappen[1].Onderschrift);
        Assert.True(stappen[2].IsHuidig);
        Assert.Equal("hier ben je", stappen[2].Onderschrift);
        Assert.False(stappen[3].KanBekijken);   // nog te doen

        stappen = VerkoopDossierViewModel.BouwStappen(info, VerkoopStap.Offerte, offerteDatum, null);
        Assert.True(stappen[0].IsBekeken);
        Assert.Equal("terug gekeken", stappen[0].Onderschrift);
        Assert.Equal("huidige stap", stappen[2].Onderschrift);
    }

    [Fact]
    public void Overgeslagen_stap_blijft_overgeslagen_in_het_dossier()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, BestelbonStatus.KlaarVoorExport, 50m,
            VerkoopStapFlags.Werkbon | VerkoopStapFlags.Productie);
        var stappen = VerkoopDossierViewModel.BouwStappen(info, info.Huidig, DateTime.Today, DateTime.Today);
        Assert.True(stappen[1].IsOvergeslagen);
        Assert.True(stappen[2].IsOvergeslagen);
        Assert.Equal("⤼", stappen[1].Bolletje);
    }

    [Theory]
    [InlineData(OfferteStatus.Concept, null, "Offerte openen ▶")]
    [InlineData(OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland, "Start productie ▶")]
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, "Klaar om af te halen ▶")]
    [InlineData(OfferteStatus.Afgewerkt, WerkBonStatus.Afgewerkt, "Klant komt afhalen ▶")]
    [InlineData(OfferteStatus.Geannuleerd, null, null)]
    public void Hoofdknop_per_stap(OfferteStatus status, WerkBonStatus? wb, string? verwacht)
    {
        var info = VerkoopStapResolver.Bepaal(status, wb, null, 10m);
        Assert.Equal(verwacht, VerkoopDossierViewModel.HoofdKnopVoor(info));
    }

    [Fact]
    public void Kopregel_met_adres_telefoon_en_afhaaldatum()
    {
        var k = new Klant { Voornaam = "Sofie", Achternaam = "Claes", Straat = "Leuvensestraat", Nummer = "12", Postcode = "3200", Gemeente = "Aarschot", Telefoon = "0478 12 34 56" };
        Assert.Equal("Leuvensestraat 12, 3200 Aarschot · 0478 12 34 56 · afhalen 10/11/2026",
            VerkoopDossierViewModel.KlantRegelTekst(k, new DateTime(2026, 11, 10)));
        Assert.Equal("nog geen afhaaldatum", VerkoopDossierViewModel.KlantRegelTekst(null, null));
    }
}
