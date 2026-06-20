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

public sealed partial class CentralExcelExportService : ICentralExcelExportService
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
