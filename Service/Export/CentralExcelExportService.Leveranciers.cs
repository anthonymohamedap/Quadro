using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed partial class CentralExcelExportService
{
    private static DatasetDefinitie BuildLeveranciersDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Leveranciers,
            "Leveranciers",
            "Leveranciers met gekoppelde stock- en bestelgegevens.",
            "Leveranciers",
            "leveranciers-export",
            async db =>
            {
                var leveranciers = await db.Leveranciers.AsNoTracking()
                    .OrderBy(x => x.Naam)
                    .ToListAsync();

                var lijstenPerLeverancier = await db.TypeLijsten.AsNoTracking()
                    .Where(x => x.LeverancierId.HasValue)
                    .GroupBy(x => x.LeverancierId!.Value)
                    .Select(g => new { LeverancierId = g.Key, Aantal = g.Count() })
                    .ToDictionaryAsync(x => x.LeverancierId, x => x.Aantal);

                var bestellingenPerLeverancier = await db.LeverancierBestellingen.AsNoTracking()
                    .Where(x => x.LeverancierId.HasValue)
                    .GroupBy(x => x.LeverancierId!.Value)
                    .Select(g => new { LeverancierId = g.Key, Aantal = g.Count() })
                    .ToDictionaryAsync(x => x.LeverancierId, x => x.Aantal);

                var afwerkingenPerLeverancier = await db.AfwerkingsOpties.AsNoTracking()
                    .Where(x => x.LeverancierId.HasValue)
                    .GroupBy(x => x.LeverancierId!.Value)
                    .Select(g => new { LeverancierId = g.Key, Aantal = g.Count() })
                    .ToDictionaryAsync(x => x.LeverancierId, x => x.Aantal);

                return leveranciers
                    .Select(x => new LeverancierExportRij(
                        x.Id,
                        x.Naam,
                        lijstenPerLeverancier.GetValueOrDefault(x.Id),
                        afwerkingenPerLeverancier.GetValueOrDefault(x.Id),
                        bestellingenPerLeverancier.GetValueOrDefault(x.Id)))
                    .Cast<object>()
                    .ToList();
            },
            row => ((LeverancierExportRij)row).Id,
            row => ((LeverancierExportRij)row).Code,
            row => $"{((LeverancierExportRij)row).AantalLijsten} lijsten, {((LeverancierExportRij)row).AantalBestellingen} bestellingen",
            [
                Col("id", "Id", "Basis", row => ((LeverancierExportRij)row).Id, false),
                Col("code", "Code", "Basis", row => ((LeverancierExportRij)row).Code),
                Col("aantalLijsten", "Aantal lijsten", "Samenvatting", row => ((LeverancierExportRij)row).AantalLijsten),
                Col("aantalAfwerkingen", "Aantal afwerkingen", "Samenvatting", row => ((LeverancierExportRij)row).AantalAfwerkingen),
                Col("aantalBestellingen", "Aantal bestellingen", "Samenvatting", row => ((LeverancierExportRij)row).AantalBestellingen)
            ],
            [
                new RelatieDefinitie(
                    "type-lijsten",
                    "Type-lijsten",
                    "Voeg alle type-lijsten per leverancier toe.",
                    "Leveranciers - Lijsten",
                    [
                        Col("leverancierId", "LeverancierId", "Koppeling", row => ((TypeLijst)row).LeverancierId),
                        Col("leverancier", "Leverancier", "Koppeling", row => ((TypeLijst)row).Leverancier?.Naam ?? string.Empty),
                        Col("artikelnummer", "Artikelnummer", "Basis", row => ((TypeLijst)row).Artikelnummer),
                        Col("levcode", "Levcode", "Basis", row => ((TypeLijst)row).Levcode),
                        Col("soort", "Soort", "Basis", row => ((TypeLijst)row).Soort),
                        Col("beschikbaar", "Beschikbaar", "Voorraad", row => ((TypeLijst)row).BeschikbareVoorraadMeter),
                        Col("inbestelling", "In bestelling", "Voorraad", row => ((TypeLijst)row).InBestellingMeter),
                        Col("status", "Voorraadstatus", "Voorraad", row => ((TypeLijst)row).VoorraadStatusLabel)
                    ],
                    async (db, ids) => (await db.TypeLijsten.AsNoTracking()
                        .Include(x => x.Leverancier)
                        .Where(x => x.LeverancierId.HasValue && ids.Contains(x.LeverancierId.Value))
                        .OrderBy(x => x.Leverancier!.Naam)
                        .ThenBy(x => x.Artikelnummer)
                        .ToListAsync()).Cast<object>().ToList()),
                new RelatieDefinitie(
                    "bestellingen",
                    "Bestellingen",
                    "Voeg alle bestellingen per leverancier toe.",
                    "Leveranciers - Bestellingen",
                    [
                        Col("leverancierId", "LeverancierId", "Koppeling", row => ((LeverancierBestelling)row).LeverancierId),
                        Col("leverancier", "Leverancier", "Koppeling", row => ((LeverancierBestelling)row).Leverancier?.Naam ?? string.Empty),
                        Col("bestelNummer", "Bestelnummer", "Basis", row => ((LeverancierBestelling)row).BestelNummer),
                        Col("status", "Status", "Basis", row => ((LeverancierBestelling)row).Status.ToString()),
                        Col("besteldOp", "Besteld op", "Timing", row => FormatDateTime(((LeverancierBestelling)row).BesteldOp)),
                        Col("verwachteLeverdatum", "Verwachte leverdatum", "Timing", row => FormatNullableDate(((LeverancierBestelling)row).VerwachteLeverdatum)),
                        Col("aantalLijnen", "Aantal lijnen", "Samenvatting", row => ((LeverancierBestelling)row).Lijnen.Count)
                    ],
                    async (db, ids) => (await db.LeverancierBestellingen.AsNoTracking()
                        .Include(x => x.Leverancier)
                        .Include(x => x.Lijnen)
                        .Where(x => x.LeverancierId != null && ids.Contains(x.LeverancierId.Value))
                        .OrderByDescending(x => x.BesteldOp)
                        .ToListAsync()).Cast<object>().ToList()),
                new RelatieDefinitie(
                    "afwerkingen",
                    "Afwerkingen",
                    "Voeg afwerkingsopties per leverancier toe.",
                    "Leveranciers - Afwerkingen",
                    [
                        Col("leverancierId", "LeverancierId", "Koppeling", row => ((AfwerkingsOptie)row).LeverancierId?.ToString() ?? string.Empty),
                        Col("leverancier", "Leverancier", "Koppeling", row => ((AfwerkingsOptie)row).Leverancier?.Naam ?? string.Empty),
                        Col("groep", "Groep", "Basis", row => ((AfwerkingsOptie)row).AfwerkingsGroep.Naam),
                        Col("familie", "Familie", "Familie", row => ((AfwerkingsOptie)row).Volgnummer.ToString()),
                        Col("kleur", "Kleur", "Familie", row => ((AfwerkingsOptie)row).Kleur),
                        Col("naam", "Naam", "Familie", row => ((AfwerkingsOptie)row).Naam)
                    ],
                    async (db, ids) => (await db.AfwerkingsOpties.AsNoTracking()
                        .Include(x => x.AfwerkingsGroep)
                        .Include(x => x.Leverancier)
                        .Where(x => x.LeverancierId.HasValue && ids.Contains(x.LeverancierId.Value))
                        .OrderBy(x => x.Leverancier!.Naam)
                        .ThenBy(x => x.AfwerkingsGroep.Naam)
                        .ThenBy(x => x.Volgnummer)
                        .ThenBy(x => x.Kleur)
                        .ToListAsync()).Cast<object>().ToList())
            ]);
    }
}
