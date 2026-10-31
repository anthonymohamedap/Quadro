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
    private static DatasetDefinitie BuildBestelbonnenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Bestelbonnen,
            "Bestelbonnen",
            "Bestelbonnen met bedragen, status en optioneel hun lijnen.",
            "Bestelbonnen",
            "bestelbonnen-export",
            async db => (await db.Bestelbonnen.AsNoTracking()
                .OrderByDescending(x => x.BestelbonDatum)
                .ThenByDescending(x => x.Id)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((Bestelbon)row).Id,
            row => $"Bestelbon {((Bestelbon)row).NummerLabel}",
            row => $"{((Bestelbon)row).KlantNaam} - {FormatDate(((Bestelbon)row).BestelbonDatum)}",
            [
                Col("bestelbonNummer", "Nummer", "Basis", row => ((Bestelbon)row).NummerLabel),
                Col("klant", "Klant", "Klant", row => ((Bestelbon)row).KlantNaam),
                Col("bestelbonDatum", "Besteldatum", "Basis", row => FormatDate(((Bestelbon)row).BestelbonDatum)),
                Col("vervalDatum", "Vervaldatum", "Basis", row => FormatDate(((Bestelbon)row).VervalDatum)),
                Col("status", "Status", "Basis", row => ((Bestelbon)row).Status.Label()),
                Col("subtotaalExcl", "Subtotaal excl. btw", "Financieel", row => ((Bestelbon)row).TotaalExclBtw),
                Col("btwBedrag", "Btw-bedrag", "Financieel", row => ((Bestelbon)row).TotaalBtw),
                Col("totaalIncl", "Totaal incl. btw", "Financieel", row => ((Bestelbon)row).TotaalInclBtw),
                Col("kortingPct", "Korting %", "Financieel", row => ((Bestelbon)row).KortingPct, false),
                Col("voorschot", "Voorschot", "Financieel", row => ((Bestelbon)row).VoorschotBedrag, false),
                Col("btwVrijgesteld", "Btw vrijgesteld", "Financieel", row => ((Bestelbon)row).IsBtwVrijgesteld ? "Ja" : "Nee", false),
                Col("afhaalDatum", "Afhaaldatum", "Administratie", row => FormatNullableDate(((Bestelbon)row).AfhaalDatum), false),
                Col("opmerking", "Opmerking", "Administratie", row => ((Bestelbon)row).Opmerking ?? string.Empty, false)
            ],
            [
                new RelatieDefinitie(
                    "bestelbon-regels",
                    "Bestelbonlijnen",
                    "Voeg de lijnen van elke bestelbon als apart werkblad toe.",
                    "Bestelbonnen - Lijnen",
                    [
                        Col("bestelbonId", "Bestelbon-id", "Koppeling", row => ((BestelbonLijn)row).BestelbonId),
                        Col("omschrijving", "Omschrijving", "Basis", row => ((BestelbonLijn)row).Omschrijving),
                        Col("aantal", "Aantal", "Basis", row => ((BestelbonLijn)row).Aantal),
                        Col("eenheid", "Eenheid", "Basis", row => ((BestelbonLijn)row).Eenheid),
                        Col("prijsExcl", "Prijs excl.", "Financieel", row => ((BestelbonLijn)row).PrijsExcl),
                        Col("btwPct", "Btw %", "Financieel", row => ((BestelbonLijn)row).BtwPct),
                        Col("totaalExcl", "Totaal excl.", "Financieel", row => ((BestelbonLijn)row).TotaalExcl),
                        Col("totaalBtw", "Totaal btw", "Financieel", row => ((BestelbonLijn)row).TotaalBtw),
                        Col("totaalIncl", "Totaal incl.", "Financieel", row => ((BestelbonLijn)row).TotaalIncl)
                    ],
                    async (db, ids) => (await db.BestelbonLijnen.AsNoTracking()
                        .Where(x => ids.Contains(x.BestelbonId))
                        .OrderBy(x => x.BestelbonId)
                        .ThenBy(x => x.Sortering)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
