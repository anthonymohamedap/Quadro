using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    private static DatasetDefinitie BuildOffertesDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Offertes,
            "Offertes",
            "Offertes met klantinfo en optionele regels of werkbon.",
            "Offertes",
            "offertes-export",
            async db => (await db.Offertes.AsNoTracking()
                .Include(x => x.Klant)
                .Include(x => x.WerkBon)
                .OrderByDescending(x => x.Datum)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((Offerte)row).Id,
            row => $"Offerte {((Offerte)row).Id}",
            row => $"{FormatKlant(((Offerte)row).Klant)} - {FormatDate(((Offerte)row).Datum)}",
            [
                Col("offerteId", "OfferteId", "Basis", row => ((Offerte)row).Id),
                Col("klant", "Klant", "Klant", row => FormatKlant(((Offerte)row).Klant)),
                Col("datum", "Datum", "Basis", row => FormatDate(((Offerte)row).Datum)),
                Col("status", "Status", "Basis", row => ((Offerte)row).Status.ToString()),
                Col("totaalIncl", "Totaal incl. btw", "Financieel", row => ((Offerte)row).TotaalInclBtw),
                Col("geplandeDatum", "Geplande datum", "Planning", row => FormatNullableDate(((Offerte)row).GeplandeDatum)),
                Col("deadline", "Deadline", "Planning", row => FormatNullableDate(((Offerte)row).DeadlineDatum)),
                Col("geschatteMinuten", "Geschatte minuten", "Planning", row => ((Offerte)row).GeschatteMinuten?.ToString() ?? string.Empty),
                Col("opmerking", "Opmerking", "Administratie", row => ((Offerte)row).Opmerking ?? string.Empty, false)
            ],
            [
                new RelatieDefinitie(
                    "offerte-regels",
                    "Offerte-regels",
                    "Voeg offerte-regels met lijst- en afwerkingskeuzes toe.",
                    "Offertes - Regels",
                    [
                        Col("offerteId", "OfferteId", "Koppeling", row => ((OfferteRegel)row).OfferteId),
                        Col("regelId", "RegelId", "Basis", row => ((OfferteRegel)row).Id),
                        Col("titel", "Titel", "Basis", row => ((OfferteRegel)row).Titel ?? string.Empty),
                        Col("aantal", "Aantal", "Basis", row => ((OfferteRegel)row).AantalStuks),
                        Col("breedte", "Breedte", "Afmetingen", row => ((OfferteRegel)row).BreedteCm),
                        Col("hoogte", "Hoogte", "Afmetingen", row => ((OfferteRegel)row).HoogteCm),
                        Col("typeLijst", "Type-lijst", "Keuzes", row => ((OfferteRegel)row).TypeLijst?.Artikelnummer ?? string.Empty),
                        Col("glas", "Glas", "Keuzes", row => ((OfferteRegel)row).Glas?.DisplayLabel ?? string.Empty),
                        Col("passe1", "Passe-partout 1", "Keuzes", row => ((OfferteRegel)row).PassePartout1?.DisplayLabel ?? string.Empty),
                        Col("passe2", "Passe-partout 2", "Keuzes", row => ((OfferteRegel)row).PassePartout2?.DisplayLabel ?? string.Empty),
                        Col("diepte", "Dieptekern", "Keuzes", row => ((OfferteRegel)row).DiepteKern?.DisplayLabel ?? string.Empty),
                        Col("opkleven", "Opkleven", "Keuzes", row => ((OfferteRegel)row).Opkleven?.DisplayLabel ?? string.Empty),
                        Col("rug", "Rug", "Keuzes", row => ((OfferteRegel)row).Rug?.DisplayLabel ?? string.Empty),
                        Col("totaalIncl", "Totaal incl. btw", "Financieel", row => ((OfferteRegel)row).TotaalInclBtw)
                    ],
                    async (db, ids) => (await db.OfferteRegels.AsNoTracking()
                        .Include(x => x.TypeLijst)
                        .Include(x => x.Glas)
                        .Include(x => x.PassePartout1)
                        .Include(x => x.PassePartout2)
                        .Include(x => x.DiepteKern)
                        .Include(x => x.Opkleven)
                        .Include(x => x.Rug)
                        .Where(x => ids.Contains(x.OfferteId))
                        .OrderBy(x => x.OfferteId)
                        .ThenBy(x => x.Id)
                        .ToListAsync()).Cast<object>().ToList()),
                new RelatieDefinitie(
                    "werkbon",
                    "Werkbon",
                    "Voeg gekoppelde werkbonnen toe als extra werkblad.",
                    "Offertes - Werkbon",
                    [
                        Col("offerteId", "OfferteId", "Koppeling", row => ((WerkBon)row).OfferteId),
                        Col("werkBonId", "WerkBonId", "Basis", row => ((WerkBon)row).Id),
                        Col("status", "Status", "Basis", row => ((WerkBon)row).Status.ToString()),
                        Col("afhaalDatum", "Afhaaldatum", "Planning", row => FormatNullableDate(((WerkBon)row).AfhaalDatum)),
                        Col("aantalTaken", "Aantal taken", "Samenvatting", row => ((WerkBon)row).Taken.Count),
                        Col("totaalPrijsIncl", "Totaal incl.", "Financieel", row => ((WerkBon)row).TotaalPrijsIncl)
                    ],
                    async (db, ids) => (await db.WerkBonnen.AsNoTracking()
                        .Include(x => x.Taken)
                        .Where(x => ids.Contains(x.OfferteId))
                        .OrderByDescending(x => x.AangemaaktOp)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
