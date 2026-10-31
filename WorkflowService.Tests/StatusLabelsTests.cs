using QuadroApp.Converters;
using QuadroApp.Model.DB;
using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-85 — gewone woorden in plaats van technische statusnamen.</summary>
public class StatusLabelsTests
{
    [Theory]
    [InlineData(BestelbonStatus.Draft, "Open")]
    [InlineData(BestelbonStatus.KlaarVoorExport, "Open")]
    [InlineData(BestelbonStatus.Geexporteerd, "Open")]
    [InlineData(BestelbonStatus.Betaald, "Betaald")]
    [InlineData(BestelbonStatus.Geannuleerd, "Geannuleerd")]
    public void Bestelbon(BestelbonStatus status, string verwacht) => Assert.Equal(verwacht, status.Label());

    [Theory]
    [InlineData(WerkBonStatus.Gepland, "Gepland")]
    [InlineData(WerkBonStatus.InUitvoering, "In uitvoering")]
    [InlineData(WerkBonStatus.Afgewerkt, "Klaar om af te halen")]
    [InlineData(WerkBonStatus.Afgehaald, "Afgehaald")]
    public void Werkbon(WerkBonStatus status, string verwacht) => Assert.Equal(verwacht, status.Label());

    [Theory]
    [InlineData(OfferteStatus.Concept, "Offerte (concept)")]
    [InlineData(OfferteStatus.Goedgekeurd, "Werkbon")]
    [InlineData(OfferteStatus.InProductie, "In productie")]
    [InlineData(OfferteStatus.Besteld, "Klaar om af te halen")]
    public void Offerte_volgt_de_fasen(OfferteStatus status, string verwacht) => Assert.Equal(verwacht, status.Label());

    [Fact]
    public void Geen_enkele_status_toont_nog_een_technische_naam()
    {
        var labels = Enum.GetValues<OfferteStatus>().Select(s => s.Label())
            .Concat(Enum.GetValues<WerkBonStatus>().Select(s => s.Label()))
            .Concat(Enum.GetValues<BestelbonStatus>().Select(s => s.Label()))
            .Concat(Enum.GetValues<VoorraadAlertType>().Select(s => s.Label()))
            .Concat(Enum.GetValues<LeverancierBestellingStatus>().Select(s => s.Label()));

        // Technische namen zijn aan elkaar geschreven met een hoofdletter in het midden (InProductie, BelowMinimum).
        Assert.All(labels, l => Assert.DoesNotMatch("[a-z][A-Z]", l));
        Assert.Equal("Onder de minimumvoorraad", VoorraadAlertType.BelowMinimum.Label());
    }

    [Fact]
    public void Converter_gebruikt_dezelfde_woorden_en_optioneel_een_formaat()
    {
        var c = StatusLabelConverter.Instance;
        Assert.Equal("Open", c.Convert(BestelbonStatus.KlaarVoorExport, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("Status: Betaald", c.Convert(BestelbonStatus.Betaald, typeof(string), "Status: {0}", CultureInfo.InvariantCulture));
        Assert.Equal("Klaar om af te halen", WerkBonStatusToLabelConverter.Instance.Convert(WerkBonStatus.Afgewerkt, typeof(string), null, CultureInfo.InvariantCulture));
    }
}
