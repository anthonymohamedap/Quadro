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
}
