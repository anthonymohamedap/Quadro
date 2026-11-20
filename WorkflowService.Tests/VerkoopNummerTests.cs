using QuadroApp.Model.DB;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Eén nummer per verkoop: bestelbonnummer na het akkoord, daarvoor "voorlopig · offertenummer".</summary>
public class VerkoopNummerTests
{
    [Fact]
    public void Zonder_bestelbonnummer_voorlopig_met_offertenummer()
        => Assert.Equal("voorlopig · 412", VerkoopNummer.Tekst(412, (string?)null));

    [Fact]
    public void Met_bestelbonnummer_enkel_dat_nummer()
        => Assert.Equal("2026-19", VerkoopNummer.Tekst(412, "2026-19"));

    [Fact]
    public void Voorlopige_bon_telt_niet_als_nummer()
        => Assert.Equal("voorlopig · 412", VerkoopNummer.Tekst(412, new Bestelbon { BestelbonNummer = "V412", VolgNr = null }));

    [Fact]
    public void Definitieve_bon_geeft_zijn_nummer()
        => Assert.Equal("2026-19", VerkoopNummer.Tekst(412, new Bestelbon { BestelbonNummer = "2026-19", Jaar = 2026, VolgNr = 19 }));

    [Fact]
    public void Werkbon_zonder_gevuld_nummer_valt_terug_op_voorlopig()
        => Assert.Equal("voorlopig · 412", new WerkBon { Offerte = new Offerte { OfferteNummer = 412 } }.VerkoopNummerTekst);
}
