using System;
using Microsoft.EntityFrameworkCore;

namespace QuadroApp.Service.Concurrency;

/// <summary>
/// REL-03: helper voor consistente gebruikersmeldingen bij gelijktijdige wijzigingen.
/// </summary>
public static class ConcurrencyUx
{
    public const string StandaardMelding =
        "Iemand anders heeft dit record intussen gewijzigd. Vernieuw het scherm en probeer opnieuw.";

    /// <summary>
    /// Geeft een gebruiksvriendelijke melding terug wanneer <paramref name="ex"/> een
    /// concurrency-conflict is (direct, of als InnerException), anders null.
    /// </summary>
    public static string? Melding(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is GelijktijdigeWijzigingException gw) return gw.Message;
            if (e is DbUpdateConcurrencyException) return StandaardMelding;
        }
        return null;
    }

    /// <summary>True wanneer de exceptie (of een inner) een concurrency-conflict is.</summary>
    public static bool IsConflict(Exception? ex) => Melding(ex) is not null;
}
