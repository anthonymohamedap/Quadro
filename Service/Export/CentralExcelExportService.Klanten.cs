using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    private static DatasetDefinitie BuildKlantenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Klanten,
            "Klanten",
            "Export van klanten met contactinformatie en optionele offerte-relatie.",
            "Klanten",
            "klanten-export",
            async db => (await db.Klanten.AsNoTracking()
                .OrderBy(x => x.Achternaam)
                .ThenBy(x => x.Voornaam)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((Klant)row).Id,
            row => FormatKlant((Klant)row),
            row => ((Klant)row).Email ?? ((Klant)row).Gemeente ?? string.Empty,
            [
                Col("id", "Id", "Basis", row => ((Klant)row).Id),
                Col("voornaam", "Voornaam", "Basis", row => ((Klant)row).Voornaam),
                Col("achternaam", "Achternaam", "Basis", row => ((Klant)row).Achternaam),
                Col("email", "E-mail", "Contact", row => ((Klant)row).Email ?? string.Empty),
                Col("telefoon", "Telefoon", "Contact", row => ((Klant)row).Telefoon ?? string.Empty),
                Col("straat", "Straat", "Adres", row => ((Klant)row).Straat ?? string.Empty, false),
                Col("nummer", "Nummer", "Adres", row => ((Klant)row).Nummer ?? string.Empty, false),
                Col("postcode", "Postcode", "Adres", row => ((Klant)row).Postcode ?? string.Empty, false),
                Col("gemeente", "Gemeente", "Adres", row => ((Klant)row).Gemeente ?? string.Empty),
                Col("btwNummer", "Btw-nummer", "Administratie", row => ((Klant)row).BtwNummer ?? string.Empty, false),
                Col("opmerking", "Opmerking", "Administratie", row => ((Klant)row).Opmerking ?? string.Empty, false)
            ],
            [
                new RelatieDefinitie(
                    "offertes",
                    "Offertes",
                    "Voeg gekoppelde offertes van de geselecteerde klanten toe als extra werkblad.",
                    "Klanten - Offertes",
                    [
                        Col("klantId", "KlantId", "Koppeling", row => ((Offerte)row).KlantId?.ToString() ?? string.Empty),
                        Col("klant", "Klant", "Koppeling", row => FormatKlant(((Offerte)row).Klant)),
                        Col("offerteId", "OfferteId", "Basis", row => ((Offerte)row).Id),
                        Col("datum", "Datum", "Basis", row => FormatDate(((Offerte)row).Datum)),
                        Col("status", "Status", "Basis", row => ((Offerte)row).Status.ToString()),
                        Col("totaalIncl", "Totaal incl. btw", "Financieel", row => ((Offerte)row).TotaalInclBtw),
                        Col("geplandeDatum", "Geplande datum", "Planning", row => FormatNullableDate(((Offerte)row).GeplandeDatum)),
                        Col("deadline", "Deadline", "Planning", row => FormatNullableDate(((Offerte)row).DeadlineDatum))
                    ],
                    async (db, ids) => (await db.Offertes.AsNoTracking()
                        .Include(x => x.Klant)
                        .Where(x => x.KlantId.HasValue && ids.Contains(x.KlantId.Value))
                        .OrderByDescending(x => x.Datum)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
