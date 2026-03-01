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
    private const float ItemHeightEstimate = 122;
    private const float FooterReserve = 210;

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

        var logoPath = Path.GetFullPath("Assets/Quadro_logo2012_RGB.jpg");
        var logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

        var items = factuur.Lijnen
            .OrderBy(x => x.Sortering)
            .Select((lijn, index) => BuildRenderItem(lijn, index + 1))
            .ToList();

        var voorschot = 0m;
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
                    DrawCustomerBlock(col, factuur);

                    float currentY = 300;
                    for (var i = 0; i < items.Count; i++)
                    {
                        DrawItemBlock(col, items[i], i + 1, ref currentY);
                    }

                    DrawTotals(col, factuur.TotaalInclBtw, voorschot, teBetalenBijAfhalen);
                    DrawSignature(col);
                });

                page.Footer().Element(c => DrawFooter(c));
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

    private static void DrawHeader(ColumnDescriptor col, Factuur factuur, byte[]? logoBytes)
    {
        if (logoBytes is not null)
            col.Item().AlignCenter().Height(60).Image(logoBytes, ImageScaling.FitHeight);

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

            block.Item().Text($"{index} stuk in te lijsten : {item.Title}").SemiBold();

            var metaLine1 = JoinMeta(
                item.BreedteCm is null ? null : $"breedte : {item.BreedteCm:0.##} cm",
                string.IsNullOrWhiteSpace(item.AfwCode) ? null : $"afw. : {item.AfwCode}",
                string.IsNullOrWhiteSpace(item.Inleg1) ? null : $"inleg 1: {item.Inleg1}");
            if (!string.IsNullOrWhiteSpace(metaLine1))
                block.Item().Text(metaLine1);

            var metaLine2 = JoinMeta(
                item.HoogteCm is null ? null : $"hoogte : {item.HoogteCm:0.##} cm",
                string.IsNullOrWhiteSpace(item.LijstCode) ? null : $"lijst : {item.LijstCode}",
                string.IsNullOrWhiteSpace(item.Inleg2) ? null : $"inleg 2: {item.Inleg2}");
            if (!string.IsNullOrWhiteSpace(metaLine2))
                block.Item().Text(metaLine2);

            foreach (var operation in item.OperationLines)
                block.Item().Text(operation);

            if (!string.IsNullOrWhiteSpace(item.AfhalenOp))
                block.Item().Text($"afhalen op {item.AfhalenOp}");

            block.Item().PaddingTop(2).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(130).AlignRight().Text(Eur(item.ItemTotal)).SemiBold();
            });
        });

        col.Item().PaddingBottom(6);
        currentY += ItemHeightEstimate;
    }

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

    private static void DrawFooter(IContainer container)
    {
        container.AlignCenter().Column(col =>
        {
            col.Item().Text("Liersesteenweg 64 - 3200 Aarschot - T 016 57 08 72 - kaders@quadro.be - www.quadro.be").FontSize(9);
            col.Item().Text("BTW BE 0636 525 975 - BE28 7343 0100 1820 - BIC KREDBEBB").FontSize(9);
            col.Item().Text("Openingsuren : Di t/m Vr 10-12 & 13-18.00 - Za doorlopend 10-17 - Zo/Ma gesloten").FontSize(9);
        });
    }

    private static RenderItem BuildRenderItem(FactuurLijn lijn, int index)
    {
        var parts = lijn.Omschrijving
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var eerste = parts.ElementAtOrDefault(0);
        var tweede = parts.ElementAtOrDefault(1);
        var derde = parts.ElementAtOrDefault(2);

        decimal? breedte = null;
        decimal? hoogte = null;

        if (!string.IsNullOrWhiteSpace(tweede))
        {
            var match = Regex.Match(tweede, @"(?<w>\d+(?:[\.,]\d+)?)\s*x\s*(?<h>\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (decimal.TryParse(match.Groups["w"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
                    breedte = b;
                if (decimal.TryParse(match.Groups["h"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var h))
                    hoogte = h;
            }
        }

        var operations = new List<string>();
        if (!string.IsNullOrWhiteSpace(derde))
            operations.Add(derde);

        foreach (var part in parts.Skip(3))
        {
            if (!string.IsNullOrWhiteSpace(part))
                operations.Add(part);
        }

        if (operations.Count == 0)
            operations.Add("Lijstwerk volgens bestelbon.");

        var title = !string.IsNullOrWhiteSpace(derde)
            ? derde
            : !string.IsNullOrWhiteSpace(eerste) ? eerste : $"Werkstuk {index}";

        return new RenderItem(
            Title: title,
            BreedteCm: breedte,
            HoogteCm: hoogte,
            AfwCode: eerste,
            LijstCode: eerste,
            Inleg1: null,
            Inleg2: null,
            OperationLines: operations,
            AfhalenOp: null,
            ItemTotal: lijn.TotaalIncl);
    }

    private static string JoinMeta(string? left, string? middle, string? right)
        => string.Join("      ", new[] { left, middle, right }.Where(x => !string.IsNullOrWhiteSpace(x))!);

    private static string Eur(decimal value) => $"€ {value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private sealed record RenderItem(
        string Title,
        decimal? BreedteCm,
        decimal? HoogteCm,
        string? AfwCode,
        string? LijstCode,
        string? Inleg1,
        string? Inleg2,
        IReadOnlyList<string> OperationLines,
        string? AfhalenOp,
        decimal ItemTotal);
}
