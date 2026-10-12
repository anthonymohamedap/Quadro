using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-69/US-70 — registreren van ontvangsten (voorschotten, betalingen op een bestelbon)
/// in het centrale ontvangstenregister (<see cref="Ontvangst"/>).</summary>
public interface IOntvangstService
{
    /// <summary>Alle voorschotten van een offerte, nieuwste eerst.</summary>
    Task<List<Ontvangst>> GetVoorschottenAsync(int offerteId);

    /// <summary>Registreert een ontvangen voorschot en verhoogt <see cref="Offerte.VoorschotBedrag"/>
    /// (en dat van een bestaande bestelbon). Geeft het nieuwe totale voorschot terug.</summary>
    Task<decimal> RegistreerVoorschotAsync(int offerteId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze);

    /// <summary>Verwijdert een ontvangst. Bij een voorschot wordt het voorschotbedrag van offerte en
    /// bestelbon mee verlaagd. Geeft het nieuwe totale voorschot van de offerte terug (of null).</summary>
    Task<decimal?> VerwijderAsync(int ontvangstId);

    // ── US-70: betalingen op een bestelbon ──

    /// <summary>Betalingen (geen voorschotten) op een bestelbon, nieuwste eerst.</summary>
    Task<List<Ontvangst>> GetBetalingenAsync(int factuurId);

    /// <summary>Totaal, voorschot, reeds betaald en rest voor een bestelbon.</summary>
    Task<BetaalStand> GetBetaalStandAsync(int factuurId);

    /// <summary>Registreert een betaling op een bestelbon (vooraf, bij afhalen of achteraf; meerdere
    /// betalingen mogelijk, bv. deels cash en deels kaart). Is de rest daarna 0, dan wordt de
    /// bestelbon op Betaald gezet. Geeft de nieuwe stand terug.</summary>
    Task<BetaalStand> RegistreerBetalingAsync(int factuurId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze);

    // ── US-76: afrekenen bij afhaling vanuit de werkbon ──

    /// <summary>De bestelbon van een werkbon (via de werkbon of diens offerte) met zijn betaalstand,
    /// of null als er (nog) geen niet-geannuleerde bestelbon is.</summary>
    Task<AfrekenStand?> GetAfrekenStandAsync(int werkBonId);

    /// <summary>Registreert een betaling op de bestelbon van de werkbon én zet de werkbon op
    /// Afgehaald (als hij Afgewerkt was), in één keer: lukt één stap niet, dan verandert niets.
    /// Is de rest daarna 0, dan gaan bestelbon en offerte op Betaald. Een gesplitste betaling kan:
    /// nadien opnieuw aanroepen voor de rest (de werkbon blijft dan Afgehaald).</summary>
    Task<AfrekenStand> AfrekenenBijAfhalingAsync(int werkBonId, decimal bedragIncl, DateTime datum, Betaalwijze betaalwijze);
}

/// <summary>US-76 — bestelbon van een werkbon + zijn betaalstand.</summary>
public sealed record AfrekenStand(int FactuurId, string FactuurNummer, BetaalStand Stand);

/// <summary>US-70 — betaalstand van een bestelbon.</summary>
public sealed record BetaalStand(decimal TotaalIncl, decimal Voorschot, decimal Betaald, bool IsBetaaldStatus)
{
    /// <summary>Nog te betalen = totaal − voorschot − betalingen (nooit negatief).</summary>
    public decimal Rest => Math.Max(0m, Math.Round(TotaalIncl - Voorschot - Betaald, 2));
}
