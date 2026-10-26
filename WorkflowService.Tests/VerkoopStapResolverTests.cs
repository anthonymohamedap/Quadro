using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Verkoop;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-83 — de stap van een verkoop volgt uit de bestaande statussen.</summary>
public class VerkoopStapResolverTests
{
    [Theory]
    [InlineData(OfferteStatus.Concept, null, null, VerkoopStap.Offerte)]
    [InlineData(OfferteStatus.Verzonden, null, null, VerkoopStap.Offerte)]
    [InlineData(OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland, null, VerkoopStap.Bestelling)]
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.Gepland, null, VerkoopStap.Bestelling)] // P10: inplannen zette de offerte al op InProductie
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, VerkoopStap.InProductie)]
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgewerkt, FactuurStatus.Draft, VerkoopStap.KlaarOmAfTeHalen)]
    [InlineData(OfferteStatus.Betaald, WerkBonStatus.Afgewerkt, FactuurStatus.Betaald, VerkoopStap.KlaarOmAfTeHalen)] // al betaald, nog niet afgehaald
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgehaald, FactuurStatus.Geexporteerd, VerkoopStap.AfgehaaldEnBetaald)]
    [InlineData(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, FactuurStatus.Betaald, VerkoopStap.AfgehaaldEnBetaald)]
    public void Bepaalt_de_huidige_stap(OfferteStatus offerte, WerkBonStatus? werkbon, FactuurStatus? bon, VerkoopStap verwacht)
        => Assert.Equal(verwacht, VerkoopStapResolver.Bepaal(offerte, werkbon, bon, 0m).Huidig);

    [Fact]
    public void Afgehaald_en_betaald_is_afgerond_en_alle_stappen_zijn_klaar()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, FactuurStatus.Betaald, 0m);

        Assert.True(info.IsAfgerond);
        Assert.False(info.IsAfgehaaldNogTeBetalen);
        Assert.All(VerkoopStapResolver.AlleStappen, s => Assert.Equal(StapToestand.Klaar, info.ToestandVan(s)));
    }

    [Fact]
    public void Afgehaald_met_rest_blijft_op_stap_5_met_bedrag()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Besteld, WerkBonStatus.Afgehaald, FactuurStatus.Geexporteerd, 26.20m);

        Assert.Equal(VerkoopStap.AfgehaaldEnBetaald, info.Huidig);
        Assert.False(info.IsAfgerond);
        Assert.True(info.IsAfgehaaldNogTeBetalen);
        Assert.Equal(26.20m, info.NogTeBetalen);
        Assert.Equal(StapToestand.Huidig, info.ToestandVan(VerkoopStap.AfgehaaldEnBetaald));
    }

    [Fact]
    public void Vorige_stappen_klaar_volgende_nog_te_doen()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null, 104.86m);

        Assert.Equal(StapToestand.Klaar, info.ToestandVan(VerkoopStap.Offerte));
        Assert.Equal(StapToestand.Klaar, info.ToestandVan(VerkoopStap.Bestelling));
        Assert.Equal(StapToestand.Huidig, info.ToestandVan(VerkoopStap.InProductie));
        Assert.Equal(StapToestand.NogTeDoen, info.ToestandVan(VerkoopStap.KlaarOmAfTeHalen));
    }

    [Fact]
    public void Geannuleerd_wordt_gemarkeerd()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Geannuleerd, WerkBonStatus.Gepland, null, 0m);
        Assert.True(info.IsGeannuleerd);
    }

    [Fact]
    public void Zonder_werkbon_volgt_de_offertestatus()
    {
        Assert.Equal(VerkoopStap.KlaarOmAfTeHalen, VerkoopStapResolver.Bepaal(OfferteStatus.Besteld, null, FactuurStatus.Draft, 0m).Huidig);
        Assert.True(VerkoopStapResolver.Bepaal(OfferteStatus.Betaald, null, null, 0m).IsAfgerond);
    }

    [Theory]
    [InlineData(WerkBonStatus.Gepland, null, OfferteStatus.Goedgekeurd)]
    [InlineData(WerkBonStatus.InUitvoering, null, OfferteStatus.InProductie)]
    [InlineData(WerkBonStatus.Afgewerkt, FactuurStatus.Draft, OfferteStatus.Besteld)]
    [InlineData(WerkBonStatus.Afgehaald, FactuurStatus.Betaald, OfferteStatus.Betaald)]
    public void Stap_en_reconciliatie_US48_zijn_het_eens(WerkBonStatus werkbon, FactuurStatus? bon, OfferteStatus doel)
    {
        // De offertestatus die US-48 afleidt, levert met dezelfde werkbon/bon dezelfde stap op.
        var factuur = bon is null ? null : new Factuur { Status = bon.Value };
        Assert.Equal(doel, OfferteStatusReconciliatieService.BepaalDoelStatus(new WerkBon { Status = werkbon }, factuur));

        var metWerkbon = VerkoopStapResolver.Bepaal(doel, werkbon, bon, 0m);
        var zonderWerkbon = VerkoopStapResolver.Bepaal(doel, null, bon, 0m);
        Assert.Equal(metWerkbon.Huidig, zonderWerkbon.Huidig);
    }
}
