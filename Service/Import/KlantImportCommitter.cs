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

public sealed class KlantImportCommitter : IImportCommitter<Klant>
{
    public async Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<Klant>> validRows, AppDbContext db, CancellationToken ct)
    {
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        var customersByEmail = await db.Klanten
            .Where(k => k.Email != null)
            .ToDictionaryAsync(k => k.Email!, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var row in validRows)
        {
            ct.ThrowIfCancellationRequested();
            if (row.Parsed is null)
            {
                skipped++;
                continue;
            }

            var email = row.Parsed.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(email) && customersByEmail.TryGetValue(email, out var existing))
            {
                existing.Voornaam = row.Parsed.Voornaam;
                existing.Achternaam = row.Parsed.Achternaam;
                existing.Telefoon = row.Parsed.Telefoon;
                existing.Straat = row.Parsed.Straat;
                existing.Nummer = row.Parsed.Nummer;
                existing.Postcode = row.Parsed.Postcode;
                existing.Gemeente = row.Parsed.Gemeente;
                existing.BtwNummer = row.Parsed.BtwNummer;
                existing.Opmerking = row.Parsed.Opmerking;
                updated++;
                continue;
            }

            db.Klanten.Add(row.Parsed);
            inserted++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }
}
