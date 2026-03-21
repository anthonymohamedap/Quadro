using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Logging;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class PdfFactuurExporter : IFactuurExporter
{
    private const float Margin = 36;
    private const float ItemHeightEstimate = 140;
    private const float FooterReserve = 230;

    private readonly ILogger<PdfFactuurExporter>? _logger;

    public PdfFactuurExporter(ILogger<PdfFactuurExporter>? logger = null)
    {
        _logger = logger;
    }

    public ExportFormaat Formaat => ExportFormaat.Pdf;

    public Task<ExportResult> ExportAsync(Factuur factuur, string exportFolder)
    {
        Directory.CreateDirectory(exportFolder);
        var safeNumber = factuur.FactuurNummer.Replace('/', '-');
        var path = Path.Combine(exportFolder, $"{factuur.DocumentType}-{safeNumber}.pdf");
        _logger?.LogInformation("PDF export map gegarandeerd: {Folder}", exportFolder);

        // Logo's laden
        var logoBytes      = LoadAsset("Assets/Quadro_logo2012_RGB.jpg");
        var hibLogoBytes   = LoadAsset("Assets/hib logo 7.4 Kb.jpg");
        var guildLogoBytes = LoadAsset("Assets/Guild Certified Framer logo 125px.png");

        var items = factuur.Lijnen
            .OrderBy(x => x.Sortering)
            .Select((lijn, index) => BuildRenderItem(lijn, index + 1))
            .ToList();

        var voorschot = factuur.VoorschotBedrag;
        var teBetalenBijAfhalen = factuur.TotaalInclBtw - voorschot;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(Margin);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    DrawHeader(col, factuur, logoBytes);
                    DrawSpecialeMededeling(col, factuur.Opmerking);
                    DrawCustomerBlock(col, factuur);

                    float currentY = 300;
                    for (var i = 0; i < items.Count; i++)
                    {
                        DrawItemBlock(col, items[i], i + 1, ref currentY);
                    }

                    DrawTotals(col, factuur.TotaalInclBtw, voorschot, teBetalenBijAfhalen);
                    DrawSignature(col);
                });

                page.Footer().Element(c => DrawFooter(c, hibLogoBytes, guildLogoBytes));
            });
        });

        doc.GeneratePdf(path);

        if (!File.Exists(path))
        {
            _logger?.LogError("PDF export mislukt: bestand niet gevonden na generate. Verwacht pad: {Pad}", path);
            return Task.FromResult(ExportResult.Fail("PDF export mislukt: bestand niet aangemaakt."));
        }

        _logger?.LogInformation("PDF export succesvol geschreven: {Pad}", path);
        return Task.FromResult(ExportResult.Ok(path, $"Export gelukt: {path}"));
    }

    // ═══════════════════ HEADER ═══════════════════
    private static void DrawHeader(ColumnDescriptor col, Factuur factuur, byte[]? logoBytes)
    {
        if (logoBytes is not null)
            col.Item().AlignCenter().Height(60).Image(logoBytes).FitHeight();

        col.Item().Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Spacing(2);
                left.Item().Text("QUADRO INLIJSTATELIER").SemiBold();
                left.Item().Text("LIERSESTEENWEG 64");
                left.Item().Text("3200 AARSCHOT");
                left.Item().Text("Telnr : 016/57.08.72");
            });

            row.ConstantItem(220).AlignRight().Column(right =>
            {
                right.Spacing(2);
                var isFactuur = string.Equals(factuur.DocumentType, "Factuur", StringComparison.OrdinalIgnoreCase);
                var nummerLabel = isFactuur ? "factuurnr." : "bestelbonnr.";
                var datumLabel = isFactuur ? "factuurdatum" : "besteldatum";

                right.Item().AlignRight().Text($"{nummerLabel} {factuur.FactuurNummer}").SemiBold();
                right.Item().AlignRight().Text($"{datumLabel} {factuur.FactuurDatum:dd/MM/yyyy}");
            });
        });

        col.Item().PaddingTop(4).Text("OPENINGSUREN: DINSDAG TOT VRIJDAG: 10-12U / 13-18U\nZATERDAG: 10-16U DOORLOPEND\nZONDAG EN MAANDAG GESLOTEN").Italic();
    }

    // ═══════════════════ SPECIALE MEDEDELING (Factuur.Opmerking) ═══════════════════
    private static void DrawSpecialeMededeling(ColumnDescriptor col, string? opmerking)
    {
        if (string.IsNullOrWhiteSpace(opmerking))
            return;

        col.Item().PaddingTop(4).PaddingBottom(4)
            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .Column(block =>
            {
                block.Item().Text("SPECIALE MEDEDELING:").SemiBold().FontSize(10);
                block.Item().Text(opmerking).FontSize(10);
            });
    }

    // ═══════════════════ KLANTBLOK ═══════════════════
    private static void DrawCustomerBlock(ColumnDescriptor col, Factuur factuur)
    {
        col.Item().PaddingTop(2).Column(c =>
        {
            c.Spacing(2);
            c.Item().Text(factuur.KlantNaam).SemiBold();
            if (!string.IsNullOrWhiteSpace(factuur.KlantAdres))
                c.Item().Text(factuur.KlantAdres);
            if (!string.IsNullOrWhiteSpace(factuur.KlantBtwNummer))
                c.Item().Text($"BTW: {factuur.KlantBtwNummer}");
            if (!string.IsNullOrWhiteSpace(factuur.AangenomenDoorInitialen))
                c.Item().Text($"initialen: {factuur.AangenomenDoorInitialen}");
        });
    }

    // ═══════════════════ ITEM BLOK ═══════════════════
    private static void DrawItemBlock(ColumnDescriptor col, RenderItem item, int index, ref float currentY)
    {
        if (currentY + ItemHeightEstimate > PageSizes.A4.Height - FooterReserve)
        {
            col.Item().PageBreak();
            currentY = 120;
        }

        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(block =>
        {
            block.Spacing(3);

            // Titel: gebruik Titel als beschikbaar, anders artikelnummer
            var titelTekst = $"{index} stuk in te lijsten : {item.Title}";
            if (!string.IsNullOrWhiteSpace(item.RegelOpmerking))
                titelTekst += $"  —  {item.RegelOpmerking}";
            block.Item().Text(titelTekst).SemiBold();

            // Als er een custom titel is, toon artikelnummer apart
            if (!string.IsNullOrWhiteSpace(item.Titel) && !string.IsNullOrWhiteSpace(item.LijstCode))
                block.Item().Text($"lijst: {item.LijstCode}").FontSize(10);

            // Afmetingen
            var metaLine1 = JoinMeta(
                item.BreedteCm is null ? null : $"breedte : {item.BreedteCm:0.##} cm",
                string.IsNullOrWhiteSpace(item.LijstCode) || !string.IsNullOrWhiteSpace(item.Titel)
                    ? null : $"lijst : {item.LijstCode}",
                string.IsNullOrWhiteSpace(item.Inleg1) ? null : $"inleg 1: {item.Inleg1}");
            if (!string.IsNullOrWhiteSpace(metaLine1))
                block.Item().Text(metaLine1);

            var metaLine2 = JoinMeta(
                item.HoogteCm is null ? null : $"hoogte : {item.HoogteCm:0.##} cm",
                null,
                string.IsNullOrWhiteSpace(item.Inleg2) ? null : $"inleg 2: {item.Inleg2}");
            if (!string.IsNullOrWhiteSpace(metaLine2))
                block.Item().Text(metaLine2);

            // Afwerkingen
            foreach (var afw in item.Afwerkingen)
                block.Item().Text($"    \u2022 {afw}").FontSize(10);

            // TypeLijst opmerking
            if (!string.IsNullOrWhiteSpace(item.LijstOpmerking))
                block.Item().Text($"    lijst opmerking: {item.LijstOpmerking}").Italic().FontSize(10);

            // Operatie-regels (backward compat voor oude data)
            foreach (var operation in item.OperationLines)
                block.Item().Text(operation);

            if (!string.IsNullOrWhiteSpace(item.AfhalenOp))
                block.Item().Text($"afhalen op {item.AfhalenOp}");

            // Prijs rechts
            block.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(130).AlignRight().Text(Eur(item.ItemTotal)).SemiBold();
            });
        });

        col.Item().PaddingBottom(6);
        currentY += ItemHeightEstimate;
    }

    // ═══════════════════ TOTALEN ═══════════════════
    private static void DrawTotals(ColumnDescriptor col, decimal totaal, decimal voorschot, decimal teBetalen)
    {
        col.Item().PaddingTop(12).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Spacing(4);
                left.Item().Text($"totaal prijs    {Eur(totaal)}");
                left.Item().Text($"voorschot       {Eur(voorschot)}");
            });

            row.RelativeItem().AlignRight().Column(right =>
            {
                right.Item().AlignRight().Text($"te betalen bij afhalen {Eur(teBetalen)}").SemiBold().FontSize(14);
            });
        });
    }

    private static void DrawSignature(ColumnDescriptor col)
    {
        col.Item().PaddingTop(20).Text("VOOR AKKOORD : ______________________");
    }

    // ═══════════════════ FOOTER met logo's ═══════════════════
    private static void DrawFooter(IContainer container, byte[]? hibLogo, byte[]? guildLogo)
    {
        container.Column(col =>
        {
            // Logo-rij onderaan
            if (hibLogo is not null || guildLogo is not null)
            {
                col.Item().PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem();
                    if (hibLogo is not null)
                        row.ConstantItem(55).Height(28).Image(hibLogo).FitArea();
                    row.ConstantItem(14);
                    if (guildLogo is not null)
                        row.ConstantItem(55).Height(28).Image(guildLogo).FitArea();
                    row.RelativeItem();
                });
            }

            col.Item().AlignCenter().Text("Liersesteenweg 64 - 3200 Aarschot - T 016 57 08 72 - kaders@quadro.be - www.quadro.be").FontSize(9);
            col.Item().AlignCenter().Text("BTW BE 0636 525 975 - BE28 7343 0100 1820 - BIC KREDBEBB").FontSize(9);
            col.Item().AlignCenter().Text("Openingsuren : Di t/m Vr 10-12 & 13-18.00 - Za doorlopend 10-17 - Zo/Ma gesloten").FontSize(9);
        });
    }

    // ═══════════════════ BuildRenderItem — tagged parsing ═══════════════════
    private static RenderItem BuildRenderItem(FactuurLijn lijn, int index)
    {
        var parts = lijn.Omschrijving
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var artikelnummer = parts.ElementAtOrDefault(0);
        var dimsPart      = parts.ElementAtOrDefault(1);

        // Afmetingen parsen
        decimal? breedte = null;
        decimal? hoogte = null;

        if (!string.IsNullOrWhiteSpace(dimsPart))
        {
            var match = Regex.Match(dimsPart, @"(?<w>\d+(?:[\.,]\d+)?)\s*x\s*(?<h>\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (decimal.TryParse(match.Groups["w"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
                    breedte = b;
                if (decimal.TryParse(match.Groups["h"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var h))
                    hoogte = h;
            }
        }

        // Tagged segmenten (vanaf index 2) uitpakken
        var afwerkingen = new List<string>();
        string? titel = null;
        string? regelOpmerking = null;
        string? lijstOpmerking = null;
        var operations = new List<string>();

        var knownTags = new[] { "titel:", "glas:", "pp1:", "pp2:", "diepte:", "opkleven:", "rug:", "lijst_opm:", "opm:" };
        var tagLabels = new Dictionary<string, string>
        {
            ["glas:"]     = "Glas",
            ["pp1:"]      = "Passe-partout 1",
            ["pp2:"]      = "Passe-partout 2",
            ["diepte:"]   = "Dieptekern",
            ["opkleven:"] = "Opkleven",
            ["rug:"]      = "Rug"
        };

        foreach (var part in parts.Skip(2))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            var matchedTag = knownTags.FirstOrDefault(t => part.StartsWith(t, StringComparison.OrdinalIgnoreCase));
            if (matchedTag is not null)
            {
                var value = part[matchedTag.Length..].Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (matchedTag == "titel:")
                    titel = value;
                else if (matchedTag == "opm:")
                    regelOpmerking = value;
                else if (matchedTag == "lijst_opm:")
                    lijstOpmerking = value;
                else if (tagLabels.TryGetValue(matchedTag, out var label))
                    afwerkingen.Add($"{label}: {value}");
            }
            else
            {
                // Backward-compat: untagged segment → operation line
                operations.Add(part);
            }
        }

        if (operations.Count == 0 && afwerkingen.Count == 0)
            operations.Add("Lijstwerk volgens bestelbon.");

        // Titel-logica: als Titel ingevuld → gebruik die, anders artikelnummer
        var displayTitle = !string.IsNullOrWhiteSpace(titel)
            ? titel
            : !string.IsNullOrWhiteSpace(artikelnummer) ? artikelnummer : $"Werkstuk {index}";

        return new RenderItem(
            Title: displayTitle,
            BreedteCm: breedte,
            HoogteCm: hoogte,
            AfwCode: null,
            LijstCode: artikelnummer,
            Inleg1: null,
            Inleg2: null,
            OperationLines: operations,
            Afwerkingen: afwerkingen,
            RegelOpmerking: regelOpmerking,
            LijstOpmerking: lijstOpmerking,
            Titel: titel,
            AfhalenOp: null,
            ItemTotal: lijn.TotaalIncl);
    }

    // ═══════════════════ Helpers ═══════════════════
    private static byte[]? LoadAsset(string relativePath)
    {
        // Probeer eerst relatief aan app-directory (bin/Debug/net9.0/)
        var basePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(basePath))
            return File.ReadAllBytes(basePath);

        // Fallback: relatief aan working directory
        var cwdPath = Path.GetFullPath(relativePath);
        if (File.Exists(cwdPath))
            return File.ReadAllBytes(cwdPath);

        // Fallback: relatief aan project root (2-3 niveaus omhoog)
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 5; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir is null) break;
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
                return File.ReadAllBytes(candidate);
        }

        return null;
    }

    private static string JoinMeta(string? left, string? middle, string? right)
        => string.Join("      ", new[] { left, middle, right }.Where(x => !string.IsNullOrWhiteSpace(x))!);

    private static string Eur(decimal value) => $"\u20ac {value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private sealed record RenderItem(
        string Title,
        decimal? BreedteCm,
        decimal? HoogteCm,
        string? AfwCode,
        string? LijstCode,
        string? Inleg1,
        string? Inleg2,
        IReadOnlyList<string> OperationLines,
        IReadOnlyList<string> Afwerkingen,
        string? RegelOpmerking,
        string? LijstOpmerking,
        string? Titel,
        string? AfhalenOp,
        decimal ItemTotal);
}
