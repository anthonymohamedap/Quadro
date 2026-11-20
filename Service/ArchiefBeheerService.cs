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
/// Gearchiveerde klanten en lijsten terugzetten of definitief verwijderen (afspraak 19/11/2026).
/// Archiveren zelf blijft in de schermen zoals het was (vlag IsGearchiveerd).
/// <list type="bullet">
/// <item>Terugzetten en verwijderen vragen hetzelfde recht als archiveren (klanten: KlantVerwijderen,
/// lijsten: LijstVerwijderen).</item>
/// <item>Definitief verwijderen kan enkel als het item gearchiveerd is én nergens meer gebruikt wordt;
/// anders volgt een melding met de reden en blijft alles zoals het was.</item>
/// </list>
/// </summary>
public sealed class ArchiefBeheerService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;

    public ArchiefBeheerService(IDbContextFactory<AppDbContext> factory, IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    // ── Klanten ─────────────────────────────────────────────────────────────

    public async Task<List<Klant>> GearchiveerdeKlantenAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Klanten.IgnoreQueryFilters().AsNoTracking()
            .Where(k => k.IsGearchiveerd)
            .OrderBy(k => k.Achternaam).ThenBy(k => k.Voornaam)
            .ToListAsync();
    }

    public async Task ZetKlantTerugAsync(int klantId)
    {
        _auth.VereisPermissie(Permissie.KlantVerwijderen);
        await using var db = await _factory.CreateDbContextAsync();
        var klant = await db.Klanten.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.Id == klantId)
            ?? throw new InvalidOperationException("Klant niet gevonden.");
        klant.IsGearchiveerd = false;
        klant.GearchiveerdOp = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Waarom deze klant niet definitief weg kan; null = het kan.</summary>
    public async Task<string?> WaaromKlantNietWegAsync(int klantId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var offertes = await db.Offertes.IgnoreQueryFilters().CountAsync(o => o.KlantId == klantId);
        var archief = await db.OfferteArchieven.CountAsync(a => a.KlantId == klantId)
                    + await db.WerkBonArchieven.CountAsync(a => a.KlantId == klantId);
        var delen = new List<string>();
        if (offertes > 0) delen.Add($"{offertes} verkoop/verkopen");
        if (archief > 0) delen.Add($"{archief} stuk(ken) in het archief");
        return delen.Count == 0
            ? null
            : $"Deze klant hoort nog bij {string.Join(" en ", delen)}. Definitief verwijderen kan niet; de klant blijft gearchiveerd.";
    }

    public async Task VerwijderKlantDefinitiefAsync(int klantId)
    {
        _auth.VereisPermissie(Permissie.KlantVerwijderen);
        var reden = await WaaromKlantNietWegAsync(klantId);
        if (reden is not null) throw new InvalidOperationException(reden);

        await using var db = await _factory.CreateDbContextAsync();
        var klant = await db.Klanten.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.Id == klantId)
            ?? throw new InvalidOperationException("Klant niet gevonden.");
        if (!klant.IsGearchiveerd)
            throw new InvalidOperationException("Enkel een gearchiveerde klant kan definitief verwijderd worden.");
        db.Klanten.Remove(klant);
        await db.SaveChangesAsync();
    }

    // ── Lijsten ─────────────────────────────────────────────────────────────

    public async Task<List<TypeLijst>> GearchiveerdeLijstenAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.TypeLijsten.IgnoreQueryFilters().AsNoTracking()
            .Include(l => l.Leverancier)
            .Where(l => l.IsGearchiveerd)
            .OrderBy(l => l.Artikelnummer)
            .ToListAsync();
    }

    public async Task ZetLijstTerugAsync(int lijstId)
    {
        _auth.VereisPermissie(Permissie.LijstVerwijderen);
        await using var db = await _factory.CreateDbContextAsync();
        var lijst = await db.TypeLijsten.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == lijstId)
            ?? throw new InvalidOperationException("Lijst niet gevonden.");
        lijst.IsGearchiveerd = false;
        await db.SaveChangesAsync();
    }

    /// <summary>Waarom deze lijst niet definitief weg kan; null = het kan.</summary>
    public async Task<string?> WaaromLijstNietWegAsync(int lijstId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var regels = await db.OfferteRegels.IgnoreQueryFilters().CountAsync(r => r.TypeLijstId == lijstId || r.InlegTypeLijstId == lijstId);
        var mutaties = await db.VoorraadMutaties.IgnoreQueryFilters().CountAsync(m => m.TypeLijstId == lijstId);
        var bestellijnen = await db.LeverancierBestelLijnen.IgnoreQueryFilters().CountAsync(b => b.TypeLijstId == lijstId);
        var delen = new List<string>();
        if (regels > 0) delen.Add($"{regels} inlijsting(en) op offertes");
        if (mutaties > 0) delen.Add($"{mutaties} voorraadbeweging(en)");
        if (bestellijnen > 0) delen.Add($"{bestellijnen} bestelling(en) bij de leverancier");
        return delen.Count == 0
            ? null
            : $"Deze lijst wordt nog gebruikt in {string.Join(", ", delen)}. Definitief verwijderen kan niet; de lijst blijft gearchiveerd.";
    }

    public async Task VerwijderLijstDefinitiefAsync(int lijstId)
    {
        _auth.VereisPermissie(Permissie.LijstVerwijderen);
        var reden = await WaaromLijstNietWegAsync(lijstId);
        if (reden is not null) throw new InvalidOperationException(reden);

        await using var db = await _factory.CreateDbContextAsync();
        var lijst = await db.TypeLijsten.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == lijstId)
            ?? throw new InvalidOperationException("Lijst niet gevonden.");
        if (!lijst.IsGearchiveerd)
            throw new InvalidOperationException("Enkel een gearchiveerde lijst kan definitief verwijderd worden.");

        // Voorraadmeldingen horen bij de lijst zelf en gaan mee weg.
        await db.VoorraadAlerts.IgnoreQueryFilters().Where(a => a.TypeLijstId == lijstId).ExecuteDeleteAsync();
        db.TypeLijsten.Remove(lijst);
        await db.SaveChangesAsync();
    }
}
