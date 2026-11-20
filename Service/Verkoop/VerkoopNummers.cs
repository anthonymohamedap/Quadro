using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Verkoop;

/// <summary>
/// Zoekt bij geladen werkbonnen het definitieve bestelbonnummer op en vult <see cref="WerkBon.VerkoopNummerTekst"/>,
/// zodat planning, weeklijst en werkbon hetzelfde nummer tonen als het verkoopdossier.
/// </summary>
public static class VerkoopNummers
{
    public static async Task VulAsync(AppDbContext db, IEnumerable<WerkBon?> werkbonnen)
    {
        var wbs = werkbonnen.OfType<WerkBon>().Distinct().ToList();
        if (wbs.Count == 0) return;

        var perOfferte = await DefinitiefPerOfferteAsync(db, wbs.Select(w => w.OfferteId), wbs.Select(w => w.Id));
        foreach (var wb in wbs)
            wb.VerkoopNummerTekst = VerkoopNummer.Tekst(wb.Offerte?.OfferteNummer ?? 0,
                perOfferte.GetValueOrDefault(wb.OfferteId) ?? perOfferte.GetValueOrDefault(-wb.Id));
    }

    /// <summary>Definitieve bestelbonnummers, per offerte-id; bonnen die enkel aan een werkbon hangen onder -werkbonId.</summary>
    private static async Task<Dictionary<int, string>> DefinitiefPerOfferteAsync(
        AppDbContext db, IEnumerable<int> offerteIds, IEnumerable<int> werkBonIds)
    {
        var oIds = offerteIds.Distinct().ToList();
        var wIds = werkBonIds.Distinct().ToList();
        var bonnen = await db.Bestelbonnen.AsNoTracking()
            .Where(b => b.VolgNr != null
                        && ((b.OfferteId != null && oIds.Contains(b.OfferteId.Value))
                            || (b.WerkBonId != null && wIds.Contains(b.WerkBonId.Value))))
            .Select(b => new { b.OfferteId, b.WerkBonId, b.BestelbonNummer })
            .ToListAsync();

        var result = new Dictionary<int, string>();
        foreach (var b in bonnen)
        {
            if (b.OfferteId is int o) result.TryAdd(o, b.BestelbonNummer);
            else if (b.WerkBonId is int w) result.TryAdd(-w, b.BestelbonNummer);
        }
        return result;
    }
}
