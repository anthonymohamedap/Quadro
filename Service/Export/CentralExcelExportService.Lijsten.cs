using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    private static DatasetDefinitie BuildLijstenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Lijsten,
            "Lijsten",
            "Export van type-lijsten met voorraad- en leveranciersinformatie.",
            "Lijsten",
            "lijsten-export",
            async db => (await db.TypeLijsten.AsNoTracking()
                .Include(x => x.Leverancier)
                .OrderBy(x => x.Artikelnummer)
                .ToListAsync()).Cast<object>().ToList(),
            row => ((TypeLijst)row).Id,
            row => ((TypeLijst)row).Artikelnummer,
            row => ((TypeLijst)row).Leverancier?.Naam ?? ((TypeLijst)row).Soort,
            [
                Col("id", "Id", "Basis", row => ((TypeLijst)row).Id, false),
                Col("artikelnummer", "Artikelnummer", "Basis", row => ((TypeLijst)row).Artikelnummer),
                Col("levcode", "Levcode", "Basis", row => ((TypeLijst)row).Levcode),
                Col("leverancier", "Leverancier", "Leverancier", row => ((TypeLijst)row).Leverancier?.Naam ?? string.Empty),
                Col("breedte", "Breedte (cm)", "Basis", row => ((TypeLijst)row).BreedteCm),
                Col("soort", "Soort", "Basis", row => ((TypeLijst)row).Soort),
                Col("dealer", "Dealer", "Basis", row => ((TypeLijst)row).IsDealer ? "Ja" : "Nee", false),
                Col("prijs", "Prijs per meter", "Prijs", row => ((TypeLijst)row).PrijsPerMeter),
                Col("winst", "Winstfactor", "Prijs", row => ((TypeLijst)row).WinstFactor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, false),
                Col("afval", "Afvalpercentage", "Prijs", row => ((TypeLijst)row).AfvalPercentage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, false),
                Col("vasteKost", "Vaste kost", "Prijs", row => ((TypeLijst)row).VasteKost, false),
                Col("werkMinuten", "Werkminuten", "Prijs", row => ((TypeLijst)row).WerkMinuten, false),
                Col("voorraad", "Voorraad", "Voorraad", row => ((TypeLijst)row).VoorraadMeter),
                Col("gereserveerd", "Gereserveerd", "Voorraad", row => ((TypeLijst)row).GereserveerdeVoorraadMeter),
                Col("beschikbaar", "Beschikbaar", "Voorraad", row => ((TypeLijst)row).BeschikbareVoorraadMeter),
                Col("inbestelling", "In bestelling", "Voorraad", row => ((TypeLijst)row).InBestellingMeter),
                Col("minimum", "Minimum voorraad", "Voorraad", row => ((TypeLijst)row).MinimumVoorraad),
                Col("herbestel", "Herbestelniveau", "Voorraad", row => ((TypeLijst)row).EffectiefHerbestelNiveauMeter),
                Col("status", "Voorraadstatus", "Voorraad", row => ((TypeLijst)row).VoorraadStatusLabel),
                Col("laatsteUpdate", "Laatste update", "Historiek", row => FormatDateTime(((TypeLijst)row).LaatsteUpdate), false),
                Col("opmerking", "Opmerking", "Historiek", row => ((TypeLijst)row).Opmerking ?? string.Empty, false)
            ],
            [
                new RelatieDefinitie(
                    "voorraad-alerts",
                    "Voorraadalerts",
                    "Exporteer open of bestaande alerts van de geselecteerde lijsten.",
                    "Lijsten - Alerts",
                    [
                        Col("typeLijstId", "TypeLijstId", "Koppeling", row => ((VoorraadAlert)row).TypeLijstId?.ToString() ?? string.Empty),
                        Col("artikelnummer", "Artikelnummer", "Koppeling", row => ((VoorraadAlert)row).TypeLijst?.Artikelnummer ?? string.Empty),
                        Col("alertType", "Alerttype", "Basis", row => ((VoorraadAlert)row).AlertType.ToString()),
                        Col("status", "Status", "Basis", row => ((VoorraadAlert)row).Status.ToString()),
                        Col("bericht", "Bericht", "Basis", row => ((VoorraadAlert)row).Bericht),
                        Col("aangemaaktOp", "Aangemaakt op", "Historiek", row => FormatDateTime(((VoorraadAlert)row).AangemaaktOp))
                    ],
                    async (db, ids) => (await db.VoorraadAlerts.AsNoTracking()
                        .Include(x => x.TypeLijst)
                        .Where(x => x.TypeLijstId.HasValue && ids.Contains(x.TypeLijstId.Value))
                        .OrderByDescending(x => x.AangemaaktOp)
                        .ToListAsync()).Cast<object>().ToList()),
                new RelatieDefinitie(
                    "voorraad-mutaties",
                    "Voorraadmutaties",
                    "Exporteer voorraadmutaties van de geselecteerde lijsten.",
                    "Lijsten - Mutaties",
                    [
                        Col("typeLijstId", "TypeLijstId", "Koppeling", row => ((VoorraadMutatie)row).TypeLijstId),
                        Col("artikelnummer", "Artikelnummer", "Koppeling", row => ((VoorraadMutatie)row).TypeLijst?.Artikelnummer ?? string.Empty),
                        Col("mutatieType", "Mutatietype", "Basis", row => ((VoorraadMutatie)row).MutatieType.ToString()),
                        Col("aantalMeter", "Aantal meter", "Basis", row => ((VoorraadMutatie)row).AantalMeter),
                        Col("mutatieDatum", "Mutatiedatum", "Historiek", row => FormatDateTime(((VoorraadMutatie)row).MutatieDatum)),
                        Col("referentie", "Referentie", "Historiek", row => ((VoorraadMutatie)row).Referentie ?? string.Empty),
                        Col("opmerking", "Opmerking", "Historiek", row => ((VoorraadMutatie)row).Opmerking ?? string.Empty, false)
                    ],
                    async (db, ids) => (await db.VoorraadMutaties.AsNoTracking()
                        .Include(x => x.TypeLijst)
                        .Where(x => ids.Contains(x.TypeLijstId))
                        .OrderByDescending(x => x.MutatieDatum)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
