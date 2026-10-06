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
    private readonly IFactuurWorkflowService? _factuurWorkflow;

    public OntvangstService(IDbContextFactory<AppDbContext> factory, IAuthService auth, IFactuurWorkflowService? factuurWorkflow = null)
    {
        _factory = factory;
        _auth = auth;
        _factuurWorkflow = factuurWorkflow;
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
        _auth.VereisPermissie(Permissie.Factureren);
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
        await SyncFactuurVoorschotAsync(db, offerteId, offerte.VoorschotBedrag);

        await db.SaveChangesAsync();
        return offerte.VoorschotBedrag;
    }

    public async Task<decimal?> VerwijderAsync(int ontvangstId)
    {
        _auth.VereisPermissie(Permissie.Factureren);

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
                await SyncFactuurVoorschotAsync(db, offerteId, offerte.VoorschotBedrag);
                nieuwVoorschot = offerte.VoorschotBedrag;
            }
        }

        db.Ontvangsten.Remove(ontvangst);
        await db.SaveChangesAsync();
        return nieuwVoorschot;
    }

    // ── US-70: betalingen op een bestelbon ──

    public async Task<List<Ontvangst>> GetBetalingenAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Ontvangsten.AsNoTracking()
            .Where(o => o.FactuurId == factuurId && o.Soort == OntvangstSoort.Betaling)
            .OrderByDescending(o => o.Datum).ThenByDescending(o => o.Id)
            .ToListAsync();
    }

    public async Task<BetaalStand> GetBetaalStandAsync(int factuurId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await BerekenStandAsync(db, factuurId);
    }

    public async Task<BetaalStand> RegistreerBetalingAsync(int factuurId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze)
    {
        _auth.VereisPermissie(Permissie.Factureren);
        if (bedragIncl <= 0m)
            throw new InvalidOperationException("Het bedrag moet groter zijn dan 0.");

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var factuur = await db.Facturen.FirstOrDefaultAsync(f => f.Id == factuurId)
                ?? throw new InvalidOperationException("Bestelbon niet gevonden.");
            if (factuur.Status == FactuurStatus.Geannuleerd)
                throw new InvalidOperationException("Op een geannuleerde bestelbon kan geen betaling geregistreerd worden.");

            var stand = await BerekenStandAsync(db, factuurId);
            if (Math.Round(bedragIncl, 2) > stand.Rest)
                throw new InvalidOperationException($"Het bedrag (€ {bedragIncl:0.00}) is hoger dan wat nog te betalen is (€ {stand.Rest:0.00}).");

            db.Ontvangsten.Add(new Ontvangst
            {
                Soort = OntvangstSoort.Betaling,
                FactuurId = factuurId,
                OfferteId = factuur.OfferteId,
                Datum = datum.Date,
                Betaalwijze = betaalwijze,
                BedragIncl = Math.Round(bedragIncl, 2),
                AangemaaktDoor = _auth.CurrentUser?.GebruikersNaam
            });
            await db.SaveChangesAsync();
        }

        var nieuw = await GetBetaalStandAsync(factuurId);

        // Volledig betaald → bestelbon op Betaald (via de bestaande workflow, die ook de offerte
        // naar Betaald laat volgen — US-42).
        if (nieuw.Rest == 0m && !nieuw.IsBetaaldStatus && _factuurWorkflow is not null)
        {
            await _factuurWorkflow.MarkeerBetaaldAsync(factuurId);
            nieuw = nieuw with { IsBetaaldStatus = true };
        }
        return nieuw;
    }

    private static async Task<BetaalStand> BerekenStandAsync(AppDbContext db, int factuurId)
    {
        var factuur = await db.Facturen.AsNoTracking().FirstOrDefaultAsync(f => f.Id == factuurId)
            ?? throw new InvalidOperationException("Bestelbon niet gevonden.");
        var betaald = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.FactuurId == factuurId && o.Soort == OntvangstSoort.Betaling)
            .Select(o => o.BedragIncl)
            .ToListAsync();   // SQLite kan geen SUM op decimal → in het geheugen optellen
        return new BetaalStand(factuur.TotaalInclBtw, factuur.VoorschotBedrag, betaald.Sum(), factuur.Status == FactuurStatus.Betaald);
    }

    /// <summary>De bestelbon (indien al gemaakt) toont het voorschot en "te betalen bij afhalen" —
    /// houd dat gelijk met de offerte. Een geannuleerde bestelbon laten we ongemoeid.</summary>
    private static async Task SyncFactuurVoorschotAsync(AppDbContext db, int offerteId, decimal voorschot)
    {
        var factuur = await db.Facturen.FirstOrDefaultAsync(f => f.OfferteId == offerteId);
        if (factuur is not null && factuur.Status != FactuurStatus.Geannuleerd)
        {
            factuur.VoorschotBedrag = voorschot;
            factuur.BijgewerktOp = DateTime.UtcNow;
        }
    }
}
