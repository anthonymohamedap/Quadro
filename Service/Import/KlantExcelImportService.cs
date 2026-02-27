using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using QuadroApp.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Services.Import;

public sealed class KlantExcelImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICrudValidator<Klant> _validator;

    public KlantExcelImportService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICrudValidator<Klant> validator)
    {
        _dbFactory = dbFactory;
        _validator = validator;
    }

    // 👁 PREVIEW
    public async Task<ImportPreviewResult<KlantPreviewRow>> PreviewAsync(string filePath)
    {
        var rows = new List<KlantPreviewRow>();
        var issues = new List<ImportIssue>();

        if (!File.Exists(filePath))
        {
            issues.Add(new ImportIssue(0, "File", "Bestand niet gevonden"));
            return new ImportPreviewResult<KlantPreviewRow>(rows, issues);
        }

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.First();

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (int i = 2; i <= lastRow; i++)
        {
            var r = ws.Row(i);
            if (r.IsEmpty()) continue;

            var preview = KlantExcelRowMapper.Map(r, i);
            rows.Add(preview);

            if (!preview.IsValid)
            {
                issues.Add(new ImportIssue(
                    i,
                    "Row",
                    preview.ValidationMessage ?? "Ongeldige rij"
                ));
            }
        }

        return new ImportPreviewResult<KlantPreviewRow>(rows, issues);
    }

    // 💾 COMMIT
    public async Task<ImportCommitResult> CommitAsync(IEnumerable<KlantPreviewRow> rows)
    {
        int added = 0, updated = 0, skipped = 0;

        await using var db = await _dbFactory.CreateDbContextAsync();

        // bestaande klanten op email (case-insensitive)
        var klantenByEmail = await db.Klanten
            .Where(k => k.Email != null)
            .ToDictionaryAsync(k => k.Email!, StringComparer.OrdinalIgnoreCase);

        foreach (var r in rows)
        {
            // 1) Mapper-validatie (snelle check)
            if (!r.IsValid)
            {
                skipped++;
                continue;
            }

            // 2) Maak entity + trim
            var entity = r.ToEntity();
            Normalize(entity);

            // 3) Bepaal scenario: update of create
            var hasEmail = !string.IsNullOrWhiteSpace(entity.Email);

            if (hasEmail && klantenByEmail.TryGetValue(entity.Email!, out var existing))
            {
                // UPDATE scenario -> valideer update
                entity.Id = existing.Id;

                var vr = await _validator.ValidateUpdateAsync(entity);
                if (!vr.IsValid)
                {
                    skipped++;
                    continue;
                }

                // pas velden toe op tracked entity
                existing.Voornaam = entity.Voornaam;
                existing.Achternaam = entity.Achternaam;
                existing.Email = entity.Email;
                existing.Telefoon = entity.Telefoon;
                existing.Straat = entity.Straat;
                existing.Nummer = entity.Nummer;
                existing.Postcode = entity.Postcode;
                existing.Gemeente = entity.Gemeente;
                existing.BtwNummer = entity.BtwNummer;
                existing.Opmerking = entity.Opmerking;

                updated++;
            }
            else
            {
                // CREATE scenario -> valideer create
                var vr = await _validator.ValidateCreateAsync(entity);
                if (!vr.IsValid)
                {
                    skipped++;
                    continue;
                }

                db.Klanten.Add(entity);

                // als email aanwezig: meteen toevoegen aan dictionary om dubbele emails binnen dezelfde import te blokkeren
                if (hasEmail)
                    klantenByEmail[entity.Email!] = entity;

                added++;
            }
        }

        await db.SaveChangesAsync();
        return new ImportCommitResult(added, updated, skipped);
    }

    private static void Normalize(Klant k)
    {
        k.Voornaam = (k.Voornaam ?? "").Trim();
        k.Achternaam = (k.Achternaam ?? "").Trim();
        k.Email = string.IsNullOrWhiteSpace(k.Email) ? null : k.Email.Trim();
        k.Telefoon = string.IsNullOrWhiteSpace(k.Telefoon) ? null : k.Telefoon.Trim();
        k.Straat = string.IsNullOrWhiteSpace(k.Straat) ? null : k.Straat.Trim();
        k.Nummer = string.IsNullOrWhiteSpace(k.Nummer) ? null : k.Nummer.Trim();
        k.Postcode = string.IsNullOrWhiteSpace(k.Postcode) ? null : k.Postcode.Trim();
        k.Gemeente = string.IsNullOrWhiteSpace(k.Gemeente) ? null : k.Gemeente.Trim();
        k.BtwNummer = string.IsNullOrWhiteSpace(k.BtwNummer) ? null : k.BtwNummer.Trim();
        k.Opmerking = string.IsNullOrWhiteSpace(k.Opmerking) ? null : k.Opmerking.Trim();
    }
}
