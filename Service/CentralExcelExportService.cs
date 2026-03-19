using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
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

    public async Task<ExportResult> ExportAsync(ExcelExportDataset dataset, string exportFolder)
    {
        if (string.IsNullOrWhiteSpace(exportFolder))
            throw new ArgumentException("Exportmap is verplicht.", nameof(exportFolder));

        var normalizedFolder = Path.GetFullPath(exportFolder);
        Directory.CreateDirectory(normalizedFolder);

        _logger?.LogInformation("Excel export gestart. Dataset={Dataset}, Folder={Folder}", dataset, normalizedFolder);

        await using var db = await _factory.CreateDbContextAsync();
        using var workbook = new XLWorkbook();

        switch (dataset)
        {
            case ExcelExportDataset.Klanten:
                await AddKlantenWorksheetAsync(db, workbook);
                break;
            case ExcelExportDataset.Lijsten:
                await AddLijstenWorksheetAsync(db, workbook);
                break;
            case ExcelExportDataset.Afwerkingen:
                await AddAfwerkingenWorksheetAsync(db, workbook);
                break;
            case ExcelExportDataset.Leveranciers:
                await AddLeveranciersWorksheetAsync(db, workbook);
                break;
            default:
                throw new InvalidOperationException($"Onbekende exportdataset: {dataset}.");
        }

        var filePath = Path.Combine(
            normalizedFolder,
            $"{GetFileNamePrefix(dataset)}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");

        workbook.SaveAs(filePath);

        _logger?.LogInformation("Excel export voltooid. Dataset={Dataset}, Bestand={Path}", dataset, filePath);

        return ExportResult.Ok(filePath, $"{GetDisplayName(dataset)} geëxporteerd.");
    }

    private static async Task AddKlantenWorksheetAsync(AppDbContext db, XLWorkbook workbook)
    {
        var klanten = await db.Klanten
            .AsNoTracking()
            .OrderBy(x => x.Achternaam)
            .ThenBy(x => x.Voornaam)
            .ToListAsync();

        var sheet = CreateWorksheet(workbook, "Klanten", [
            "Id", "Voornaam", "Achternaam", "Email", "Telefoon", "Straat", "Nummer",
            "Postcode", "Gemeente", "BtwNummer", "Opmerking"
        ]);

        var row = 2;
        foreach (var klant in klanten)
        {
            sheet.Cell(row, 1).Value = klant.Id;
            sheet.Cell(row, 2).Value = klant.Voornaam;
            sheet.Cell(row, 3).Value = klant.Achternaam;
            sheet.Cell(row, 4).Value = klant.Email ?? string.Empty;
            sheet.Cell(row, 5).Value = klant.Telefoon ?? string.Empty;
            sheet.Cell(row, 6).Value = klant.Straat ?? string.Empty;
            sheet.Cell(row, 7).Value = klant.Nummer ?? string.Empty;
            sheet.Cell(row, 8).Value = klant.Postcode ?? string.Empty;
            sheet.Cell(row, 9).Value = klant.Gemeente ?? string.Empty;
            sheet.Cell(row, 10).Value = klant.BtwNummer ?? string.Empty;
            sheet.Cell(row, 11).Value = klant.Opmerking ?? string.Empty;
            row++;
        }

        FinalizeWorksheet(sheet, row - 1, 11);
    }

    private static async Task AddLijstenWorksheetAsync(AppDbContext db, XLWorkbook workbook)
    {
        var lijsten = await db.TypeLijsten
            .AsNoTracking()
            .Include(x => x.Leverancier)
            .OrderBy(x => x.Artikelnummer)
            .ToListAsync();

        var sheet = CreateWorksheet(workbook, "Lijsten", [
            "Id", "Artikelnummer", "Levcode", "Leverancier", "BreedteCm", "Soort", "Dealer",
            "PrijsPerMeter", "WinstFactor", "AfvalPercentage", "VasteKost", "WerkMinuten",
            "VoorraadMeter", "GereserveerdeVoorraadMeter", "BeschikbareVoorraadMeter",
            "InBestellingMeter", "InventarisKost", "MinimumVoorraad", "HerbestelNiveauMeter",
            "LaatsteUpdate", "LaatsteVoorraadCheckOp", "Opmerking"
        ]);

        var row = 2;
        foreach (var lijst in lijsten)
        {
            sheet.Cell(row, 1).Value = lijst.Id;
            sheet.Cell(row, 2).Value = lijst.Artikelnummer;
            sheet.Cell(row, 3).Value = lijst.Levcode;
            sheet.Cell(row, 4).Value = lijst.Leverancier?.Naam ?? string.Empty;
            sheet.Cell(row, 5).Value = lijst.BreedteCm;
            sheet.Cell(row, 6).Value = lijst.Soort;
            sheet.Cell(row, 7).Value = lijst.IsDealer ? "Ja" : "Nee";
            sheet.Cell(row, 8).Value = lijst.PrijsPerMeter;
            sheet.Cell(row, 9).Value = lijst.WinstFactor?.ToString() ?? string.Empty;
            sheet.Cell(row, 10).Value = lijst.AfvalPercentage;
            sheet.Cell(row, 11).Value = lijst.VasteKost;
            sheet.Cell(row, 12).Value = lijst.WerkMinuten;
            sheet.Cell(row, 13).Value = lijst.VoorraadMeter;
            sheet.Cell(row, 14).Value = lijst.GereserveerdeVoorraadMeter;
            sheet.Cell(row, 15).Value = lijst.BeschikbareVoorraadMeter;
            sheet.Cell(row, 16).Value = lijst.InBestellingMeter;
            sheet.Cell(row, 17).Value = lijst.InventarisKost;
            sheet.Cell(row, 18).Value = lijst.MinimumVoorraad;
            sheet.Cell(row, 19).Value = lijst.HerbestelNiveauMeter?.ToString() ?? string.Empty;
            sheet.Cell(row, 20).Value = lijst.LaatsteUpdate;
            sheet.Cell(row, 21).Value = lijst.LaatsteVoorraadCheckOp?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
            sheet.Cell(row, 22).Value = lijst.Opmerking;
            row++;
        }

        sheet.Column(20).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        sheet.Column(21).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";

        FinalizeWorksheet(sheet, row - 1, 22);
    }

    private static async Task AddAfwerkingenWorksheetAsync(AppDbContext db, XLWorkbook workbook)
    {
        var opties = await db.AfwerkingsOpties
            .AsNoTracking()
            .Include(x => x.AfwerkingsGroep)
            .Include(x => x.Leverancier)
            .OrderBy(x => x.AfwerkingsGroep.Code)
            .ThenBy(x => x.Volgnummer)
            .ThenBy(x => x.Naam)
            .ToListAsync();

        var sheet = CreateWorksheet(workbook, "Afwerkingen", [
            "Id", "GroepCode", "GroepNaam", "Naam", "Volgnummer", "KostprijsPerM2",
            "WinstMarge", "AfvalPercentage", "VasteKost", "WerkMinuten", "LeverancierId",
            "Leverancier"
        ]);

        var row = 2;
        foreach (var optie in opties)
        {
            sheet.Cell(row, 1).Value = optie.Id;
            sheet.Cell(row, 2).Value = optie.AfwerkingsGroep.Code.ToString();
            sheet.Cell(row, 3).Value = optie.AfwerkingsGroep.Naam;
            sheet.Cell(row, 4).Value = optie.Naam;
            sheet.Cell(row, 5).Value = optie.Volgnummer.ToString();
            sheet.Cell(row, 6).Value = optie.KostprijsPerM2;
            sheet.Cell(row, 7).Value = optie.WinstMarge;
            sheet.Cell(row, 8).Value = optie.AfvalPercentage;
            sheet.Cell(row, 9).Value = optie.VasteKost;
            sheet.Cell(row, 10).Value = optie.WerkMinuten;
            sheet.Cell(row, 11).Value = optie.LeverancierId?.ToString() ?? string.Empty;
            sheet.Cell(row, 12).Value = optie.Leverancier?.Naam ?? string.Empty;
            row++;
        }

        FinalizeWorksheet(sheet, row - 1, 12);
    }

    private static async Task AddLeveranciersWorksheetAsync(AppDbContext db, XLWorkbook workbook)
    {
        var leveranciers = await db.Leveranciers
            .AsNoTracking()
            .OrderBy(x => x.Naam)
            .Select(x => new
            {
                x.Id,
                x.Naam,
                TypeLijstenCount = x.TypeLijsten.Count,
                BestellingenCount = x.Bestellingen.Count
            })
            .ToListAsync();

        var sheet = CreateWorksheet(workbook, "Leveranciers", [
            "Id", "Code", "AantalTypeLijsten", "AantalBestellingen"
        ]);

        var row = 2;
        foreach (var leverancier in leveranciers)
        {
            sheet.Cell(row, 1).Value = leverancier.Id;
            sheet.Cell(row, 2).Value = leverancier.Naam;
            sheet.Cell(row, 3).Value = leverancier.TypeLijstenCount;
            sheet.Cell(row, 4).Value = leverancier.BestellingenCount;
            row++;
        }

        FinalizeWorksheet(sheet, row - 1, 4);
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook, string sheetName, string[] headers)
    {
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var index = 0; index < headers.Length; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            cell.Value = headers[index];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#444A50");
            cell.Style.Font.FontColor = XLColor.White;
        }

        sheet.SheetView.FreezeRows(1);
        return sheet;
    }

    private static void FinalizeWorksheet(IXLWorksheet sheet, int lastDataRow, int columnCount)
    {
        var lastRow = Math.Max(lastDataRow, 1);
        var usedRange = sheet.Range(1, 1, lastRow, columnCount);
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.SetAutoFilter();
        sheet.Columns(1, columnCount).AdjustToContents();
    }

    private static string GetDisplayName(ExcelExportDataset dataset) => dataset switch
    {
        ExcelExportDataset.Klanten => "Klanten",
        ExcelExportDataset.Lijsten => "Lijsten",
        ExcelExportDataset.Afwerkingen => "Afwerkingen",
        ExcelExportDataset.Leveranciers => "Leveranciers",
        _ => dataset.ToString()
    };

    private static string GetFileNamePrefix(ExcelExportDataset dataset) => dataset switch
    {
        ExcelExportDataset.Klanten => "klanten-export",
        ExcelExportDataset.Lijsten => "lijsten-export",
        ExcelExportDataset.Afwerkingen => "afwerkingen-export",
        ExcelExportDataset.Leveranciers => "leveranciers-export",
        _ => "export"
    };
}
