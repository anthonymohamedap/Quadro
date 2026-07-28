using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    // US-45 — Facturen/bestelbonnen als Excel-dataset (hoofdwerkblad + factuurregels).
    private static DatasetDefinitie BuildFacturenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Facturen,
            "Facturen / bestelbonnen",
            "Facturen en bestelbonnen met bedragen, status en optioneel hun regels.",
            "Facturen",
            "facturen-export",
            async db => (await db.Facturen.AsNoTracking()
                .OrderByDescending(x => x.FactuurDatum)
                .ThenByDescending(x => x.Id)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((Factuur)row).Id,
            row => $"{((Factuur)row).DocumentType} {((Factuur)row).FactuurNummer}",
            row => $"{((Factuur)row).KlantNaam} - {FormatDate(((Factuur)row).FactuurDatum)}",
            [
                Col("factuurNummer", "Nummer", "Basis", row => ((Factuur)row).FactuurNummer),
                Col("documentType", "Type", "Basis", row => ((Factuur)row).DocumentType),
                Col("klant", "Klant", "Klant", row => ((Factuur)row).KlantNaam),
                Col("factuurDatum", "Datum", "Basis", row => FormatDate(((Factuur)row).FactuurDatum)),
                Col("vervalDatum", "Vervaldatum", "Basis", row => FormatDate(((Factuur)row).VervalDatum)),
                Col("status", "Status", "Basis", row => ((Factuur)row).Status.ToString()),
                Col("subtotaalExcl", "Subtotaal excl. btw", "Financieel", row => ((Factuur)row).TotaalExclBtw),
                Col("btwBedrag", "Btw-bedrag", "Financieel", row => ((Factuur)row).TotaalBtw),
                Col("totaalIncl", "Totaal incl. btw", "Financieel", row => ((Factuur)row).TotaalInclBtw),
                Col("kortingPct", "Korting %", "Financieel", row => ((Factuur)row).KortingPct, false),
                Col("voorschot", "Voorschot", "Financieel", row => ((Factuur)row).VoorschotBedrag, false),
                Col("btwVrijgesteld", "Btw vrijgesteld", "Financieel", row => ((Factuur)row).IsBtwVrijgesteld ? "Ja" : "Nee", false),
                Col("afhaalDatum", "Afhaaldatum", "Administratie", row => FormatNullableDate(((Factuur)row).AfhaalDatum), false),
                Col("opmerking", "Opmerking", "Administratie", row => ((Factuur)row).Opmerking ?? string.Empty, false)
            ],
            [
                new RelatieDefinitie(
                    "factuur-regels",
                    "Factuurregels",
                    "Voeg de regels van elke factuur/bestelbon als apart werkblad toe.",
                    "Facturen - Regels",
                    [
                        Col("factuurId", "FactuurId", "Koppeling", row => ((FactuurLijn)row).FactuurId),
                        Col("omschrijving", "Omschrijving", "Basis", row => ((FactuurLijn)row).Omschrijving),
                        Col("aantal", "Aantal", "Basis", row => ((FactuurLijn)row).Aantal),
                        Col("eenheid", "Eenheid", "Basis", row => ((FactuurLijn)row).Eenheid),
                        Col("prijsExcl", "Prijs excl.", "Financieel", row => ((FactuurLijn)row).PrijsExcl),
                        Col("btwPct", "Btw %", "Financieel", row => ((FactuurLijn)row).BtwPct),
                        Col("totaalExcl", "Totaal excl.", "Financieel", row => ((FactuurLijn)row).TotaalExcl),
                        Col("totaalBtw", "Totaal btw", "Financieel", row => ((FactuurLijn)row).TotaalBtw),
                        Col("totaalIncl", "Totaal incl.", "Financieel", row => ((FactuurLijn)row).TotaalIncl)
                    ],
                    async (db, ids) => (await db.FactuurLijnen.AsNoTracking()
                        .Where(x => ids.Contains(x.FactuurId))
                        .OrderBy(x => x.FactuurId)
                        .ThenBy(x => x.Sortering)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
