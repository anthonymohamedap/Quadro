using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.Service;

/// <summary>US-40 — alleen-lezen toegang tot het auditlogboek (admin-only).</summary>
public sealed class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;

    public AuditService(IDbContextFactory<AppDbContext> factory, IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    public async Task<AuditPagina> ZoekAsync(AuditFilter filter, int skip, int take)
    {
        _auth.VereisPermissie(Permissie.AuditInzien);
        await using var db = await _factory.CreateDbContextAsync();

        var q = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Gebruiker))
            q = q.Where(a => a.Gebruiker == filter.Gebruiker);
        if (!string.IsNullOrWhiteSpace(filter.EntiteitType))
            q = q.Where(a => a.EntiteitType == filter.EntiteitType);
        if (!string.IsNullOrWhiteSpace(filter.Actie))
            q = q.Where(a => a.Actie == filter.Actie);

        // Filterdatums zijn lokale dagen; Tijdstip staat in UTC → grenzen naar UTC omzetten.
        if (filter.VanafLokaal is DateTime van)
        {
            var vanUtc = DateTime.SpecifyKind(van.Date, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.Tijdstip >= vanUtc);
        }
        if (filter.TotLokaal is DateTime tot)
        {
            var totUtc = DateTime.SpecifyKind(tot.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.Tijdstip < totUtc);
        }

        var totaal = await q.CountAsync();
        var records = await q
            .OrderByDescending(a => a.Tijdstip)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return new AuditPagina(records, totaal);
    }

    public async Task<(IReadOnlyList<string> Gebruikers, IReadOnlyList<string> Types, IReadOnlyList<string> Acties)>
        GetFilterOptiesAsync()
    {
        _auth.VereisPermissie(Permissie.AuditInzien);
        await using var db = await _factory.CreateDbContextAsync();

        var gebruikers = await db.AuditLogs.AsNoTracking()
            .Select(a => a.Gebruiker).Distinct().OrderBy(x => x).ToListAsync();
        var types = await db.AuditLogs.AsNoTracking()
            .Select(a => a.EntiteitType).Distinct().OrderBy(x => x).ToListAsync();
        var acties = await db.AuditLogs.AsNoTracking()
            .Select(a => a.Actie).Distinct().OrderBy(x => x).ToListAsync();

        return (gebruikers, types, acties);
    }
}
