using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuadroApp.Service;

/// <summary>
/// Genereert een A4 PDF van de weekwerklijst (gegroepeerd per klant).
/// Gebruikt QuestPDF — zelfde aanpak als PdfFactuurExporter.
/// </summary>
public sealed class PdfWeekLijstExporter
{
    private const string OpeningsUren =
        "Di t/m Vr 10-12 & 13-18u  —  Za 10-17u doorlopend  —  Zo & Ma gesloten";

    /// <summary>
    /// Genereert de PDF en geeft het bestandspad terug.
    /// </summary>
    public string Export(int year, int weekNr, IReadOnlyList<KlantWeekBlock> blocks)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "QuadroApp", "Weeklijsten");
        Directory.CreateDirectory(folder);

        var fileName = $"weeklijst-{year}-week{weekNr:D2}.pdf";
        var path = Path.Combine(folder, fileName);

        var logoBytes = LoadAsset("Assets/Quadro_logo2012_RGB.jpg");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(3);

                    // ── HEADER ───────────────────────────────────────────────
                    DrawHeader(col, year, weekNr, logoBytes);

                    if (!blocks.Any() || blocks.All(b => !b.Items.Any()))
                    {
                        col.Item().PaddingTop(20)
                            .AlignCenter()
                            .Text("Geen taken gepland voor deze week.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    // ── PER KLANT ────────────────────────────────────────────
                    foreach (var block in blocks)
                    {
                        DrawKlantBlock(col, block);
                    }
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
    private static void DrawHeader(ColumnDescriptor col, int year, int weekNr, byte[]? logoBytes)
    {
        col.Item().Row(row =>
        {
            // Links: logo
            if (logoBytes is not null)
                row.ConstantItem(100).Height(48).Image(logoBytes).FitArea();
            else
                row.ConstantItem(100); // lege ruimte als geen logo

            row.RelativeItem().PaddingLeft(12).Column(mid =>
            {
                mid.Item().Text("QUADRO INLIJSTATELIER").Bold().FontSize(13);
                mid.Item().Text("Liersesteenweg 64 — 3200 Aarschot").FontSize(9);
            });

            row.ConstantItem(160).AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text($"Weeklijst — Week {weekNr} / {year}").Bold().FontSize(12);
                right.Item().AlignRight().Text($"Afgedrukt op: {DateTime.Today:dd/MM/yyyy}").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });

        col.Item().PaddingTop(2).Text(OpeningsUren).Italic().FontSize(8).FontColor(Colors.Grey.Medium);

        // Horizontale lijn
        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingBottom(4);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KLANT BLOK
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawKlantBlock(ColumnDescriptor col, KlantWeekBlock block)
    {
        // Klantnaam als sectietitel
        col.Item().PaddingTop(6).Background(Colors.Grey.Lighten4)
            .BorderLeft(3).BorderColor(Colors.Yellow.Darken1)
            .PaddingLeft(8).PaddingVertical(3)
            .Text(block.KlantNaam).Bold().FontSize(10);

        foreach (var item in block.Items)
        {
            DrawWerkItem(col, item);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INDIVIDUEEL WERK-ITEM — compact twee-kolom layout (links: info, rechts: inleg)
    // ══════════════════════════════════════════════════════════════════════════
    private static void DrawWerkItem(ColumnDescriptor col, WeekWerkItem item)
    {
        col.Item()
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .Row(row =>
            {
                // ── Linker kolom: werk-info ──────────────────────────────────
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(1);

                    // Stuks + omschrijving
                    left.Item().Text($"{item.Stuks} stuks in te lijsten:")
                        .Bold().FontSize(10);
                    if (!string.IsNullOrWhiteSpace(item.Omschrijving))
                        left.Item().Text(item.Omschrijving).FontSize(9);

                    // Afmetingen / lijst / afw op één regel
                    var metaparts = new List<string>();
                    if (item.Breedte > 0) metaparts.Add($"breedte : {item.Breedte:0.##} cm");
                    if (item.Hoogte > 0)  metaparts.Add($"hoogte : {item.Hoogte:0.##} cm");
                    if (!string.IsNullOrWhiteSpace(item.Afw))
                        metaparts.Add($"afw. : {item.Afw}");
                    if (!string.IsNullOrWhiteSpace(item.Lijst))
                        metaparts.Add($"lijst : {item.Lijst}");
                    if (metaparts.Any())
                        left.Item().Text(string.Join("    ", metaparts)).FontSize(9);

                    // Productiedatum + bonnr
                    left.Item().Text($"productie op {item.ProductieDatum:dd/MM/yy}").FontSize(9);
                    left.Item().Text($"bonnr. {item.BonNr}").FontSize(9);

                    // Bestelstatus (compact, één regel)
                    var statusLine = GetBestelStatusLine(item);
                    if (!string.IsNullOrWhiteSpace(statusLine))
                        left.Item().PaddingTop(2).Text(statusLine).FontSize(8).Italic();

                    // Notitie (alleen als ingevuld)
                    if (!string.IsNullOrWhiteSpace(item.Notitie))
                        left.Item().PaddingTop(2)
                            .Text($"nota: {item.Notitie}").FontSize(8).Italic();
                });

                // ── Rechter kolom: inleg / artikel / afwerkingen ────────────
                row.ConstantItem(175).PaddingLeft(8).Column(right =>
                {
                    right.Spacing(1);
                    var inleg1 = (!string.IsNullOrWhiteSpace(item.Inleg1) && item.Inleg1 != "×")
                        ? item.Inleg1 : "";
                    right.Item().Text($"inleg 1 : {inleg1}").FontSize(9);
                    right.Item().Text($"inleg 2 : {item.Inleg2}").FontSize(9);
                    right.Item().Text("artikel 1 :").FontSize(9);
                    right.Item().Text("artikel 2 :").FontSize(9);

                    // Afwerkingen
                    if (!string.IsNullOrWhiteSpace(item.GlasBeschrijving))
                        right.Item().Text($"glas : {item.GlasBeschrijving}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.Passe1Beschrijving))
                        right.Item().Text($"pp 1 : {item.Passe1Beschrijving}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.Passe2Beschrijving))
                        right.Item().Text($"pp 2 : {item.Passe2Beschrijving}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.DieptyeBeschrijving))
                        right.Item().Text($"diepte : {item.DieptyeBeschrijving}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.OpklevenBeschrijving))
                        right.Item().Text($"opkleven : {item.OpklevenBeschrijving}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(item.RugBeschrijving))
                        right.Item().Text($"rug : {item.RugBeschrijving}").FontSize(9);
                });
            });
    }

    private static string GetBestelStatusLine(WeekWerkItem item)
    {
        var vormLabel = item.GeselecteerdeBestelVorm switch
        {
            BestelVorm.Verstek    => "in verstek",
            BestelVorm.InLengte   => "in lengte",
            BestelVorm.Gemonteerd => "gemonteerd",
            _                     => null
        };

        if (item.IsOpVoorraad) return "✓ op voorraad";
        if (item.IsBesteld)
        {
            var s = $"✓ besteld op {item.BestelDatum:dd/MM/yyyy}";
            if (!string.IsNullOrWhiteSpace(item.BestellingNummer))
                s += $"  —  {item.BestellingNummer}";
            if (!string.IsNullOrWhiteSpace(item.LeverancierNaam))
                s += $"  —  {item.LeverancierNaam}";
            if (vormLabel is not null)
                s += $"  —  {vormLabel}";
            return s;
        }

        var teBestellenLabel = "⚠ nog te bestellen";
        if (vormLabel is not null)
            teBestellenLabel += $" ({vormLabel})";
        return teBestellenLabel;
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
    // ASSET LOADER (zelfde als PdfFactuurExporter)
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
