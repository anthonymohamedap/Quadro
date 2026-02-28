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

        var leveranciersByNaam = await db.Leveranciers
            .Where(l => !string.IsNullOrWhiteSpace(l.Naam))
            .ToDictionaryAsync(l => l.Naam.Trim().ToUpper(), StringComparer.OrdinalIgnoreCase, ct);

        var existing = await db.TypeLijsten
            .AsTracking()
            .Include(t => t.Leverancier)
            .Where(t => !string.IsNullOrWhiteSpace(t.Artikelnummer))
            .ToDictionaryAsync(t => t.Artikelnummer.Trim(), StringComparer.OrdinalIgnoreCase, ct);

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

            var leverancierNaam = NormalizeLeverancierNaam(parsed.Leverancier?.Naam);
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

            if (!existing.TryGetValue(artikelnummer, out var current))
            {
                parsed.Artikelnummer = artikelnummer;
                parsed.Levcode = (parsed.Levcode ?? string.Empty).Trim();
                parsed.Leverancier = leverancier;
                parsed.LaatsteUpdate = DateTime.Now;
                db.TypeLijsten.Add(parsed);
                existing[artikelnummer] = parsed;
                inserted++;
                continue;
            }

            current.Leverancier = leverancier;
            current.Levcode = (parsed.Levcode ?? string.Empty).Trim();
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

    private static string NormalizeLeverancierNaam(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();
}
