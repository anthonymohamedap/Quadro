using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class TypeLijstImportCommitter : IImportCommitter<TypeLijst>
{
    public async Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<TypeLijst>> validRows, AppDbContext db, CancellationToken ct)
    {
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        var leveranciersByCode = await db.Leveranciers
            .ToDictionaryAsync(l => l.Code, StringComparer.OrdinalIgnoreCase, ct);

        var existing = await db.TypeLijsten
            .Include(t => t.Leverancier)
            .ToListAsync(ct);

        foreach (var row in validRows)
        {
            ct.ThrowIfCancellationRequested();

            if (row.Parsed is null)
            {
                skipped++;
                continue;
            }

            var leverancierCode = row.Parsed.Leverancier?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(leverancierCode) || !leveranciersByCode.TryGetValue(leverancierCode, out var leverancier))
            {
                skipped++;
                continue;
            }

            var artikelnummer = row.Parsed.Artikelnummer.Trim();
            var current = existing.FirstOrDefault(t =>
                t.LeverancierId == leverancier.Id &&
                string.Equals(t.Artikelnummer, artikelnummer, StringComparison.OrdinalIgnoreCase));

            if (current is null)
            {
                row.Parsed.LeverancierId = leverancier.Id;
                row.Parsed.Leverancier = leverancier;
                row.Parsed.Artikelnummer = artikelnummer;
                row.Parsed.LaatsteUpdate = DateTime.Now;
                db.TypeLijsten.Add(row.Parsed);
                inserted++;
                continue;
            }

            current.BreedteCm = row.Parsed.BreedteCm;
            current.Soort = string.IsNullOrWhiteSpace(row.Parsed.Soort) ? current.Soort : row.Parsed.Soort;
            current.Serie = row.Parsed.Serie;
            current.IsDealer = row.Parsed.IsDealer;
            current.Opmerking = row.Parsed.Opmerking;
            current.LaatsteUpdate = DateTime.Now;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }
}
