using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.Service;

/// <summary>US-48 — reconciliatie van bestaande offertestatussen (admin-only).</summary>
public sealed class OfferteStatusReconciliatieService : IOfferteStatusReconciliatieService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;

    public OfferteStatusReconciliatieService(IDbContextFactory<AppDbContext> factory, IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    public async Task<int> ReconcilieerAlleAsync()
    {
        _auth.VereisPermissie(Permissie.GebruikersBeheren);
        await using var db = await _factory.CreateDbContextAsync();

        var offertes = await db.Offertes.Include(o => o.WerkBon).ToListAsync();
        var facturen = await db.Facturen.AsNoTracking().ToListAsync();

        var factuurPerOfferte = facturen.Where(f => f.OfferteId is not null)
            .GroupBy(f => f.OfferteId!.Value).ToDictionary(g => g.Key, g => g.First());
        var factuurPerWerkBon = facturen.Where(f => f.WerkBonId is not null)
            .GroupBy(f => f.WerkBonId!.Value).ToDictionary(g => g.Key, g => g.First());

        var gewijzigd = 0;
        foreach (var offerte in offertes)
        {
            if (offerte.Status == OfferteStatus.Geannuleerd) continue;

            Factuur? factuur = null;
            if (factuurPerOfferte.TryGetValue(offerte.Id, out var f1)) factuur = f1;
            else if (offerte.WerkBon is not null && factuurPerWerkBon.TryGetValue(offerte.WerkBon.Id, out var f2)) factuur = f2;

            var doel = BepaalDoelStatus(offerte.WerkBon, factuur);
            if (doel is OfferteStatus target && (int)target > (int)offerte.Status)
            {
                offerte.Status = target;
                gewijzigd++;
            }
        }

        if (gewijzigd > 0)
            await db.SaveChangesAsync();

        return gewijzigd;
    }

    /// <summary>
    /// Leidt de offerte-doelstatus af uit de werkbon + bestelbon. <c>null</c> = geen productie
    /// gestart (offerte blijft zoals ze is). Alleen omhoog toepassen door de aanroeper.
    /// </summary>
    public static OfferteStatus? BepaalDoelStatus(WerkBon? werkbon, Factuur? factuur)
    {
        // Bestelbon betaald → Betaald.
        if (factuur is { Status: FactuurStatus.Betaald })
            return OfferteStatus.Betaald;

        // Bestelbon bestaat (niet geannuleerd) of werkbon afgewerkt/afgehaald → Gefactureerd.
        if ((factuur is not null && factuur.Status != FactuurStatus.Geannuleerd)
            || werkbon is { Status: WerkBonStatus.Afgewerkt or WerkBonStatus.Afgehaald })
            return OfferteStatus.Gefactureerd;

        // Werkbon in uitvoering → InProductie.
        if (werkbon is { Status: WerkBonStatus.InUitvoering })
            return OfferteStatus.InProductie;

        // Werkbon bestaat (Gepland) → minstens Goedgekeurd.
        if (werkbon is not null)
            return OfferteStatus.Goedgekeurd;

        return null;
    }
}
