using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>US-94 — één inlijsting op de werkbon-PDF.</summary>
public sealed record WerkBonPdfRegel(
    string Kop, string? Gepland, string LijstOfKader, string? Bestelstatus,
    string? Afwerking, string? Inleg, string? Opmerking, decimal PrijsIncl);

/// <summary>US-94 — gegevens voor de werkbon-PDF (werkbon = verkoopnummer).</summary>
public sealed record WerkBonPdfData(
    int Nummer, string KlantRegel, DateTime? AfhaalDatum, IReadOnlyList<WerkBonPdfRegel> Regels, decimal TotaalIncl);

/// <summary>
/// US-94 — werkbon voor het atelier als A4-PDF (QuestPDF, zelfde aanpak als de weeklijst). Standaard
/// zonder prijzen; per inlijsting maat, lijst of standaardkader (naam + omschrijving, US-80), bestelstatus,
/// afwerkingen, inleg, geplande dag en opmerking. Bewaard in Documenten › QuadroApp › Werkbonnen.
/// Afdrukken gebeurt via het printvenster van het systeem (beslist 02/11).
/// </summary>
public sealed class PdfWerkBonExporter
{
    private static readonly CultureInfo Nl = new("nl-BE");

    public static string Map
    {
        get
        {
            // Documenten; bestaat die map niet (bv. Linux zonder ~/Documents), dan de thuismap.
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs))
                docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
            return Path.GetFullPath(Path.Combine(docs, "QuadroApp", "Werkbonnen"));
        }
    }

    public static async Task<WerkBonPdfData> LeesAsync(IDbContextFactory<AppDbContext> factory, int werkBonId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var wb = await db.WerkBonnen.AsNoTracking()
            .Include(w => w.Offerte).ThenInclude(o => o.Klant)
            .Include(w => w.Taken).ThenInclude(t => t.LeverancierBestelLijn)
            .FirstOrDefaultAsync(w => w.Id == werkBonId)
            ?? throw new InvalidOperationException("Werkbon niet gevonden.");
        var offerteId = wb.OfferteId;
        var regels = await db.OfferteRegels.AsNoTracking()
            .Include(r => r.TypeLijst).Include(r => r.KantKlaarKader).Include(r => r.InlegTypeLijst)
            .Include(r => r.Glas).Include(r => r.GlasVariant)
            .Include(r => r.PassePartout1).Include(r => r.PassePartout1Variant)
            .Include(r => r.PassePartout2).Include(r => r.PassePartout2Variant)
            .Include(r => r.DiepteKern).Include(r => r.DiepteKernVariant)
            .Include(r => r.Opkleven).Include(r => r.OpklevenVariant)
            .Include(r => r.Rug).Include(r => r.RugVariant)
            .Where(r => r.OfferteId == offerteId)
            .OrderBy(r => r.Id)
            .ToListAsync();
        var bonNummer = await db.Bestelbonnen.AsNoTracking()
            .Where(b => (b.OfferteId == offerteId || b.WerkBonId == werkBonId) && b.VolgNr != null)
            .Select(b => b.BestelbonNummer).FirstOrDefaultAsync();

        var k = wb.Offerte?.Klant;
        var delen = new List<string>();
        if (k is not null)
        {
            delen.Add(k.VolledigeNaam);
            if (!string.IsNullOrWhiteSpace(k.Telefoon)) delen.Add(k.Telefoon!);
        }
        delen.Add($"werkbon van {wb.AangemaaktOp:dd/MM/yyyy}");
        if (!string.IsNullOrWhiteSpace(bonNummer)) delen.Add($"bestelbon {bonNummer}");

        var pdfRegels = regels.Select(r =>
        {
            var taken = wb.Taken.Where(t => t.OfferteRegelId == r.Id).OrderBy(t => t.GeplandVan).ToList();
            var gepland = taken.Count == 0 ? null
                : "gepland " + string.Join(" + ", taken.Select(t => t.GeplandVan.ToString("ddd d/MM", Nl)));
            var lijst = r.KantKlaarKader is { } kader
                ? $"Standaardkader {kader.Weergave}"
                : r.TypeLijst is { } tl ? $"Lijst {tl.Artikelnummer} {tl.Soort}".Trim() : "Geen lijst gekozen";
            string? bestel = taken.Count == 0 ? null
                : taken.All(t => t.IsOpVoorraad) ? "uit voorraad"
                : taken.All(t => t.IsBesteld || t.IsOpVoorraad)
                    ? $"besteld{(taken.FirstOrDefault(t => t.BestelDatum != null)?.BestelDatum is { } d ? $" op {d:dd/MM}" : "")}"
                    : "te bestellen";
            var titel = string.IsNullOrWhiteSpace(r.Titel) ? "Inlijsting" : r.Titel;
            return new WerkBonPdfRegel(
                $"{r.AantalStuks} × {titel} — {r.BreedteCm:0.#} × {r.HoogteCm:0.#} cm",
                gepland, lijst, bestel,
                string.IsNullOrWhiteSpace(r.AfwerkingSamenvatting) ? null : r.AfwerkingSamenvatting,
                string.IsNullOrWhiteSpace(r.InlegLabel) ? null : r.InlegLabel,
                string.IsNullOrWhiteSpace(r.Opmerking) ? null : r.Opmerking,
                r.TotaalInclBtw);
        }).ToList();

        return new WerkBonPdfData(wb.Offerte?.OfferteNummer ?? wb.Id, string.Join(" · ", delen),
            wb.AfhaalDatum ?? wb.Offerte?.AfhaalDatum, pdfRegels, pdfRegels.Sum(x => x.PrijsIncl));
    }

    /// <summary>Maakt de PDF en geeft het pad terug.</summary>
    public string Export(WerkBonPdfData data, bool metPrijzen)
    {
        Directory.CreateDirectory(Map);
        var pad = Path.Combine(Map, metPrijzen ? $"Werkbon-{data.Nummer}-met-prijzen.pdf" : $"Werkbon-{data.Nummer}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"WERKBON {data.Nummer}").Bold().FontSize(16);
                        row.ConstantItem(200).AlignRight().Text(t =>
                        {
                            t.Span("afhalen ").FontSize(10);
                            t.Span(data.AfhaalDatum is { } a ? a.ToString("ddd dd/MM/yyyy", Nl) : "nog niet gekend").Bold();
                        });
                    });
                    col.Item().Text(data.KlantRegel).FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    foreach (var r in data.Regels)
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(box =>
                        {
                            box.Spacing(2);
                            box.Item().Row(row =>
                            {
                                row.RelativeItem().Text(r.Kop).Bold();
                                if (r.Gepland is not null) row.ConstantItem(160).AlignRight().Text(r.Gepland).FontSize(9);
                            });
                            box.Item().Text(r.Bestelstatus is null ? r.LijstOfKader : $"{r.LijstOfKader} · {r.Bestelstatus}");
                            if (r.Afwerking is not null) box.Item().Text(r.Afwerking).FontSize(9);
                            if (r.Inleg is not null) box.Item().Text(r.Inleg).FontSize(9);
                            if (r.Opmerking is not null) box.Item().Text($"Opmerking: {r.Opmerking}").FontSize(9).Italic();
                            if (metPrijzen) box.Item().AlignRight().Text($"€ {r.PrijsIncl.ToString("#,##0.00", Nl)}").Bold();
                        });
                    }

                    if (metPrijzen)
                        col.Item().AlignRight().Text($"Totaal incl. btw € {data.TotaalIncl.ToString("#,##0.00", Nl)}").Bold();
                });

                page.Footer().AlignRight().Text($"Afgedrukt op {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf(pad);

        return pad;
    }
}
