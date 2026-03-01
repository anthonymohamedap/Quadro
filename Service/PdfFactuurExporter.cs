using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class PdfFactuurExporter : IFactuurExporter
{
    public ExportFormaat Formaat => ExportFormaat.Pdf;

    public Task<ExportResult> ExportAsync(Factuur factuur, string exportFolder)
    {
        Directory.CreateDirectory(exportFolder);
        var safeNumber = factuur.FactuurNummer.Replace('/', '-');
        var path = Path.Combine(exportFolder, $"Factuur-{safeNumber}.pdf");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(32);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("QUADRO").FontSize(22).SemiBold();
                    col.Item().Text("Factuur").FontSize(13).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("QUADRO");
                            c.Item().Text("Atelierlaan 1");
                            c.Item().Text("9000 Gent");
                            c.Item().Text("BE 0123.456.789");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(factuur.KlantNaam).SemiBold();
                            if (!string.IsNullOrWhiteSpace(factuur.KlantAdres)) c.Item().Text(factuur.KlantAdres);
                            if (!string.IsNullOrWhiteSpace(factuur.KlantBtwNummer)) c.Item().Text($"BTW: {factuur.KlantBtwNummer}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Factuur #: {factuur.FactuurNummer}");
                            c.Item().Text($"Factuurdatum: {factuur.FactuurDatum:dd/MM/yyyy}");
                            c.Item().Text($"Vervaldatum: {factuur.VervalDatum:dd/MM/yyyy}");
                        });
                    });

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(def =>
                        {
                            def.RelativeColumn(4);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1.3f);
                            def.RelativeColumn(1);
                            def.RelativeColumn(1.3f);
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
                            t.Cell().Element(CellBody).AlignRight().Text($"{lijn.Aantal:0.##} {lijn.Eenheid}");
                            t.Cell().Element(CellBody).AlignRight().Text(Eur(lijn.PrijsExcl));
                            t.Cell().Element(CellBody).AlignRight().Text($"{lijn.BtwPct:0.##}%");
                            t.Cell().Element(CellBody).AlignRight().Text(Eur(lijn.TotaalIncl));
                        }
                    });

                    col.Item().AlignRight().Width(220).Table(t =>
                    {
                        t.ColumnsDefinition(x =>
                        {
                            x.RelativeColumn();
                            x.RelativeColumn();
                        });

                        t.Cell().Text("Subtotaal");
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalExclBtw));
                        t.Cell().Text("BTW");
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalBtw));
                        t.Cell().Text("Totaal").SemiBold();
                        t.Cell().AlignRight().Text(Eur(factuur.TotaalInclBtw)).SemiBold();
                    });

                    if (factuur.IsBtwVrijgesteld)
                    {
                        col.Item().PaddingTop(8).Text("BTW-vrijstelling van toepassing volgens artikel 44 van het BTW-wetboek.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                    }

                    if (!string.IsNullOrWhiteSpace(factuur.Opmerking))
                        col.Item().PaddingTop(6).Text($"Opmerking: {factuur.Opmerking}");
                });

                page.Footer().Text("Algemene voorwaarden: betaling binnen 30 dagen op rekening BE00 0000 0000 0000.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        doc.GeneratePdf(path);
        return Task.FromResult(ExportResult.Ok(path));
    }

    private static string Eur(decimal value) => $"€ {value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static IContainer CellHeader(IContainer container) => container.BorderBottom(1).PaddingVertical(4).PaddingHorizontal(2).DefaultTextStyle(x => x.SemiBold());
    private static IContainer CellBody(IContainer container) => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(2);
}
