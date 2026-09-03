using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuadroApp.Service;

/// <summary>
/// US-57 — genereert een klantvriendelijke offerte-PDF (i.t.t. de bestelbon-/factuur-preview,
/// die productie-/facturatiegegevens toont). Zelfde QuestPDF-aanpak als
/// PdfFactuurExporter/PdfKlantenlijstExporter, incl. de gedeelde LoadAsset-helper voor het logo.
/// </summary>
public sealed class PdfOfferteExporter
{
    /// <summary>
    /// Genereert de PDF voor de meegegeven offerte (verwacht <see cref="Offerte.Klant"/> en de
    /// afwerkings-navigaties op <see cref="Offerte.Regels"/> mee geladen) en geeft het
    /// bestandspad terug.
    /// </summary>
    public string Export(Offerte offerte)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "QuadroApp", "Offertes");
        Directory.CreateDirectory(folder);

        var nummer = offerte.OfferteNummer > 0 ? offerte.OfferteNummer.ToString() : "concept";
        var fileName = $"offerte-{nummer}.pdf";
        var path = Path.Combine(folder, fileName);

        var logoBytes = LoadAsset("Assets/Quadro_logo2012_RGB.jpg");
        var regels = offerte.Regels.OrderBy(r => r.Id).ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    DrawHeader(col, offerte, logoBytes);
                    DrawKlant(col, offerte.Klant);

                    if (!string.IsNullOrWhiteSpace(offerte.Opmerking))
                        DrawOpmerking(col, offerte.Opmerking);

                    for (var i = 0; i < regels.Count; i++)
                        DrawRegel(col, regels[i], i + 1);

                    DrawTotals(col, offerte, regels);
                });

                page.Footer().Element(DrawFooter);
            });
        });

        doc.GeneratePdf(path);
        return path;
    }

    // ═══════════════════ HEADER ═══════════════════
    private static void DrawHeader(ColumnDescriptor col, Offerte offerte, byte[]? logoBytes)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().AlignLeft().Column(left =>
            {
                if (logoBytes is not null)
                    left.Item().Height(70).AlignLeft().Image(logoBytes).FitHeight();
            });

            row.ConstantItem(220).AlignRight().Column(right =>
            {
                right.Spacing(2);
                right.Item().AlignRight().Text("OFFERTE").Bold().FontSize(16);
                right.Item().AlignRight().Text($"offertenr. {offerte.OfferteNummer}").SemiBold();
                right.Item().AlignRight().Text($"datum {offerte.Datum:dd/MM/yyyy}");
            });
        });

        col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    // ═══════════════════ KLANTBLOK ═══════════════════
    private static void DrawKlant(ColumnDescriptor col, Klant? klant)
    {
        if (klant is null) return;

        col.Item().PaddingTop(6).PaddingBottom(2).Column(c =>
        {
            c.Spacing(1);
            c.Item().Text($"{klant.Voornaam} {klant.Achternaam}".Trim()).Bold().FontSize(13);

            var adres = string.Join(" ", new[] { klant.Straat, klant.Nummer }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            var plaats = string.Join(" ", new[] { klant.Postcode, klant.Gemeente }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            if (!string.IsNullOrWhiteSpace(adres)) c.Item().Text(adres).FontSize(11);
            if (!string.IsNullOrWhiteSpace(plaats)) c.Item().Text(plaats).FontSize(11);
            if (!string.IsNullOrWhiteSpace(klant.BtwNummer)) c.Item().Text($"BTW: {klant.BtwNummer}").FontSize(10);
        });
    }

    private static void DrawOpmerking(ColumnDescriptor col, string opmerking)
    {
        col.Item().PaddingTop(2).PaddingBottom(2)
            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .Text(opmerking).FontSize(10);
    }

    // ═══════════════════ REGEL ═══════════════════
    private static void DrawRegel(ColumnDescriptor col, OfferteRegel r, int index)
    {
        var titel = !string.IsNullOrWhiteSpace(r.Titel)
            ? r.Titel
            : !string.IsNullOrWhiteSpace(r.TypeLijst?.Artikelnummer)
                ? r.TypeLijst!.Artikelnummer
                : $"Werkstuk {index}";

        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(block =>
        {
            block.Spacing(2);

            block.Item().Row(row =>
            {
                row.RelativeItem().Text($"{r.AantalStuks}× {titel}").SemiBold().FontSize(11);
                row.ConstantItem(90).AlignRight().Text(Eur(r.TotaalInclBtw)).SemiBold();
            });

            var metaParts = new List<string>();
            if (r.BreedteCm > 0 && r.HoogteCm > 0)
                metaParts.Add($"{r.BreedteCm:0.##} × {r.HoogteCm:0.##} cm");
            if (!string.IsNullOrWhiteSpace(r.TypeLijst?.Artikelnummer) && !string.IsNullOrWhiteSpace(r.Titel))
                metaParts.Add($"lijst: {r.TypeLijst!.Artikelnummer}");
            if (!string.IsNullOrWhiteSpace(r.InlegLabel))
                metaParts.Add(r.InlegLabel);
            if (metaParts.Count > 0)
                block.Item().Text(string.Join("    ", metaParts)).FontSize(9.5f);

            if (!string.IsNullOrWhiteSpace(r.AfwerkingSamenvatting))
                block.Item().Text(r.AfwerkingSamenvatting).FontSize(9).FontColor(Colors.Grey.Darken1);

            if (!string.IsNullOrWhiteSpace(r.Opmerking))
                block.Item().Text(r.Opmerking).FontSize(9).Italic();
        });

        col.Item().PaddingBottom(3);
    }

    // ═══════════════════ TOTALEN ═══════════════════
    // Zelfde kortingformule als PricingEngine.Calculate (korting op het bruto excl.-bedrag van
    // de regels), zodat dit exact aansluit bij de reeds opgeslagen Offerte-totalen.
    private static void DrawTotals(ColumnDescriptor col, Offerte offerte, IReadOnlyList<OfferteRegel> regels)
    {
        var brutoExcl = Math.Round(regels.Sum(r => r.SubtotaalExBtw), 2);
        var heeftKorting = offerte.KortingPct > 0m && brutoExcl > 0m;
        var kortingExcl = heeftKorting ? Math.Round(brutoExcl * (offerte.KortingPct / 100m), 2) : 0m;

        col.Item().PaddingTop(8).Column(totaalCol =>
        {
            totaalCol.Spacing(2);

            totaalCol.Item().Row(r =>
            {
                r.RelativeItem().Text("Subtotaal (excl. BTW)");
                r.ConstantItem(130).AlignRight().Text(Eur(brutoExcl));
            });

            if (heeftKorting)
            {
                totaalCol.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Korting {offerte.KortingPct.ToString("0.##", CultureInfo.InvariantCulture)}%")
                        .FontColor(Colors.Red.Darken2);
                    r.ConstantItem(130).AlignRight().Text($"- {Eur(kortingExcl)}")
                        .FontColor(Colors.Red.Darken2);
                });
            }

            totaalCol.Item().Row(r =>
            {
                r.RelativeItem().Text("BTW");
                r.ConstantItem(130).AlignRight().Text(Eur(offerte.BtwBedrag));
            });

            totaalCol.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("Totaal (incl. BTW)").SemiBold().FontSize(13);
                r.ConstantItem(160).AlignRight().Text(Eur(offerte.TotaalInclBtw)).SemiBold().FontSize(13);
            });
        });
    }

    // ═══════════════════ FOOTER ═══════════════════
    private static void DrawFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).AlignCenter()
                .Text("Liersesteenweg 64 - 3200 Aarschot - T 016 57 08 72 - kaders@quadro.be - www.quadro.be")
                .FontSize(9);
            col.Item().AlignCenter()
                .Text("BTW BE 0636 525 975 - BE28 7343 0100 1820 - BIC KREDBEBB")
                .FontSize(9);
        });
    }

    // ═══════════════════ Helpers (zelfde patroon als PdfKlantenlijstExporter/PdfFactuurExporter) ═══════════════════
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

    private static string Eur(decimal value) => $"€ {value.ToString("0.00", CultureInfo.InvariantCulture)}";
}
