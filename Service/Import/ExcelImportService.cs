using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Services.Import;

public sealed class ExcelImportService : IExcelImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private readonly ICrudValidator<TypeLijst> _validator;

    public ExcelImportService(IDbContextFactory<AppDbContext> dbFactory, ICrudValidator<TypeLijst> validator)
    {
        _dbFactory = dbFactory;
        _validator = validator;
    }

    public async Task<ImportPreviewResult> ReadTypeLijstenPreviewAsync(string filePath)
    {
        var rows = new List<TypeLijstPreviewRow>();
        var issues = new List<ImportIssue>();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            issues.Add(new ImportIssue(0, "File", "Bestandspad is leeg."));
            return new ImportPreviewResult(rows, issues);
        }

        if (!File.Exists(filePath))
        {
            issues.Add(new ImportIssue(0, "File", "Bestand niet gevonden.", filePath));
            return new ImportPreviewResult(rows, issues);
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        // ✅ SAFE leveranciers dictionary (geen crash bij duplicaten)
        var leveranciersByCode = await db.Leveranciers
            .AsNoTracking()
            .Where(l => !string.IsNullOrWhiteSpace(l.Code))
            .GroupBy(l => l.Code!.Trim())
            .Select(g => g.First())
            .ToDictionaryAsync(l => l.Code!.Trim());

        try
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.FirstOrDefault();

            if (ws is null)
            {
                issues.Add(new ImportIssue(0, "Sheet", "Excel bestand bevat geen werkblad."));
                return new ImportPreviewResult(rows, issues);
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            if (lastRow < 2)
            {
                issues.Add(new ImportIssue(0, "Sheet", "Excel bestand bevat geen data-rijen."));
                return new ImportPreviewResult(rows, issues);
            }

            for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var r = ws.Row(rowNumber);
                if (r.IsEmpty()) continue;

                var preview = ExcelRowMapper.TryMapPreview(
                    r,
                    leveranciersByCode,
                    rowNumber
                );

                rows.Add(preview);

                if (!preview.IsValid)
                {
                    issues.Add(new ImportIssue(
                        rowNumber,
                        "Row",
                        preview.ValidationMessage ?? "Ongeldige rij",
                        preview.Artikelnummer
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ImportIssue(0, "Excel", $"Fout bij inlezen: {ex.Message}"));
        }

        return new ImportPreviewResult(rows, issues);
    }

    public async Task<ImportCommitResult> CommitTypeLijstenAsync(IEnumerable<TypeLijstPreviewRow> rows)
    {
        int added = 0, updated = 0, skipped = 0;

        if (rows is null)
            return new ImportCommitResult(0, 0, 0);

        var rowList = rows.ToList();
        if (rowList.Count == 0)
            return new ImportCommitResult(0, 0, 0);

        await using var db = await _dbFactory.CreateDbContextAsync();

        // ✅ leveranciers op code (trim + case-insensitive)
        var leveranciersByCode = await db.Leveranciers
            .AsTracking()
            .Where(l => !string.IsNullOrWhiteSpace(l.Code))
            .ToDictionaryAsync(l => l.Code!.Trim(), StringComparer.OrdinalIgnoreCase);

        // ✅ bestaande lijsten op artikelnummer (trim + case-insensitive)
        var bestaandeByArtikelnummer = await db.TypeLijsten
            .AsTracking()
            .Where(t => !string.IsNullOrWhiteSpace(t.Artikelnummer))
            .ToDictionaryAsync(t => t.Artikelnummer.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var r in rowList)
        {
            // ✅ skip invalid rows
            if (r is null || !r.IsValid || string.IsNullOrWhiteSpace(r.Artikelnummer))
            {
                skipped++;
                continue;
            }

            var artikel = r.Artikelnummer.Trim();

            // leverancier code bepalen
            var code = (r.LeverancierCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                code = "QDO";

            // leverancier ophalen of aanmaken
            if (!leveranciersByCode.TryGetValue(code, out var leverancier))
            {
                leverancier = new Leverancier
                {
                    Code = code,
                    Naam = (code.Equals("QDO", StringComparison.OrdinalIgnoreCase))
                        ? "Quadro Default"
                        : (r.LeverancierNaam ?? code).Trim()
                };

                db.Leveranciers.Add(leverancier);
                leveranciersByCode[code] = leverancier;
                // ✅ nog geen SaveChanges nodig: EF tracked dit
            }

            // 🔁 UPDATE
            if (bestaandeByArtikelnummer.TryGetValue(artikel, out var existing))
            {
                existing.Opmerking = $"{r.Opmerking1 ?? ""}/{r.Opmerking2 ?? ""}".Trim('/');
                existing.BreedteCm = r.BreedteCm;
                existing.VoorraadMeter = r.Stock;
                existing.MinimumVoorraad = r.Minstock;
                existing.InventarisKost = r.Inventariskost;
                existing.Soort = r.Type ?? existing.Soort;
                existing.LaatsteUpdate = DateTime.Now;

                // ✅ zet FK expliciet (belangrijk)
                existing.LeverancierId = leverancier.Id;
                existing.Leverancier = leverancier;

                updated++;
            }
            // ➕ INSERT
            else
            {
                var nieuw = new TypeLijst
                {
                    Artikelnummer = artikel,
                    BreedteCm = r.BreedteCm,
                    Opmerking = $"{r.Opmerking1 ?? ""}/{r.Opmerking2 ?? ""}".Trim('/'),
                    PrijsPerMeter = 0m,
                    Soort = r.Type ?? "",
                    InventarisKost = r.Inventariskost,
                    VoorraadMeter = r.Stock,
                    MinimumVoorraad = r.Minstock,
                    WinstMargeFactor = 0.25m,
                    AfvalPercentage = 0m,
                    VasteKost = 0m,
                    WerkMinuten = 0,
                    LaatsteUpdate = DateTime.Now,

                    // ✅ zet FK expliciet
                    Leverancier = leverancier,
                    LeverancierId = leverancier.Id
                };

                db.TypeLijsten.Add(nieuw);
                bestaandeByArtikelnummer[artikel] = nieuw;

                added++;
            }
        }

        await db.SaveChangesAsync();
        return new ImportCommitResult(added, updated, skipped);
    }

}
