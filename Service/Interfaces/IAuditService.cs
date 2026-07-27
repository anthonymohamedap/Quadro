using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroApp.Model.DB;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-40 — filter voor het auditlogboek. Datums zijn LOKALE dagen (inclusief).</summary>
public sealed record AuditFilter(
    string? Gebruiker = null,
    string? EntiteitType = null,
    string? Actie = null,
    DateTime? VanafLokaal = null,
    DateTime? TotLokaal = null);

/// <summary>Eén pagina auditrecords + het totaal (voor paginering).</summary>
public sealed record AuditPagina(IReadOnlyList<AuditLog> Records, int TotaalAantal);

/// <summary>US-40 — alleen-lezen toegang tot het auditlogboek (admin-only).</summary>
public interface IAuditService
{
    /// <summary>
    /// Gefilterd + gepagineerd, nieuwste eerst. Vereist <c>Permissie.AuditInzien</c>.
    /// </summary>
    Task<AuditPagina> ZoekAsync(AuditFilter filter, int skip, int take);

    /// <summary>Distinct waarden voor de filter-keuzelijsten (gebruikers, types, acties).</summary>
    Task<(IReadOnlyList<string> Gebruikers, IReadOnlyList<string> Types, IReadOnlyList<string> Acties)>
        GetFilterOptiesAsync();
}
