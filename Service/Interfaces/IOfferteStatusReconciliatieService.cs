using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

/// <summary>
/// US-48 — eenmalige/herhaalbare reconciliatie van bestaande offertestatussen op basis van
/// hun werkbon + bestelbon. Corrigeert offertes die vóór US-42 nooit zijn bijgewerkt.
/// </summary>
public interface IOfferteStatusReconciliatieService
{
    /// <summary>
    /// Werkt alle offertes bij naar de status die volgt uit hun werkbon/bestelbon.
    /// Alleen vooruit; Geannuleerde offertes blijven ongemoeid. Idempotent.
    /// Retourneert het aantal gewijzigde offertes. Vereist <c>Permissie.GebruikersBeheren</c>.
    /// </summary>
    Task<int> ReconcilieerAlleAsync();
}
