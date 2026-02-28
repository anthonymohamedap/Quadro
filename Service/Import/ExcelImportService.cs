// Candidate for removal – requires runtime verification
﻿using ClosedXML.Excel;
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

namespace QuadroApp.Service.Import;

[Obsolete("Not used in current startup flow. Remove after runtime verification.")]
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

        var leveranciersByNaam = await db.Leveranciers
            .AsNoTracking()
            .Where(l => !string.IsNullOrWhiteSpace(l.Naam))
            .GroupBy(l => l.Naam.Trim().ToUpper())
            .Select(g => g.First())
            .ToDictionaryAsync(l => l.Naam.Trim().ToUpper());

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
                    leveranciersByNaam,
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

        var leveranciersByNaam = await db.Leveranciers
            .AsTracking()
            .Where(l => !string.IsNullOrWhiteSpace(l.Naam))
            .ToDictionaryAsync(l => l.Naam.Trim().ToUpper(), StringComparer.OrdinalIgnoreCase);

        var bestaandeByArtikelnummer = await db.TypeLijsten
            .AsTracking()
            .Where(t => !string.IsNullOrWhiteSpace(t.Artikelnummer))
            .ToDictionaryAsync(t => t.Artikelnummer.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var r in rowList)
        {
            if (r is null || !r.IsValid || string.IsNullOrWhiteSpace(r.Artikelnummer))
            {
                skipped++;
                continue;
            }

            var artikel = r.Artikelnummer.Trim();
            var leverancierNaam = NormalizeLeverancierNaam(r.Leverancier);
            if (string.IsNullOrWhiteSpace(leverancierNaam))
            {
                skipped++;
                continue;
            }

            if (!leveranciersByNaam.TryGetValue(leverancierNaam, out var leverancier))
            {
                leverancier = new Leverancier { Naam = leverancierNaam };
                db.Leveranciers.Add(leverancier);
                leveranciersByNaam[leverancierNaam] = leverancier;
            }

            if (bestaandeByArtikelnummer.TryGetValue(artikel, out var existing))
            {
                existing.Opmerking = $"{r.Opmerking1 ?? ""}/{r.Opmerking2 ?? ""}".Trim('/');
                existing.BreedteCm = r.BreedteCm;
                existing.VoorraadMeter = r.Stock;
                existing.MinimumVoorraad = r.Minstock;
                existing.InventarisKost = r.Inventariskost;
                existing.Soort = r.Type ?? existing.Soort;
                existing.Levcode = (r.Levcode ?? string.Empty).Trim();
                existing.LaatsteUpdate = DateTime.Now;
                existing.Leverancier = leverancier;

                updated++;
            }
            else
            {
                var nieuw = new TypeLijst
                {
                    Artikelnummer = artikel,
                    Levcode = (r.Levcode ?? string.Empty).Trim(),
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
                    Leverancier = leverancier
                };

                db.TypeLijsten.Add(nieuw);
                bestaandeByArtikelnummer[artikel] = nieuw;

                added++;
            }
        }

        await db.SaveChangesAsync();
        return new ImportCommitResult(added, updated, skipped);
    }

    private static string NormalizeLeverancierNaam(string? naam)
        => string.IsNullOrWhiteSpace(naam) ? string.Empty : naam.Trim().ToUpperInvariant();
}
