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

public sealed class AfwerkingsOptieImportCommitter : IImportCommitter<AfwerkingsOptie>
{
    public async Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<AfwerkingsOptie>> validRows, AppDbContext db, CancellationToken ct)
    {
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        var groepenByCode = await db.AfwerkingsGroepen
            .ToDictionaryAsync(g => g.Code, StringComparer.OrdinalIgnoreCase, ct);

        var bestaande = await db.AfwerkingsOpties
            .Include(o => o.AfwerkingsGroep)
            .ToListAsync(ct);

        foreach (var row in validRows)
        {
            ct.ThrowIfCancellationRequested();
            if (row.Parsed is null)
            {
                skipped++;
                continue;
            }

            var groepCode = row.Parsed.AfwerkingsGroep?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(groepCode) || !groepenByCode.TryGetValue(groepCode, out var groep))
            {
                skipped++;
                continue;
            }

            var naam = row.Parsed.Naam.Trim();
            var huidig = bestaande.FirstOrDefault(o =>
                o.AfwerkingsGroepId == groep.Id &&
                string.Equals(o.Naam, naam, StringComparison.OrdinalIgnoreCase));

            if (huidig is null)
            {
                row.Parsed.AfwerkingsGroepId = groep.Id;
                row.Parsed.AfwerkingsGroep = groep;
                if (row.Parsed.Volgnummer == default)
                {
                    row.Parsed.Volgnummer = 'A';
                }

                db.AfwerkingsOpties.Add(row.Parsed);
                inserted++;
                continue;
            }

            huidig.Naam = naam;
            if (row.Parsed.Volgnummer != default)
            {
                huidig.Volgnummer = row.Parsed.Volgnummer;
            }
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }
}
