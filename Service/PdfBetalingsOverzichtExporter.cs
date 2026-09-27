using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Globalization;
using System.IO;

namespace QuadroApp.Service;

/// <summary>
/// US-67 — de twee afdrukken van het "overzicht betalingen", naar het voorbeeld van de oude
/// kassa-afdrukken van Quadro:
/// <list type="bullet">
/// <item><b>Per dag (detail)</b>: elke ontvangst met datum, bon, bedrag in de kolom van de
/// betaalwijze en jaar; per dag een regel "totaal" met het dagtotaal vet links.</item>
/// <item><b>Per periode (dagtotalen)</b>: één regel per dag (datum, dag, totalen per betaalwijze),
/// eindtotalen per betaalwijze en vet het grote totaal.</item>
/// </list>
/// Zelfde QuestPDF-aanpak als <see cref="PdfKlantenlijstExporter"/>.
/// </summary>
public sealed class PdfBetalingsOverzichtExporter
{
    private static readonly CultureInfo Nl = CultureInfo.GetCultureInfo("nl-BE");

    private static readonly (Betaalwijze Wijze, string Kop)[] Kolommen =
    {
        (Betaalwijze.Kontant, "kontant"),
        (Betaalwijze.Cheque, "cheque"),
        (Betaalwijze.Visa, "visa"),
        (Betaalwijze.Bancontact, "bancontact"),
        (Betaalwijze.Proton, "proton"),
        (Betaalwijze.Storting, "storting")
    };

    public string ExportDetail(BetalingsOverzicht overzicht)
        => Genereer(overzicht, "per-dag", (col) => TekenDetail(col, overzicht));

    public string ExportPerDag(BetalingsOverzicht overzicht)
        => Genereer(overzicht, "dagtotalen", (col) => TekenDagtotalen(col, overzicht));

    private static string Genereer(BetalingsOverzicht overzicht, string soort, Action<ColumnDescriptor> inhoud)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "QuadroApp", "Overzicht betalingen");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder,
            $"betalingen-{soort}-{overzicht.Van:yyyyMMdd}-{overzicht.Tot:yyyyMMdd}-{DateTime.Now:HHmmss}.pdf");

        var afgedruktOp = DateTime.Now;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(c => TekenKop(c, overzicht, afgedruktOp));

                page.Content().PaddingTop(6).Column(col =>
                {
                    if (overzicht.Dagen.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Geen ontvangsten in deze periode.")
                            .Italic().FontColor(Colors.Grey.Medium);
                        return;
                    }

                    inhoud(col);
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KOP: "Quadro — overzicht betalingen van .. tot ..", blz, afdruktijdstip
    // ══════════════════════════════════════════════════════════════════════════
    private static void TekenKop(IContainer container, BetalingsOverzicht o, DateTime afgedruktOp)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(links =>
                {
                    links.Item().Text("Quadro").Bold().FontSize(13);
                    links.Item().Text($"overzicht betalingen van {o.Van:dd/MM/yy} tot {o.Tot:dd/MM/yy}").FontSize(10);
                });

                row.ConstantItem(140).AlignRight().Column(rechts =>
                {
                    rechts.Item().AlignRight().Text(t =>
                    {
                        t.Span("blz ");
                        t.CurrentPageNumber();
                    });
                    rechts.Item().AlignRight().Text($"{afgedruktOp:dd/MM/yy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AFDRUK 1: per dag detail
    // ══════════════════════════════════════════════════════════════════════════
    private static void TekenDetail(ColumnDescriptor col, BetalingsOverzicht o)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(58); // datum / dagtotaal
                c.ConstantColumn(62); // bon
                foreach (var _ in Kolommen) c.RelativeColumn();
                c.ConstantColumn(34); // jaar
            });

            table.Header(h =>
            {
                h.Cell().Element(KopCel).Text("datum");
                h.Cell().Element(KopCel).Text("bon");
                foreach (var k in Kolommen)
                    h.Cell().Element(KopCel).AlignRight().Text(k.Kop);
                h.Cell().Element(KopCel).AlignRight().Text("jaar");
            });

            foreach (var dag in o.Dagen)
            {
                foreach (var r in dag.Regels)
                {
                    table.Cell().Element(Cel).Text($"{r.Datum:dd/MM/yy}");
                    table.Cell().Element(Cel).Text(r.Bon);
                    foreach (var k in Kolommen)
                        table.Cell().Element(Cel).AlignRight().Text(r.Betaalwijze == k.Wijze ? Bedrag(r.Bedrag) : "");
                    table.Cell().Element(Cel).AlignRight().Text(r.Jaar?.ToString(CultureInfo.InvariantCulture) ?? "");
                }

                // "totaal"-regel per dag: dagtotaal vet links, subtotalen per betaalwijze.
                table.Cell().Element(TotaalCel).Text(Bedrag(dag.Totaal)).Bold();
                table.Cell().Element(TotaalCel).Text("totaal").Italic();
                foreach (var k in Kolommen)
                    table.Cell().Element(TotaalCel).AlignRight().Text(LeegAlsNul(dag.Voor(k.Wijze)));
                table.Cell().Element(TotaalCel).Text("");
            }

            TekenEindtotalen(table, o, eersteKolomLabel: Bedrag(o.Totaal), tweedeKolomLabel: "eindtotaal", extraKolom: true);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AFDRUK 2: per periode, één regel per dag
    // ══════════════════════════════════════════════════════════════════════════
    private static void TekenDagtotalen(ColumnDescriptor col, BetalingsOverzicht o)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(58); // datum
                c.ConstantColumn(34); // dag
                foreach (var _ in Kolommen) c.RelativeColumn();
                c.RelativeColumn();    // totaal
            });

            table.Header(h =>
            {
                h.Cell().Element(KopCel).Text("datum");
                h.Cell().Element(KopCel).Text("dag");
                foreach (var k in Kolommen)
                    h.Cell().Element(KopCel).AlignRight().Text(k.Kop);
                h.Cell().Element(KopCel).AlignRight().Text("totaal");
            });

            foreach (var dag in o.Dagen)
            {
                table.Cell().Element(Cel).Text($"{dag.Datum:dd/MM/yy}");
                table.Cell().Element(Cel).Text(dag.DagNaam);
                foreach (var k in Kolommen)
                    table.Cell().Element(Cel).AlignRight().Text(LeegAlsNul(dag.Voor(k.Wijze)));
                table.Cell().Element(Cel).AlignRight().Text(Bedrag(dag.Totaal)).SemiBold();
            }

            TekenEindtotalen(table, o, eersteKolomLabel: "", tweedeKolomLabel: "totaal", extraKolom: false);
        });
    }

    /// <summary>Eindtotalen per betaalwijze + vet het grote totaal.</summary>
    private static void TekenEindtotalen(TableDescriptor table, BetalingsOverzicht o,
        string eersteKolomLabel, string tweedeKolomLabel, bool extraKolom)
    {
        table.Cell().Element(EindCel).Text(eersteKolomLabel).Bold();
        table.Cell().Element(EindCel).Text(tweedeKolomLabel).Bold();
        foreach (var k in Kolommen)
            table.Cell().Element(EindCel).AlignRight().Text(Bedrag(o.Voor(k.Wijze))).SemiBold();
        table.Cell().Element(EindCel).AlignRight().Text(extraKolom ? "" : Bedrag(o.Totaal)).Bold();

        var aantalKolommen = (uint)(2 + Kolommen.Length + 1);
        table.Cell().ColumnSpan(aantalKolommen).PaddingTop(8).AlignRight()
            .Text($"Totaal ontvangen: € {Bedrag(o.Totaal)}").Bold().FontSize(11);
    }

    // ── Celstijlen ──
    private static IContainer KopCel(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(3).PaddingHorizontal(2).DefaultTextStyle(x => x.Bold());

    private static IContainer Cel(IContainer c) =>
        c.PaddingVertical(1.5f).PaddingHorizontal(2);

    private static IContainer TotaalCel(IContainer c) =>
        c.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(1.5f).PaddingBottom(6).PaddingHorizontal(2);

    private static IContainer EindCel(IContainer c) =>
        c.BorderTop(1).BorderColor(Colors.Grey.Darken2).PaddingVertical(4).PaddingHorizontal(2);

    private static string Bedrag(decimal bedrag) => bedrag.ToString("#,##0.00", Nl);

    private static string LeegAlsNul(decimal bedrag) => bedrag == 0m ? "" : Bedrag(bedrag);
}
