using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.Service.Foutlog;

/// <summary>Leest de foutmeldingen (crashes) voor het beheerdersscherm. Enkel voor wie het auditlog mag inzien.</summary>
public interface IFoutLogService
{
    Task<List<FoutLog>> LijstAsync(bool enkelNietBekeken, CancellationToken ct = default);
    Task MarkeerBekekenAsync(int id, CancellationToken ct = default);
}

public sealed class FoutLogService : IFoutLogService
{
    private const int MaxRijen = 500;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;

    public FoutLogService(IDbContextFactory<AppDbContext> factory, IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    public async Task<List<FoutLog>> LijstAsync(bool enkelNietBekeken, CancellationToken ct = default)
    {
        _auth.VereisPermissie(Permissie.AuditInzien);
        await using var db = await _factory.CreateDbContextAsync(ct);
        IQueryable<FoutLog> q = db.FoutLogs.AsNoTracking();
        if (enkelNietBekeken) q = q.Where(x => !x.IsBekeken);
        return await q.OrderByDescending(x => x.Tijdstip).ThenByDescending(x => x.Id)
            .Take(MaxRijen).ToListAsync(ct);
    }

    public async Task MarkeerBekekenAsync(int id, CancellationToken ct = default)
    {
        _auth.VereisPermissie(Permissie.AuditInzien);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rij = await db.FoutLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rij is null || rij.IsBekeken) return;
        rij.IsBekeken = true;
        await db.SaveChangesAsync(ct);
    }
}
