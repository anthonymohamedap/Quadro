using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class CentralExcelExportService : ICentralExcelExportService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<CentralExcelExportService>? _logger;

    public CentralExcelExportService(
        IDbContextFactory<AppDbContext> factory,
        ILogger<CentralExcelExportService>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<ExportDatasetOptie>> GetBeschikbareDatasetsAsync()
        => Task.FromResult<IReadOnlyList<ExportDatasetOptie>>(GetDatasetDefinitions()
            .Select(x => new ExportDatasetOptie
            {
                Dataset = x.Dataset,
                Naam = x.Naam,
                Beschrijving = x.Beschrijving
            })
            .ToList());

    public Task<IReadOnlyList<ExportPresetOptie>> GetStandaardPresetsAsync()
        => Task.FromResult<IReadOnlyList<ExportPresetOptie>>(GetPresetDefinitions()
            .Select(x => new ExportPresetOptie
            {
                Sleutel = x.Sleutel,
                Naam = x.Naam,
                Beschrijving = x.Beschrijving,
                Dataset = x.Dataset
            })
            .ToList());

    public async Task<ExportConfiguratie> MaakConfiguratieAsync(ExcelExportDataset dataset, string? presetSleutel = null)
    {
        var definitie = GetDatasetDefinition(dataset);
        await using var db = await _factory.CreateDbContextAsync();
        var entiteitRijen = await definitie.LaadtRijenAsync(db);
        var configuratie = new ExportConfiguratie
        {
            Dataset = dataset,
            Titel = definitie.Naam,
            Beschrijving = definitie.Beschrijving,
            Entiteiten = new ObservableCollection<ExportEntiteitOptie>(
                entiteitRijen.Select(rij => new ExportEntiteitOptie
                {
                    Id = definitie.GetId(rij),
                    Label = definitie.GetLabel(rij),
                    Beschrijving = definitie.GetSubLabel(rij)
                })),
            Kolommen = new ObservableCollection<ExportKolomOptie>(
                definitie.Kolommen.Select(k => new ExportKolomOptie
                {
                    Sleutel = k.Sleutel,
                    Label = k.Label,
                    Groep = k.Groep,
                    IsGeselecteerd = k.StandaardGeselecteerd
                })),
            Relaties = new ObservableCollection<ExportRelatieOptie>(
                definitie.Relaties.Select(r => new ExportRelatieOptie
                {
                    Sleutel = r.Sleutel,
                    Label = r.Label,
                    Beschrijving = r.Beschrijving,
                    WerkbladNaam = r.WerkbladNaam,
                    IsGeselecteerd = false,
                    Kolommen = new ObservableCollection<ExportKolomOptie>(
                        r.Kolommen.Select(k => new ExportKolomOptie
                        {
                            Sleutel = k.Sleutel,
                            Label = k.Label,
                            Groep = k.Groep,
                            IsGeselecteerd = k.StandaardGeselecteerd
                        }))
                }))
        };

        if (string.IsNullOrWhiteSpace(presetSleutel))
            return configuratie;

        var preset = GetPresetDefinitions().FirstOrDefault(x =>
            string.Equals(x.Sleutel, presetSleutel, StringComparison.OrdinalIgnoreCase)
            && x.Dataset == dataset);

        if (preset is null)
            return configuratie;

        foreach (var kolom in configuratie.Kolommen)
            kolom.IsGeselecteerd = preset.KolomSleutels.Contains(kolom.Sleutel, StringComparer.OrdinalIgnoreCase);

        foreach (var relatie in configuratie.Relaties)
        {
            if (!preset.RelatieKolommen.TryGetValue(relatie.Sleutel, out var relatieKolommen))
            {
                relatie.IsGeselecteerd = false;
                continue;
            }

            relatie.IsGeselecteerd = true;
            foreach (var kolom in relatie.Kolommen)
                kolom.IsGeselecteerd = relatieKolommen.Contains(kolom.Sleutel, StringComparer.OrdinalIgnoreCase);
        }

        return configuratie;
    }

    public async Task<ExportResult> ExportAsync(ExcelExportDataset dataset, string exportFolder)
    {
        var presetSleutel = GetStandaardPresetSleutel(dataset);
        var configuratie = await MaakConfiguratieAsync(dataset, presetSleutel);
        var aanvraag = BuildRequest(configuratie, presetSleutel);
        return await ExportAsync(aanvraag, exportFolder);
    }

    public async Task<ExportResult> ExportAsync(ExportAanvraag aanvraag, string exportFolder)
    {
        if (string.IsNullOrWhiteSpace(exportFolder))
            throw new ArgumentException("Exportmap is verplicht.", nameof(exportFolder));

        var definitie = GetDatasetDefinition(aanvraag.Dataset);
        var geselecteerdeKolommen = definitie.Kolommen
            .Where(k => aanvraag.KolomSleutels.Contains(k.Sleutel, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (geselecteerdeKolommen.Count == 0)
            throw new InvalidOperationException("Selecteer minstens één veld voor de hoofdexport.");

        var normalizedFolder = Path.GetFullPath(exportFolder);
        Directory.CreateDirectory(normalizedFolder);

        _logger?.LogInformation("Configurable export gestart. Dataset={Dataset}, Folder={Folder}", aanvraag.Dataset, normalizedFolder);

        await using var db = await _factory.CreateDbContextAsync();
        using var workbook = new XLWorkbook();

        var hoofdRijen = await definitie.LaadtRijenAsync(db);
        if (aanvraag.EntiteitIds.Count > 0)
        {
            var geselecteerdeIds = aanvraag.EntiteitIds.ToHashSet();
            hoofdRijen = hoofdRijen
                .Where(rij => geselecteerdeIds.Contains(definitie.GetId(rij)))
                .ToList();
        }

        VoegWerkbladToe(workbook, definitie.WerkbladNaam, geselecteerdeKolommen, hoofdRijen);

        var hoofdIds = hoofdRijen.Select(definitie.GetId).Distinct().ToList();
        foreach (var relatieAanvraag in aanvraag.Relaties)
        {
            var relatieDefinitie = definitie.Relaties.FirstOrDefault(r =>
                string.Equals(r.Sleutel, relatieAanvraag.Sleutel, StringComparison.OrdinalIgnoreCase));

            if (relatieDefinitie is null)
                continue;

            var relatieKolommen = relatieDefinitie.Kolommen
                .Where(k => relatieAanvraag.KolomSleutels.Count == 0
                    || relatieAanvraag.KolomSleutels.Contains(k.Sleutel, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (relatieKolommen.Count == 0)
                relatieKolommen = relatieDefinitie.Kolommen.Where(k => k.StandaardGeselecteerd).ToList();

            if (relatieKolommen.Count == 0)
                continue;

            var relatieRijen = await relatieDefinitie.LaadtRijenAsync(db, hoofdIds);
            VoegWerkbladToe(workbook, relatieDefinitie.WerkbladNaam, relatieKolommen, relatieRijen);
        }

        var filePath = Path.Combine(
            normalizedFolder,
            $"{definitie.BestandPrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");

        workbook.SaveAs(filePath);

        return ExportResult.Ok(
            filePath,
            $"{definitie.Naam} geëxporteerd met {aanvraag.Relaties.Count} relatie(s).");
    }

    private static ExportAanvraag BuildRequest(ExportConfiguratie configuratie, string? presetSleutel)
    {
        return new ExportAanvraag
        {
            Dataset = configuratie.Dataset,
            PresetSleutel = presetSleutel,
            EntiteitIds = configuratie.Entiteiten.Where(e => e.IsGeselecteerd).Select(e => e.Id).ToList(),
            KolomSleutels = configuratie.Kolommen.Where(k => k.IsGeselecteerd).Select(k => k.Sleutel).ToList(),
            Relaties = configuratie.Relaties
                .Where(r => r.IsGeselecteerd)
                .Select(r => new ExportRelatieAanvraag
                {
                    Sleutel = r.Sleutel,
                    KolomSleutels = r.Kolommen.Where(k => k.IsGeselecteerd).Select(k => k.Sleutel).ToList()
                })
                .ToList()
        };
    }

    private static void VoegWerkbladToe(
        XLWorkbook workbook,
        string werkbladNaam,
        IReadOnlyList<ExportKolomDefinitie> kolommen,
        IReadOnlyList<object> rijen)
    {
        var sheet = workbook.Worksheets.Add(MaakWerkbladNaamVeilig(workbook, werkbladNaam));

        for (int index = 0; index < kolommen.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = kolommen[index].Label;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#444A50");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var rowIndex = 2;
        foreach (var rij in rijen)
        {
            for (int colIndex = 0; colIndex < kolommen.Count; colIndex++)
            {
                sheet.Cell(rowIndex, colIndex + 1).Value = MaakCelWaarde(kolommen[colIndex].Waarde(rij));
            }
            rowIndex++;
        }

        var lastRow = Math.Max(rowIndex - 1, 1);
        var usedRange = sheet.Range(1, 1, lastRow, kolommen.Count);
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, kolommen.Count).AdjustToContents();
    }

    private static string MaakWerkbladNaamVeilig(XLWorkbook workbook, string basisNaam)
    {
        var naam = basisNaam.Length <= 31 ? basisNaam : basisNaam[..31];
        var candidate = naam;
        var teller = 2;

        while (workbook.Worksheets.Any(w => string.Equals(w.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            var suffix = $" {teller}";
            var maxBasis = Math.Max(1, 31 - suffix.Length);
            candidate = $"{naam[..Math.Min(naam.Length, maxBasis)]}{suffix}";
            teller++;
        }

        return candidate;
    }

    private static XLCellValue MaakCelWaarde(object? waarde) => waarde switch
    {
        null => string.Empty,
        XLCellValue cellValue => cellValue,
        string text => text,
        int number => number,
        long number => number,
        short number => number,
        double number => number,
        float number => number,
        decimal number => number,
        bool boolean => boolean,
        DateTime dateTime => dateTime,
        DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
        _ => waarde.ToString() ?? string.Empty
    };

    private static string GetStandaardPresetSleutel(ExcelExportDataset dataset) => dataset switch
    {
        ExcelExportDataset.Klanten => "klanten-standaard",
        ExcelExportDataset.Lijsten => "voorraadoverzicht",
        ExcelExportDataset.Afwerkingen => "afwerkingen-families",
        ExcelExportDataset.Leveranciers => "leverancier-voorraadbundel",
        ExcelExportDataset.Offertes => "offertebundel",
        _ => throw new InvalidOperationException($"Onbekende exportdataset: {dataset}.")
    };

    private static IReadOnlyList<ExportPresetDefinitie> GetPresetDefinitions() =>
    [
        new("klanten-standaard", "Klanten standaard", "Basisklantgegevens zonder relaties.", ExcelExportDataset.Klanten,
            ["id", "voornaam", "achternaam", "email", "telefoon", "gemeente"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)),
        new("voorraadoverzicht", "Voorraadoverzicht", "Praktisch overzicht van artikelen, leverancier en voorraadstatus.", ExcelExportDataset.Lijsten,
            ["artikelnummer", "levcode", "leverancier", "breedte", "soort", "voorraad", "gereserveerd", "beschikbaar", "inbestelling", "minimum", "herbestel", "status"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["voorraad-alerts"] = ["typeLijstId", "artikelnummer", "alertType", "status", "bericht", "aangemaaktOp"]
            }),
        new("afwerkingen-families", "Afwerkingen per familie", "Familie, kleur en kostparameters voor afwerkingen.", ExcelExportDataset.Afwerkingen,
            ["groepCode", "groepNaam", "familie", "kleur", "naam", "leverancier", "kostprijs", "winstmarge", "afval", "vasteKost", "werkMinuten"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)),
        new("leverancier-voorraadbundel", "Leverancier met lijsten en bestellingen", "Leveranciers met hun lijsten en bestellingen in aparte tabbladen.", ExcelExportDataset.Leveranciers,
            ["id", "code", "aantalLijsten", "aantalAfwerkingen", "aantalBestellingen"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["type-lijsten"] = ["leverancierId", "leverancier", "artikelnummer", "levcode", "soort", "beschikbaar", "inbestelling", "status"],
                ["bestellingen"] = ["leverancierId", "leverancier", "bestelNummer", "status", "besteldOp", "verwachteLeverdatum", "aantalLijnen"]
            }),
        new("klantoverzicht", "Klanten met offertes", "Klantgegevens met gekoppelde offertes in een extra werkblad.", ExcelExportDataset.Klanten,
            ["id", "voornaam", "achternaam", "email", "telefoon", "gemeente", "btwNummer"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["offertes"] = ["klantId", "klant", "offerteId", "datum", "status", "totaalIncl", "geplandeDatum", "deadline"]
            }),
        new("offertebundel", "Offertes met regels en werkbon", "Offertekop, regels en gekoppelde werkbon in één export.", ExcelExportDataset.Offertes,
            ["offerteId", "klant", "datum", "status", "totaalIncl", "geplandeDatum", "deadline", "geschatteMinuten"],
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["offerte-regels"] = ["offerteId", "regelId", "titel", "aantal", "breedte", "hoogte", "typeLijst", "glas", "passe1", "passe2", "diepte", "opkleven", "rug", "totaalIncl"],
                ["werkbon"] = ["offerteId", "werkBonId", "status", "afhaalDatum", "aantalTaken", "totaalPrijsIncl"]
            })
    ];

    private static DatasetDefinitie GetDatasetDefinition(ExcelExportDataset dataset)
        => GetDatasetDefinitions().First(x => x.Dataset == dataset);

    private static IReadOnlyList<DatasetDefinitie> GetDatasetDefinitions() =>
    [
        BuildKlantenDefinition(),
        BuildLijstenDefinition(),
        BuildAfwerkingenDefinition(),
        BuildLeveranciersDefinition(),
        BuildOffertesDefinition()
    ];

    private static DatasetDefinitie BuildKlantenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Klanten,
            "Klanten",
            "Export van klanten met contactinformatie en optionele offerte-relatie.",
            "Klanten",
             "klanten-export",
             async db => (await db.Klanten.AsNoTracking().OrderBy(x => x.Achternaam).ThenBy(x => x.Voornaam).ToListAsync()).Cast<object>().ToList(),
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

    private static DatasetDefinitie BuildAfwerkingenDefinition()
    {
        return new DatasetDefinitie(
            ExcelExportDataset.Afwerkingen,
            "Afwerkingen",
            "Export van afwerkingsfamilies en kleurvarianten.",
            "Afwerkingen",
             "afwerkingen-export",
             async db => (await db.AfwerkingsOpties.AsNoTracking()
                 .Include(x => x.AfwerkingsGroep)
                 .Include(x => x.Leverancier)
                .OrderBy(x => x.AfwerkingsGroep.Code)
                .ThenBy(x => x.Volgnummer)
                 .ThenBy(x => x.Kleur)
                 .ToListAsync()).Cast<object>().ToList(),
             row => ((AfwerkingsOptie)row).Id,
             row => $"{((AfwerkingsOptie)row).AfwerkingsGroep.Naam} - {((AfwerkingsOptie)row).Naam}",
             row => ((AfwerkingsOptie)row).Kleur,
             [
                Col("id", "Id", "Basis", row => ((AfwerkingsOptie)row).Id, false),
                Col("groepCode", "Groepcode", "Basis", row => ((AfwerkingsOptie)row).AfwerkingsGroep.Code.ToString()),
                Col("groepNaam", "Groep", "Basis", row => ((AfwerkingsOptie)row).AfwerkingsGroep.Naam),
                Col("familie", "Familie", "Familie", row => ((AfwerkingsOptie)row).Volgnummer.ToString()),
                Col("kleur", "Kleur", "Familie", row => ((AfwerkingsOptie)row).Kleur),
                Col("naam", "Naam", "Familie", row => ((AfwerkingsOptie)row).Naam),
                Col("leverancier", "Leverancier", "Leverancier", row => ((AfwerkingsOptie)row).Leverancier?.Naam ?? string.Empty),
                Col("kostprijs", "Kostprijs per m²", "Prijs", row => ((AfwerkingsOptie)row).KostprijsPerM2),
                Col("winstmarge", "Winstmarge", "Prijs", row => ((AfwerkingsOptie)row).WinstMarge),
                Col("afval", "Afvalpercentage", "Prijs", row => ((AfwerkingsOptie)row).AfvalPercentage),
                Col("vasteKost", "Vaste kost", "Prijs", row => ((AfwerkingsOptie)row).VasteKost),
                Col("werkMinuten", "Werkminuten", "Prijs", row => ((AfwerkingsOptie)row).WerkMinuten)
            ],
            []);
    }

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
                    .GroupBy(x => x.LeverancierId)
                    .Select(g => new { LeverancierId = g.Key, Aantal = g.Count() })
                    .ToDictionaryAsync(x => x.LeverancierId, x => x.Aantal);

                var bestellingenPerLeverancier = await db.LeverancierBestellingen.AsNoTracking()
                    .GroupBy(x => x.LeverancierId)
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
                        .Where(x => ids.Contains(x.LeverancierId))
                        .OrderBy(x => x.Leverancier.Naam)
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
                        .Where(x => ids.Contains(x.LeverancierId))
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

    private static ExportKolomDefinitie Col(string sleutel, string label, string groep, Func<object, object?> waarde, bool standaardGeselecteerd = true)
        => new(sleutel, label, groep, waarde, standaardGeselecteerd);

    private static string FormatKlant(Klant? klant)
        => klant is null ? string.Empty : $"{klant.Achternaam} {klant.Voornaam}".Trim();

    private static string FormatDate(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
    private static string FormatDateTime(DateTime value) => value.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    private static string FormatNullableDate(DateTime? value) => value.HasValue ? FormatDate(value.Value) : string.Empty;

    private sealed record ExportKolomDefinitie(
        string Sleutel,
        string Label,
        string Groep,
        Func<object, object?> Waarde,
        bool StandaardGeselecteerd);

    private sealed record RelatieDefinitie(
        string Sleutel,
        string Label,
        string Beschrijving,
        string WerkbladNaam,
        IReadOnlyList<ExportKolomDefinitie> Kolommen,
        Func<AppDbContext, IReadOnlyCollection<int>, Task<List<object>>> LaadtRijenAsync);

    private sealed record DatasetDefinitie(
        ExcelExportDataset Dataset,
        string Naam,
        string Beschrijving,
        string WerkbladNaam,
        string BestandPrefix,
        Func<AppDbContext, Task<List<object>>> LaadtRijenAsync,
        Func<object, int> GetId,
        Func<object, string> GetLabel,
        Func<object, string> GetSubLabel,
        IReadOnlyList<ExportKolomDefinitie> Kolommen,
        IReadOnlyList<RelatieDefinitie> Relaties);

    private sealed record ExportPresetDefinitie(
        string Sleutel,
        string Naam,
        string Beschrijving,
        ExcelExportDataset Dataset,
        IReadOnlyList<string> KolomSleutels,
        IReadOnlyDictionary<string, IReadOnlyList<string>> RelatieKolommen);

    private sealed record LeverancierExportRij(
        int Id,
        string Code,
        int AantalLijsten,
        int AantalAfwerkingen,
        int AantalBestellingen);
}
