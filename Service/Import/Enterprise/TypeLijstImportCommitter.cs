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
            .Where(l => !string.IsNullOrWhiteSpace(l.Code))
            .ToDictionaryAsync(l => l.Code.Trim(), StringComparer.OrdinalIgnoreCase, ct);

        var existing = await db.TypeLijsten
            .AsTracking()
            .Include(t => t.Leverancier)
            .Where(t => !string.IsNullOrWhiteSpace(t.Artikelnummer))
            .ToDictionaryAsync(t => t.Artikelnummer.Trim(), StringComparer.OrdinalIgnoreCase, ct);

        var fallbackLeverancierCode = $"QDO-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        foreach (var row in validRows)
        {
            ct.ThrowIfCancellationRequested();

            if (row.Parsed is null)
            {
                skipped++;
                continue;
            }

            var parsed = row.Parsed;
            var artikelnummer = parsed.Artikelnummer.Trim();
            if (string.IsNullOrWhiteSpace(artikelnummer))
            {
                skipped++;
                continue;
            }

            var leverancierCode = parsed.Leverancier?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(leverancierCode))
            {
                leverancierCode = fallbackLeverancierCode;
            }

            if (!leveranciersByCode.TryGetValue(leverancierCode, out var leverancier))
            {
                leverancier = new Leverancier
                {
                    Code = leverancierCode,
                    Naam = "Quadro Default"
                };
                db.Leveranciers.Add(leverancier);
                leveranciersByCode[leverancierCode] = leverancier;
            }

            if (!existing.TryGetValue(artikelnummer, out var current))
            {
                parsed.Artikelnummer = artikelnummer;
                parsed.Leverancier = leverancier;
                parsed.LaatsteUpdate = DateTime.Now;
                db.TypeLijsten.Add(parsed);
                existing[artikelnummer] = parsed;
                inserted++;
                continue;
            }

            current.Leverancier = leverancier;
            current.BreedteCm = parsed.BreedteCm;
            current.Soort = parsed.Soort;
            current.Serie = parsed.Serie;
            current.Opmerking = parsed.Opmerking;
            current.PrijsPerMeter = parsed.PrijsPerMeter;
            current.WinstMargeFactor = parsed.WinstMargeFactor;
            current.AfvalPercentage = parsed.AfvalPercentage;
            current.VasteKost = parsed.VasteKost;
            current.WerkMinuten = parsed.WerkMinuten;
            current.VoorraadMeter = parsed.VoorraadMeter;
            current.MinimumVoorraad = parsed.MinimumVoorraad;
            current.InventarisKost = parsed.InventarisKost;
            current.LaatsteUpdate = DateTime.Now;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }
}
