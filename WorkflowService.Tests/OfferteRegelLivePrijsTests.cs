using QuadroApp.Model.DB;
using System.Collections.Generic;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-77 — de regelprijs in de offertelijst beweegt live mee.</summary>
public class OfferteRegelLivePrijsTests
{
    [Fact]
    public void TotaalInclBtw_wijzigen_meldt_de_weergave()
    {
        var regel = new OfferteRegel();
        var gemeld = new List<string?>();
        regel.PropertyChanged += (_, e) => gemeld.Add(e.PropertyName);

        regel.TotaalInclBtw = 121m;

        Assert.Contains(nameof(OfferteRegel.TotaalInclBtw), gemeld);
    }

    [Fact]
    public void Zelfde_prijs_opnieuw_zetten_meldt_niets()
    {
        var regel = new OfferteRegel { TotaalInclBtw = 50m };
        var aantal = 0;
        regel.PropertyChanged += (_, _) => aantal++;

        regel.TotaalInclBtw = 50m;

        Assert.Equal(0, aantal);
    }

    [Fact]
    public void MeldWeergaveGewijzigd_ververst_prijs_en_omschrijving()
    {
        var regel = new OfferteRegel();
        var gemeld = new List<string?>();
        regel.PropertyChanged += (_, e) => gemeld.Add(e.PropertyName);

        regel.MeldWeergaveGewijzigd();

        Assert.Contains(nameof(OfferteRegel.TotaalInclBtw), gemeld);
        Assert.Contains(nameof(OfferteRegel.AfmetingLabel), gemeld);
    }
}
