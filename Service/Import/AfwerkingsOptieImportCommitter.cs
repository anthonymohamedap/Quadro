using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;

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
            var kleur = NormalizeKleur(parsed.Kleur);
            var huidig = bestaande.FirstOrDefault(o =>
                o.AfwerkingsGroepId == groep.Id &&
                o.Volgnummer == parsed.Volgnummer &&
                NormalizeKleur(o.Kleur) == kleur);

            if (huidig is null)
            {
                parsed.Naam = naam;
                parsed.Kleur = kleur;
                parsed.AfwerkingsGroep = groep;
                parsed.Leverancier = leverancier;
                db.AfwerkingsOpties.Add(parsed);
                bestaande.Add(parsed);
                SynchroniseerFamiliePrijs(bestaande, groep.Id, parsed.Volgnummer, parsed);
                inserted++;
                continue;
            }

            huidig.Naam = naam;
            huidig.Kleur = kleur;
            huidig.KostprijsPerM2 = parsed.KostprijsPerM2;
            huidig.WinstMarge = parsed.WinstMarge;
            huidig.AfvalPercentage = parsed.AfvalPercentage;
            huidig.VasteKost = parsed.VasteKost;
            huidig.WerkMinuten = parsed.WerkMinuten;
            huidig.Leverancier = leverancier;
            SynchroniseerFamiliePrijs(bestaande, groep.Id, parsed.Volgnummer, huidig);
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return (inserted, updated, skipped);
    }

    private static string NormalizeLeverancierNaam(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();

    private static string NormalizeKleur(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? "Standaard" : raw.Trim();

    private static void SynchroniseerFamiliePrijs(List<AfwerkingsOptie> opties, int groepId, char volgnummer, AfwerkingsOptie bron)
    {
        foreach (var optie in opties.Where(o => o.AfwerkingsGroepId == groepId && o.Volgnummer == volgnummer))
        {
            optie.KostprijsPerM2 = bron.KostprijsPerM2;
            optie.WinstMarge = bron.WinstMarge;
            optie.AfvalPercentage = bron.AfvalPercentage;
            optie.VasteKost = bron.VasteKost;
            optie.WerkMinuten = bron.WerkMinuten;
        }
    }
}

