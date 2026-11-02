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
        var bestelbonnen = await db.Bestelbonnen.AsNoTracking().ToListAsync();

        var bestelbonPerOfferte = bestelbonnen.Where(f => f.OfferteId is not null)
            .GroupBy(f => f.OfferteId!.Value).ToDictionary(g => g.Key, g => g.First());
        var bestelbonPerWerkBon = bestelbonnen.Where(f => f.WerkBonId is not null)
            .GroupBy(f => f.WerkBonId!.Value).ToDictionary(g => g.Key, g => g.First());

        var gewijzigd = 0;
        foreach (var offerte in offertes)
        {
            if (offerte.Status == OfferteStatus.Geannuleerd) continue;

            Bestelbon? bestelbon = null;
            if (bestelbonPerOfferte.TryGetValue(offerte.Id, out var f1)) bestelbon = f1;
            else if (offerte.WerkBon is not null && bestelbonPerWerkBon.TryGetValue(offerte.WerkBon.Id, out var f2)) bestelbon = f2;

            var doel = BepaalDoelStatus(offerte.WerkBon, bestelbon);
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
    /// <remarks>
    /// Sinds de bestelbon als prijsvoorstel dient, bestaat er al een <b>voorlopige</b> bon
    /// (zonder volgnummer) bij een offerte in Concept/Verzonden, en krijgt de bon zijn
    /// volgnummer al bij "Bevestigen → Werkbon". Een bon telt daarom enkel voor "Besteld"
    /// als het werk klaar is (werkbon Afgewerkt/Afgehaald), of — bij oude gegevens zonder
    /// werkbon — als de bon genummerd is. Een voorlopige bon verhoogt de status nooit.
    /// </remarks>
    public static OfferteStatus? BepaalDoelStatus(WerkBon? werkbon, Bestelbon? bestelbon)
    {
        // Een voorlopige bon (prijsvoorstel) of geannuleerde bon telt niet mee.
        var geldigeBon = bestelbon is { IsVoorlopig: false } && bestelbon.Status != BestelbonStatus.Geannuleerd
            ? bestelbon
            : null;

        // Bestelbon betaald → Betaald.
        if (geldigeBon is { Status: BestelbonStatus.Betaald })
            return OfferteStatus.Betaald;

        // Werk klaar → Besteld.
        if (werkbon is { Status: WerkBonStatus.Afgewerkt or WerkBonStatus.Afgehaald })
            return OfferteStatus.Besteld;

        // Oude gegevens: genummerde bon zonder werkbon → Besteld.
        if (werkbon is null && geldigeBon is not null)
            return OfferteStatus.Besteld;

        // Werkbon in uitvoering → InProductie.
        if (werkbon is { Status: WerkBonStatus.InUitvoering })
            return OfferteStatus.InProductie;

        // Werkbon bestaat (Gepland) → minstens Goedgekeurd.
        if (werkbon is not null)
            return OfferteStatus.Goedgekeurd;

        return null;
    }
}
