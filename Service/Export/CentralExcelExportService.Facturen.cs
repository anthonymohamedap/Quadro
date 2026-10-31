using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    // US-45 — bestelbonnen als Excel-dataset (hoofdwerkblad + lijnen). US-99: overal "bestelbon".
    private static DatasetDefinitie BuildFacturenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Facturen,
            "Bestelbonnen",
            "Bestelbonnen met bedragen, status en optioneel hun lijnen.",
            "Bestelbonnen",
            "bestelbonnen-export",
            async db => (await db.Facturen.AsNoTracking()
                .OrderByDescending(x => x.FactuurDatum)
                .ThenByDescending(x => x.Id)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((Factuur)row).Id,
            row => $"Bestelbon {((Factuur)row).NummerLabel}",
            row => $"{((Factuur)row).KlantNaam} - {FormatDate(((Factuur)row).FactuurDatum)}",
            [
                Col("factuurNummer", "Nummer", "Basis", row => ((Factuur)row).NummerLabel),
                Col("klant", "Klant", "Klant", row => ((Factuur)row).KlantNaam),
                Col("factuurDatum", "Besteldatum", "Basis", row => FormatDate(((Factuur)row).FactuurDatum)),
                Col("vervalDatum", "Vervaldatum", "Basis", row => FormatDate(((Factuur)row).VervalDatum)),
                Col("status", "Status", "Basis", row => ((Factuur)row).Status.Label()),
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
                    "Bestelbonlijnen",
                    "Voeg de lijnen van elke bestelbon als apart werkblad toe.",
                    "Bestelbonnen - Lijnen",
                    [
                        Col("factuurId", "Bestelbon-id", "Koppeling", row => ((FactuurLijn)row).FactuurId),
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
