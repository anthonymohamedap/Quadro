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
            .ToDictionaryAsync(g => g.Code, ct);

        var leveranciersByNaam = await db.Leveranciers
            .Where(l => !string.IsNullOrWhiteSpace(l.Naam))
            .ToDictionaryAsync(l => l.Naam.Trim().ToUpper(), StringComparer.OrdinalIgnoreCase, ct);

        var bestaande = await db.AfwerkingsOpties
            .AsTracking()
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

            var parsed = row.Parsed;
            var groepCode = parsed.AfwerkingsGroep?.Code;
            if (!groepCode.HasValue || !groepenByCode.TryGetValue(groepCode.Value, out var groep))
            {
                skipped++;
                continue;
            }

            var leverancierNaam = NormalizeLeverancierNaam(parsed.Leverancier?.Naam);
            if (string.IsNullOrWhiteSpace(leverancierNaam))
            {
                skipped++;
                continue;
            }

            if (!leveranciersByNaam.TryGetValue(leverancierNaam, out var leverancier))
            {
                leverancier = new Leverancier
                {
                    Naam = leverancierNaam
                };
                db.Leveranciers.Add(leverancier);
                leveranciersByNaam[leverancierNaam] = leverancier;
            }

            var naam = parsed.Naam.Trim();
            var huidig = bestaande.FirstOrDefault(o =>
                o.AfwerkingsGroepId == groep.Id &&
                o.Volgnummer == parsed.Volgnummer);

            if (huidig is null)
            {
                parsed.Naam = naam;
                parsed.AfwerkingsGroep = groep;
                parsed.Leverancier = leverancier;
                db.AfwerkingsOpties.Add(parsed);
                bestaande.Add(parsed);
                inserted++;
                continue;
            }

            huidig.Naam = naam;
            huidig.KostprijsPerM2 = parsed.KostprijsPerM2;
            huidig.WinstMarge = parsed.WinstMarge;
            huidig.AfvalPercentage = parsed.AfvalPercentage;
            huidig.VasteKost = parsed.VasteKost;
            huidig.WerkMinuten = parsed.WerkMinuten;
            huidig.Leverancier = leverancier;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }

    private static string NormalizeLeverancierNaam(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();
}

