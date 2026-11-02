using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>
/// US-69/US-70 — schrijft voorschotten en betalingen naar het ontvangstenregister.
/// <para>Het voorschot wordt <b>incrementeel</b> bijgehouden op <see cref="Offerte.VoorschotBedrag"/>:
/// oude offertes hebben een voorschotbedrag zonder ontvangstregels (van vóór deze versie, zonder
/// betaalwijze) — die bedragen blijven zo behouden.</para>
/// </summary>
public sealed class OntvangstService : IOntvangstService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;
    private readonly IBestelbonWorkflowService? _bestelbonWorkflow;

    public OntvangstService(IDbContextFactory<AppDbContext> factory, IAuthService auth, IBestelbonWorkflowService? bestelbonWorkflow = null)
    {
        _factory = factory;
        _auth = auth;
        _bestelbonWorkflow = bestelbonWorkflow;
    }

    public async Task<List<Ontvangst>> GetVoorschottenAsync(int offerteId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Ontvangsten.AsNoTracking()
            .Where(o => o.OfferteId == offerteId && o.Soort == OntvangstSoort.Voorschot)
            .OrderByDescending(o => o.Datum).ThenByDescending(o => o.Id)
            .ToListAsync();
    }

    public async Task<decimal> RegistreerVoorschotAsync(int offerteId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze)
    {
        _auth.VereisPermissie(Permissie.Bestelbonnen);
        if (bedragIncl <= 0m)
            throw new InvalidOperationException("Het voorschot moet groter zijn dan 0.");

        await using var db = await _factory.CreateDbContextAsync();
        var offerte = await db.Offertes.FirstOrDefaultAsync(o => o.Id == offerteId)
            ?? throw new InvalidOperationException("Offerte niet gevonden. Sla de offerte eerst op.");

        db.Ontvangsten.Add(new Ontvangst
        {
            Soort = OntvangstSoort.Voorschot,
            OfferteId = offerteId,
            Datum = datum.Date,
            Betaalwijze = betaalwijze,
            BedragIncl = Math.Round(bedragIncl, 2),
            AangemaaktDoor = _auth.CurrentUser?.GebruikersNaam
        });

        offerte.VoorschotBedrag = Math.Round(offerte.VoorschotBedrag + bedragIncl, 2);
        offerte.IsVoorschotBetaald = offerte.VoorschotBedrag > 0m;
        await SyncBestelbonVoorschotAsync(db, offerteId, offerte.VoorschotBedrag);

        await db.SaveChangesAsync();
        return offerte.VoorschotBedrag;
    }

    public async Task<decimal?> VerwijderAsync(int ontvangstId)
    {
        _auth.VereisPermissie(Permissie.Bestelbonnen);

        await using var db = await _factory.CreateDbContextAsync();
        var ontvangst = await db.Ontvangsten.FirstOrDefaultAsync(o => o.Id == ontvangstId);
        if (ontvangst is null) return null;

        decimal? nieuwVoorschot = null;
        if (ontvangst.Soort == OntvangstSoort.Voorschot && ontvangst.OfferteId is int offerteId)
        {
            var offerte = await db.Offertes.FirstOrDefaultAsync(o => o.Id == offerteId);
            if (offerte is not null)
            {
                offerte.VoorschotBedrag = Math.Max(0m, Math.Round(offerte.VoorschotBedrag - ontvangst.BedragIncl, 2));
                offerte.IsVoorschotBetaald = offerte.VoorschotBedrag > 0m;
                await SyncBestelbonVoorschotAsync(db, offerteId, offerte.VoorschotBedrag);
                nieuwVoorschot = offerte.VoorschotBedrag;
            }
        }

        db.Ontvangsten.Remove(ontvangst);

        // US-72: stond de bestelbon op Betaald en is er nu weer een rest, zet hem dan terug open.
        await HeropenBestelbonIndienNodigAsync(db, ontvangst);

        await db.SaveChangesAsync();
        return nieuwVoorschot;
    }

    /// <summary>US-72 — na het verwijderen van een voorschot of betaling: was de bestelbon
    /// <see cref="BestelbonStatus.Betaald"/> en staat er weer een rest open, dan gaat hij terug naar
    /// Geëxporteerd (als hij al geëxporteerd was) of KlaarVoorExport; de offerte gaat van Betaald
    /// terug naar Besteld. Werkt op de nog niet opgeslagen wijzigingen in <paramref name="db"/>.</summary>
    private static async Task HeropenBestelbonIndienNodigAsync(AppDbContext db, Ontvangst verwijderd)
    {
        Bestelbon? bestelbon = null;
        if (verwijderd.BestelbonId is int fid)
            bestelbon = await db.Bestelbonnen.FirstOrDefaultAsync(f => f.Id == fid);
        else if (verwijderd.OfferteId is int oid)
            bestelbon = await db.Bestelbonnen.FirstOrDefaultAsync(f => f.OfferteId == oid);

        if (bestelbon is null || bestelbon.Status != BestelbonStatus.Betaald)
            return;

        var betalingen = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.BestelbonId == bestelbon.Id && o.Soort == OntvangstSoort.Betaling && o.Id != verwijderd.Id)
            .Select(o => o.BedragIncl)
            .ToListAsync();

        var stand = new BetaalStand(bestelbon.TotaalInclBtw, bestelbon.VoorschotBedrag, betalingen.Sum(), true);
        if (stand.Rest <= 0m)
            return;

        bestelbon.Status = string.IsNullOrWhiteSpace(bestelbon.ExportPad)
            ? BestelbonStatus.KlaarVoorExport
            : BestelbonStatus.Geexporteerd;
        bestelbon.BijgewerktOp = DateTime.UtcNow;

        if (bestelbon.OfferteId is int offerteId)
        {
            var offerte = await db.Offertes.FirstOrDefaultAsync(o => o.Id == offerteId);
            if (offerte is not null && offerte.Status == OfferteStatus.Betaald)
                offerte.Status = OfferteStatus.Besteld;
        }
    }

    // ── US-70: betalingen op een bestelbon ──

    public async Task<List<Ontvangst>> GetBetalingenAsync(int bestelbonId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Ontvangsten.AsNoTracking()
            .Where(o => o.BestelbonId == bestelbonId && o.Soort == OntvangstSoort.Betaling)
            .OrderByDescending(o => o.Datum).ThenByDescending(o => o.Id)
            .ToListAsync();
    }

    public async Task<BetaalStand> GetBetaalStandAsync(int bestelbonId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await BerekenStandAsync(db, bestelbonId);
    }

    public async Task<BetaalStand> RegistreerBetalingAsync(int bestelbonId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze)
    {
        _auth.VereisPermissie(Permissie.Bestelbonnen);
        if (bedragIncl <= 0m)
            throw new InvalidOperationException("Het bedrag moet groter zijn dan 0.");

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var bestelbon = await db.Bestelbonnen.FirstOrDefaultAsync(f => f.Id == bestelbonId)
                ?? throw new InvalidOperationException("Bestelbon niet gevonden.");
            if (bestelbon.Status == BestelbonStatus.Geannuleerd)
                throw new InvalidOperationException("Op een geannuleerde bestelbon kan geen betaling geregistreerd worden.");

            var stand = await BerekenStandAsync(db, bestelbonId);
            if (Math.Round(bedragIncl, 2) > stand.Rest)
                throw new InvalidOperationException($"Het bedrag (€ {bedragIncl:0.00}) is hoger dan wat nog te betalen is (€ {stand.Rest:0.00}).");

            db.Ontvangsten.Add(new Ontvangst
            {
                Soort = OntvangstSoort.Betaling,
                BestelbonId = bestelbonId,
                OfferteId = bestelbon.OfferteId,
                Datum = datum.Date,
                Betaalwijze = betaalwijze,
                BedragIncl = Math.Round(bedragIncl, 2),
                AangemaaktDoor = _auth.CurrentUser?.GebruikersNaam
            });
            await db.SaveChangesAsync();
        }

        var nieuw = await GetBetaalStandAsync(bestelbonId);

        // Volledig betaald → bestelbon op Betaald (via de bestaande workflow, die ook de offerte
        // naar Betaald laat volgen — US-42).
        if (nieuw.Rest == 0m && !nieuw.IsBetaaldStatus && _bestelbonWorkflow is not null)
        {
            await _bestelbonWorkflow.MarkeerBetaaldAsync(bestelbonId);
            nieuw = nieuw with { IsBetaaldStatus = true };
        }
        return nieuw;
    }

    // ── US-76: afrekenen bij afhaling ──

    public async Task<AfrekenStand?> GetAfrekenStandAsync(int werkBonId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var werkBon = await db.WerkBonnen.AsNoTracking().FirstOrDefaultAsync(w => w.Id == werkBonId);
        if (werkBon is null) return null;

        var bestelbon = await ZoekBestelbonAsync(db, werkBon, tracked: false);
        if (bestelbon is null) return null;

        return new AfrekenStand(bestelbon.Id, bestelbon.BestelbonNummer, await BerekenStandAsync(db, bestelbon.Id));
    }

    public async Task<AfrekenStand> AfrekenenBijAfhalingAsync(int werkBonId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze)
    {
        _auth.VereisPermissie(Permissie.Bestelbonnen);
        if (bedragIncl <= 0m)
            throw new InvalidOperationException("Het bedrag moet groter zijn dan 0.");

        await using var db = await _factory.CreateDbContextAsync();

        var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId)
            ?? throw new InvalidOperationException("Werkbon niet gevonden.");
        if (werkBon.Status is not (WerkBonStatus.Afgewerkt or WerkBonStatus.Afgehaald))
            throw new InvalidOperationException("De werkbon moet eerst afgewerkt zijn voor hij afgehaald kan worden.");

        var bestelbon = await ZoekBestelbonAsync(db, werkBon, tracked: true)
            ?? throw new InvalidOperationException("Er is nog geen bestelbon voor deze werkbon.");

        var stand = await BerekenStandAsync(db, bestelbon.Id);
        var bedrag = Math.Round(bedragIncl, 2);
        if (bedrag > stand.Rest)
            throw new InvalidOperationException($"Het bedrag (€ {bedrag:0.00}) is hoger dan wat nog te betalen is (€ {stand.Rest:0.00}).");

        // Alles in dezelfde context → één SaveChanges = één transactie.
        werkBon.Status = WerkBonStatus.Afgehaald;

        db.Ontvangsten.Add(new Ontvangst
        {
            Soort = OntvangstSoort.Betaling,
            BestelbonId = bestelbon.Id,
            OfferteId = bestelbon.OfferteId ?? werkBon.OfferteId,
            Datum = datum.Date,
            Betaalwijze = betaalwijze,
            BedragIncl = bedrag,
            AangemaaktDoor = _auth.CurrentUser?.GebruikersNaam
        });

        var nieuw = stand with { Betaald = stand.Betaald + bedrag };
        if (nieuw.Rest == 0m && bestelbon.Status != BestelbonStatus.Betaald)
        {
            bestelbon.Status = BestelbonStatus.Betaald;
            bestelbon.BijgewerktOp = DateTime.UtcNow;

            var offerte = await db.Offertes.FirstOrDefaultAsync(o => o.Id == (bestelbon.OfferteId ?? werkBon.OfferteId));
            if (offerte is not null)
                OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.Betaald);

            nieuw = nieuw with { IsBetaaldStatus = true };
        }

        await db.SaveChangesAsync();
        return new AfrekenStand(bestelbon.Id, bestelbon.BestelbonNummer, nieuw);
    }

    private static Task<Bestelbon?> ZoekBestelbonAsync(AppDbContext db, WerkBon werkBon, bool tracked)
    {
        var q = tracked ? db.Bestelbonnen : db.Bestelbonnen.AsNoTracking();
        return q.Where(f => f.Status != BestelbonStatus.Geannuleerd
                            && (f.WerkBonId == werkBon.Id || f.OfferteId == werkBon.OfferteId))
                .OrderByDescending(f => f.WerkBonId == werkBon.Id)
                .FirstOrDefaultAsync();
    }

    private static async Task<BetaalStand> BerekenStandAsync(AppDbContext db, int bestelbonId)
    {
        var bestelbon = await db.Bestelbonnen.AsNoTracking().FirstOrDefaultAsync(f => f.Id == bestelbonId)
            ?? throw new InvalidOperationException("Bestelbon niet gevonden.");
        var betaald = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.BestelbonId == bestelbonId && o.Soort == OntvangstSoort.Betaling)
            .Select(o => o.BedragIncl)
            .ToListAsync();   // SQLite kan geen SUM op decimal → in het geheugen optellen
        return new BetaalStand(bestelbon.TotaalInclBtw, bestelbon.VoorschotBedrag, betaald.Sum(), bestelbon.Status == BestelbonStatus.Betaald);
    }

    /// <summary>De bestelbon (indien al gemaakt) toont het voorschot en "te betalen bij afhalen" —
    /// houd dat gelijk met de offerte. Een geannuleerde bestelbon laten we ongemoeid.</summary>
    private static async Task SyncBestelbonVoorschotAsync(AppDbContext db, int offerteId, decimal voorschot)
    {
        var bestelbon = await db.Bestelbonnen.FirstOrDefaultAsync(f => f.OfferteId == offerteId);
        if (bestelbon is not null && bestelbon.Status != BestelbonStatus.Geannuleerd)
        {
            bestelbon.VoorschotBedrag = voorschot;
            bestelbon.BijgewerktOp = DateTime.UtcNow;
        }
    }
}
