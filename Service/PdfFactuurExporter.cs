using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Logging;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class PdfFactuurExporter : IFactuurExporter
{
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

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Spacing(6);

                    if (logoBytes is not null)
                    {
                        col.Item().AlignCenter().Height(70).Image(logoBytes, ImageScaling.FitHeight);
                    }

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("QUADRO INLIJSTATELIER").SemiBold();
                            c.Item().Text("LIERSESTEENWEG 64");
                            c.Item().Text("3200 AARSCHOT");
                            c.Item().Text("Telnr : 016/57.08.72");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"{factuur.DocumentType.ToLowerInvariant()}nr. {factuur.FactuurNummer}");
                            c.Item().Text($"datum {factuur.FactuurDatum:dd/MM/yyyy}");
                            c.Item().Text($"vervaldatum {factuur.VervalDatum:dd/MM/yyyy}");
                            if (!string.IsNullOrWhiteSpace(factuur.AangenomenDoorInitialen))
                                c.Item().Text($"initialen {factuur.AangenomenDoorInitialen}");
                        });
                    });

                    col.Item().PaddingTop(4).Text("OPENINGSUREN: DINSDAG TOT VRIJDAG: 10-12U / 13-18U\nZATERDAG: 10-16U DOORLOPEND\nZONDAG EN MAANDAG GESLOTEN").Italic();
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text(factuur.KlantNaam).SemiBold();
                    if (!string.IsNullOrWhiteSpace(factuur.KlantAdres))
                        col.Item().Text(factuur.KlantAdres);
                    if (!string.IsNullOrWhiteSpace(factuur.KlantBtwNummer))
                        col.Item().Text($"BTW: {factuur.KlantBtwNummer}");

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(def =>
                        {
                            def.RelativeColumn(4);
                            def.RelativeColumn(0.9f);
                            def.RelativeColumn(1.2f);
                            def.RelativeColumn(0.8f);
                            def.RelativeColumn(1.2f);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Beschrijving");
                            h.Cell().Element(CellHeader).AlignRight().Text("Aantal");
                            h.Cell().Element(CellHeader).AlignRight().Text("Prijs excl.");
                            h.Cell().Element(CellHeader).AlignRight().Text("Btw");
                            h.Cell().Element(CellHeader).AlignRight().Text("Totaal");
                        });

                        foreach (var lijn in factuur.Lijnen.OrderBy(x => x.Sortering))
                        {
                            t.Cell().Element(CellBody).Text(lijn.Omschrijving);
                            t.Cell().Element(CellBody).AlignRight().Text($"{lijn.Aantal:0.##}");
                            t.Cell().Element(CellBody).AlignRight().Text(Eur(lijn.PrijsExcl));
                            t.Cell().Element(CellBody).AlignRight().Text($"{lijn.BtwPct:0.##}%");
                            t.Cell().Element(CellBody).AlignRight().Text(Eur(lijn.TotaalIncl));
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Width(260).Table(t =>
                    {
                        t.ColumnsDefinition(x =>
                        {
                            x.RelativeColumn(1.4f);
                            x.RelativeColumn();
                        });

                        t.Cell().Text("Subtotaal");
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalExclBtw));

                        var btwLabel = factuur.IsBtwVrijgesteld ? "Btw (0%)" : "Btw (21%)";
                        t.Cell().Text(btwLabel);
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalBtw));

                        t.Cell().Text("Totaal").SemiBold();
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalInclBtw)).SemiBold();
                    });

                    if (factuur.IsBtwVrijgesteld)
                    {
                        col.Item().Text("BTW-vrijstelling van toepassing volgens artikel 44 van het BTW-wetboek.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                    }

                    if (!string.IsNullOrWhiteSpace(factuur.Opmerking))
                    {
                        col.Item().Text($"Opmerking: {factuur.Opmerking}");
                    }

                    col.Item().PaddingTop(18).Text("VOOR AKKOORD : ______________________________");
                });

                page.Footer().AlignCenter().Column(col =>
                {
                    col.Item().Text("Liersesteenweg 64 - 3200 Aarschot - T 016 57 08 72 - kaders@quadro.be - www.quadro.be")
                        .FontSize(9);
                    col.Item().Text("BTW BE 0636 525 975 - BE28 7343 0100 1820 - BIC KREDBEBB")
                        .FontSize(9);
                    col.Item().Text("Openingsuren : Di t/m Vr 10-12 & 13-18.00 - Za doorlopend 10-17 - Zo/Ma gesloten")
                        .FontSize(9);
                });
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

    private static string Eur(decimal value) => $"€ {value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5).PaddingHorizontal(2).DefaultTextStyle(x => x.SemiBold());

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(2);
}
