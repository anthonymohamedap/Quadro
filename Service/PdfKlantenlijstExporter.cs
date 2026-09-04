using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuadroApp.Service;

/// <summary>
/// Genereert een A4 PDF van de (eventueel gefilterde) klantenlijst — bv. om af te drukken na
/// een zoekopdracht op woonplaats. Zelfde QuestPDF-aanpak als PdfWeekLijstExporter/PdfFactuurExporter.
/// </summary>
public sealed class PdfKlantenlijstExporter
{
    /// <summary>
    /// Genereert de PDF voor de meegegeven klanten (typisch: de op dat moment zichtbare/gefilterde
    /// lijst) en geeft het bestandspad terug.
    /// </summary>
    public string Export(IReadOnlyList<Klant> klanten, string? filterOmschrijving = null)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "QuadroApp", "Klantenlijsten");
        Directory.CreateDirectory(folder);

        var fileName = $"klantenlijst-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
        var path = Path.Combine(folder, fileName);

        var logoBytes = LoadAsset("Assets/Quadro_logo2012_RGB.jpg");
        var gesorteerd = klanten
            .OrderBy(k => k.Achternaam)
            .ThenBy(k => k.Voornaam)
            .ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(3);

                    DrawHeader(col, logoBytes, gesorteerd.Count, filterOmschrijving);

                    if (gesorteerd.Count == 0)
                    {
                        col.Item().PaddingTop(20)
                            .AlignCenter()
                            .Text("Geen klanten gevonden.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    DrawKolomKoppen(col);
                    foreach (var klant in gesorteerd)
                        DrawKlantRij(col, klant);
                });

                page.Footer().Element(DrawFooter);
            });
        });

        doc.GeneratePdf(path);
        return path;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HEADER
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawHeader(ColumnDescriptor col, byte[]? logoBytes, int aantal, string? filterOmschrijving)
    {
        col.Item().Row(row =>
        {
            if (logoBytes is not null)
                row.ConstantItem(100).Height(48).Image(logoBytes).FitArea();
            else
                row.ConstantItem(100);

            row.RelativeItem().PaddingLeft(12).Column(mid =>
            {
                mid.Item().Text("QUADRO INLIJSTATELIER").Bold().FontSize(13);
                mid.Item().Text("Liersesteenweg 64 — 3200 Aarschot").FontSize(9);
            });

            row.ConstantItem(160).AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text("Klantenlijst").Bold().FontSize(12);
                right.Item().AlignRight().Text($"Afgedrukt op: {DateTime.Today:dd/MM/yyyy}").FontSize(9).FontColor(Colors.Grey.Medium);
                right.Item().AlignRight().Text($"{aantal} klant(en)").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });

        if (!string.IsNullOrWhiteSpace(filterOmschrijving))
            col.Item().PaddingTop(2).Text($"Filter: {filterOmschrijving}").Italic().FontSize(9).FontColor(Colors.Grey.Darken1);

        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingBottom(4);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KOLOMKOPPEN
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawKolomKoppen(ColumnDescriptor col)
    {
        col.Item().PaddingTop(2).Row(row =>
        {
            row.RelativeItem(2).Text("Naam").Bold().FontSize(9);
            row.RelativeItem(3).Text("Adres").Bold().FontSize(9);
            row.RelativeItem(2).Text("Telefoon").Bold().FontSize(9);
            row.RelativeItem(3).Text("E-mail").Bold().FontSize(9);
        });
        col.Item().PaddingBottom(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KLANT-RIJ
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawKlantRij(ColumnDescriptor col, Klant klant)
    {
        var adres = string.Join(" ", new[] { klant.Straat, klant.Nummer }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var plaats = string.Join(" ", new[] { klant.Postcode, klant.Gemeente }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var volledigAdres = string.Join(", ", new[] { adres, plaats }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        col.Item()
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(3)
            .Row(row =>
            {
                row.RelativeItem(2).Text($"{klant.Voornaam} {klant.Achternaam}".Trim()).FontSize(9);
                row.RelativeItem(3).Text(volledigAdres).FontSize(9);
                row.RelativeItem(2).Text(klant.Telefoon ?? "").FontSize(9);
                row.RelativeItem(3).Text(klant.Email ?? "").FontSize(9);
            });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FOOTER
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().AlignLeft()
                    .Text("Liersesteenweg 64 - 3200 Aarschot - T 016 57 08 72 - kaders@quadro.be")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
                row.ConstantItem(60).AlignRight()
                    .Text(text =>
                    {
                        text.Span("Pagina ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ASSET LOADER (zelfde als PdfWeekLijstExporter/PdfFactuurExporter)
    // ══════════════════════════════════════════════════════════════════════════
    private static byte[]? LoadAsset(string relativePath)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(basePath)) return File.ReadAllBytes(basePath);

        var cwdPath = Path.GetFullPath(relativePath);
        if (File.Exists(cwdPath)) return File.ReadAllBytes(cwdPath);

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 5; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir is null) break;
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return File.ReadAllBytes(candidate);
        }

        return null;
    }
}
