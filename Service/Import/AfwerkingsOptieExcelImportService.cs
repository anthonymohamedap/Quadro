// Candidate for removal – requires runtime verification
﻿using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;

[Obsolete("Not used in current startup flow. Remove after runtime verification.")]
public sealed class AfwerkingsOptieExcelImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AfwerkingsOptieExcelImportService> _logger;

    public AfwerkingsOptieExcelImportService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<AfwerkingsOptieExcelImportService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<ImportCommitResult> CommitAsync(IEnumerable<AfwerkingsOptiePreviewRow> rows)
    {
        int added = 0, updated = 0, skipped = 0;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // groepen op naam
        var groepenByNaam = await db.AfwerkingsGroepen
            .AsNoTracking()
            .Where(g => !string.IsNullOrWhiteSpace(g.Naam))
            .ToDictionaryAsync(g => g.Naam.Trim(), StringComparer.OrdinalIgnoreCase);

        // leveranciers op code (case-insensitive)
        var leveranciersByNaam = await db.Leveranciers
            .Where(l => !string.IsNullOrWhiteSpace(l.Naam))
            .ToDictionaryAsync(l => l.Naam!.Trim().ToUpper(), StringComparer.OrdinalIgnoreCase);

        // optioneel: alle bestaande opties al ophalen om N+1 queries te vermijden
        // (zeker als je veel rijen hebt)
        var bestaande = await db.AfwerkingsOpties
            .ToListAsync();

        foreach (var r in rows)
        {
            if (!r.IsValid)
            {
                skipped++;
                continue;
            }

            // 1) groep vinden
            if (!groepenByNaam.TryGetValue(r.AfwerkingsGroep?.Trim() ?? "", out var groep))
            {
                skipped++;
                _logger.LogWarning("Row {Row}: groep '{Groep}' niet gevonden", r.RowNumber, r.AfwerkingsGroep);
                continue;
            }

            r.AfwerkingsGroepId = groep.Id;

            // 2) leverancier bepalen op Naam
            var naam = NormalizeLeverancierNaam(r.Leverancier);
            if (string.IsNullOrWhiteSpace(naam))
            {
                skipped++;
                continue;
            }

            if (!leveranciersByNaam.TryGetValue(naam, out var leverancier))
            {
                leverancier = new Leverancier
                {
                    Naam = naam
                };

                db.Leveranciers.Add(leverancier);
                leveranciersByNaam[naam] = leverancier;
            }

            // 3) update of insert (match op groep + volgnummer char)
            var bestaand = bestaande.FirstOrDefault(a =>
                a.AfwerkingsGroepId == r.AfwerkingsGroepId &&
                a.Volgnummer == r.Volgnummer);

            if (bestaand is not null)
            {
                bestaand.Naam = r.Naam?.Trim() ?? "";
                bestaand.KostprijsPerM2 = r.KostprijsPerM2;
                bestaand.WinstMarge = r.WinstMarge;
                bestaand.AfvalPercentage = r.AfvalPercentage;
                bestaand.VasteKost = r.VasteKost;
                bestaand.WerkMinuten = r.WerkMinuten;

                // ✅ zet navigation (werkt ook voor nieuwe leveranciers zonder Id)
                bestaand.Leverancier = leverancier;
                bestaand.LeverancierId = null; // optioneel: mag weg, navigation is genoeg

                updated++;
            }
            else
            {
                var nieuw = new AfwerkingsOptie
                {
                    AfwerkingsGroepId = r.AfwerkingsGroepId,
                    Naam = r.Naam?.Trim() ?? "",
                    Volgnummer = r.Volgnummer, // ✅ char
                    KostprijsPerM2 = r.KostprijsPerM2,
                    WinstMarge = r.WinstMarge,
                    AfvalPercentage = r.AfvalPercentage,
                    VasteKost = r.VasteKost,
                    WerkMinuten = r.WerkMinuten,

                    // ✅ navigation
                    Leverancier = leverancier
                };

                db.AfwerkingsOpties.Add(nieuw);
                bestaande.Add(nieuw); // zodat duplicates in dezelfde import ook gezien worden
                added++;
            }
        }

        await db.SaveChangesAsync();
        return new ImportCommitResult(added, updated, skipped);
    }
    private static string NormalizeLeverancierNaam(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();

    public async Task<ImportPreviewResult<AfwerkingsOptiePreviewRow>> PreviewAsync(string file)
    {
        var rows = new List<AfwerkingsOptiePreviewRow>();
        var issues = new List<ImportIssue>();

        if (!File.Exists(file))
        {
            issues.Add(new ImportIssue(0, "File", "Bestand niet gevonden."));
            return new ImportPreviewResult<AfwerkingsOptiePreviewRow>(rows, issues);
        }

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheets.First();

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (int i = 2; i <= lastRow; i++)
        {
            var row = ws.Row(i);
            if (row.IsEmpty()) continue;

            var previewRow = AfwerkingsOptieExcelRowMapper.Map(row, i);
            rows.Add(previewRow);

            if (!previewRow.IsValid)
            {
                issues.Add(new ImportIssue(
                    i,
                    "Row",
                    previewRow.ValidationMessage ?? "Ongeldige rij"
                ));
            }
        }

        await Task.CompletedTask;
        return new ImportPreviewResult<AfwerkingsOptiePreviewRow>(rows, issues);
    }
}