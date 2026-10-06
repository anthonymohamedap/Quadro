using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-73 — de bestelbon-PDF houdt rekening met de geregistreerde betalingen.</summary>
public class BestelbonBetalingenPdfTests
{
    private static Factuur Bon(decimal totaal, decimal voorschot, FactuurStatus status = FactuurStatus.KlaarVoorExport, params decimal[] betalingen)
    {
        var f = new Factuur
        {
            Id = 1, FactuurNummer = "2026-14", KlantNaam = "Test", Status = status,
            TotaalInclBtw = totaal, VoorschotBedrag = voorschot
        };
        foreach (var b in betalingen)
            f.GeregistreerdeBetalingen.Add(new Ontvangst
            {
                Soort = OntvangstSoort.Betaling, Datum = new DateTime(2026, 10, 6),
                Betaalwijze = Betaalwijze.Storting, BedragIncl = b
            });
        return f;
    }

    [Fact]
    public void Zonder_betalingen_is_te_betalen_totaal_min_voorschot()
    {
        var (rest, betaald) = PdfFactuurExporter.BerekenTeBetalen(Bon(259.05m, 222m));
        Assert.Equal(37.05m, rest);
        Assert.False(betaald);
    }

    [Fact]
    public void Volledig_betaald_toont_betaald()
    {
        var (rest, betaald) = PdfFactuurExporter.BerekenTeBetalen(Bon(259.05m, 222m, FactuurStatus.KlaarVoorExport, 37.05m));
        Assert.Equal(0m, rest);
        Assert.True(betaald);
    }

    [Fact]
    public void Deelbetaling_verlaagt_de_rest()
    {
        var (rest, betaald) = PdfFactuurExporter.BerekenTeBetalen(Bon(300m, 100m, FactuurStatus.KlaarVoorExport, 50m));
        Assert.Equal(150m, rest);
        Assert.False(betaald);
    }

    [Fact]
    public void Oude_bon_op_status_betaald_zonder_betalingsregels_toont_betaald()
    {
        var (rest, betaald) = PdfFactuurExporter.BerekenTeBetalen(Bon(120m, 0m, FactuurStatus.Betaald));
        Assert.Equal(0m, rest);
        Assert.True(betaald);
    }

    [Fact]
    public void Bon_van_0_euro_zonder_betalingen_is_niet_betaald()
    {
        var (rest, betaald) = PdfFactuurExporter.BerekenTeBetalen(Bon(0m, 0m));
        Assert.Equal(0m, rest);
        Assert.False(betaald);
    }

    [Fact]
    public async Task Pdf_met_betalingen_wordt_aangemaakt()
    {
        var map = Path.Combine(Path.GetTempPath(), "quadro-us73-" + Guid.NewGuid().ToString("N"));
        try
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            var result = await new PdfFactuurExporter().ExportAsync(Bon(259.05m, 222m, FactuurStatus.KlaarVoorExport, 20m, 17.05m), map);
            Assert.True(result.Success, result.Message);
            Assert.True(File.Exists(result.BestandPad));
        }
        finally
        {
            if (Directory.Exists(map)) Directory.Delete(map, recursive: true);
        }
    }
}
