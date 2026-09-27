using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-67 — "overzicht betalingen": alle ontvangsten (voorschotten, betalingen op bestelbons,
/// winkelverkopen) van een periode, per dag en per betaalwijze. Vervangt de oude afdrukken
/// "per dag detail" en "per maand dagtotalen" uit de kassa.</summary>
public interface IBetalingsOverzichtService
{
    /// <summary>Overzicht van <paramref name="van"/> t.e.m. <paramref name="tot"/> (datums inclusief).</summary>
    Task<BetalingsOverzicht> GetOverzichtAsync(DateTime van, DateTime tot);
}

/// <summary>Eén ontvangst in het overzicht.</summary>
public sealed record BetalingsOverzichtRegel(
    int OntvangstId,
    DateTime Datum,
    OntvangstSoort Soort,
    Betaalwijze Betaalwijze,
    decimal Bedrag,
    string Bon,
    int? Jaar,
    string? Omschrijving)
{
    public string SoortLabel => Soort switch
    {
        OntvangstSoort.Voorschot => "Voorschot",
        OntvangstSoort.Betaling => "Betaling",
        OntvangstSoort.Winkelverkoop => "Winkelverkoop",
        _ => Soort.ToString()
    };

    public decimal Kontant => Betaalwijze == Betaalwijze.Kontant ? Bedrag : 0m;
    public decimal Cheque => Betaalwijze == Betaalwijze.Cheque ? Bedrag : 0m;
    public decimal Visa => Betaalwijze == Betaalwijze.Visa ? Bedrag : 0m;
    public decimal Bancontact => Betaalwijze == Betaalwijze.Bancontact ? Bedrag : 0m;
    public decimal Proton => Betaalwijze == Betaalwijze.Proton ? Bedrag : 0m;
    public decimal Storting => Betaalwijze == Betaalwijze.Storting ? Bedrag : 0m;
}

/// <summary>Totalen per betaalwijze (basis voor een dag en voor de hele periode).</summary>
public abstract class BetaalwijzeTotalen
{
    /// <summary>Kolomvolgorde zoals op de oude afdrukken van de kassa.</summary>
    public static readonly IReadOnlyList<Betaalwijze> KolomVolgorde = new[]
    {
        Betaalwijze.Kontant, Betaalwijze.Cheque, Betaalwijze.Visa,
        Betaalwijze.Bancontact, Betaalwijze.Proton, Betaalwijze.Storting
    };

    private readonly Dictionary<Betaalwijze, decimal> _perBetaalwijze;

    protected BetaalwijzeTotalen(IEnumerable<BetalingsOverzichtRegel> regels)
    {
        _perBetaalwijze = KolomVolgorde.ToDictionary(b => b, _ => 0m);
        foreach (var r in regels)
            _perBetaalwijze[r.Betaalwijze] = _perBetaalwijze.GetValueOrDefault(r.Betaalwijze) + r.Bedrag;
        Totaal = _perBetaalwijze.Values.Sum();
    }

    public decimal Voor(Betaalwijze betaalwijze) => _perBetaalwijze.GetValueOrDefault(betaalwijze);

    public decimal Kontant => Voor(Betaalwijze.Kontant);
    public decimal Cheque => Voor(Betaalwijze.Cheque);
    public decimal Visa => Voor(Betaalwijze.Visa);
    public decimal Bancontact => Voor(Betaalwijze.Bancontact);
    public decimal Proton => Voor(Betaalwijze.Proton);
    public decimal Storting => Voor(Betaalwijze.Storting);
    public decimal Totaal { get; }
}

/// <summary>Alle ontvangsten van één dag.</summary>
public sealed class BetalingsOverzichtDag : BetaalwijzeTotalen
{
    private static readonly string[] DagNamen = { "zo", "ma", "di", "wo", "do", "vr", "za" };

    public BetalingsOverzichtDag(DateTime datum, IReadOnlyList<BetalingsOverzichtRegel> regels) : base(regels)
    {
        Datum = datum.Date;
        Regels = regels;
    }

    public DateTime Datum { get; }
    public IReadOnlyList<BetalingsOverzichtRegel> Regels { get; }

    /// <summary>Korte dagnaam zoals op de afdruk: ma, di, wo, do, vr, za, zo.</summary>
    public string DagNaam => DagNamen[(int)Datum.DayOfWeek];
}

/// <summary>Het volledige overzicht van een periode.</summary>
public sealed class BetalingsOverzicht : BetaalwijzeTotalen
{
    public BetalingsOverzicht(DateTime van, DateTime tot, IReadOnlyList<BetalingsOverzichtDag> dagen)
        : base(dagen.SelectMany(d => d.Regels))
    {
        Van = van.Date;
        Tot = tot.Date;
        Dagen = dagen;
    }

    public DateTime Van { get; }
    public DateTime Tot { get; }
    public IReadOnlyList<BetalingsOverzichtDag> Dagen { get; }
    public int AantalOntvangsten => Dagen.Sum(d => d.Regels.Count);

    /// <summary>Groepeert losse regels per dag (alleen dagen met ontvangsten), oudste dag eerst.</summary>
    public static BetalingsOverzicht Bouw(DateTime van, DateTime tot, IEnumerable<BetalingsOverzichtRegel> regels)
    {
        var dagen = regels
            .Where(r => r.Datum.Date >= van.Date && r.Datum.Date <= tot.Date)
            .GroupBy(r => r.Datum.Date)
            .OrderBy(g => g.Key)
            .Select(g => new BetalingsOverzichtDag(g.Key, g
                .OrderBy(r => r.Bon == string.Empty ? 1 : 0)
                .ThenBy(r => r.Bon, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.OntvangstId)
                .ToList()))
            .ToList();
        return new BetalingsOverzicht(van, tot, dagen);
    }
}
