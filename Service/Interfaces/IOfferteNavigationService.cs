using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IOfferteNavigationService
    {
        Task OpenOfferteAsync(int offerteId);

        /// <summary>
        /// US-95 — een bestaande verkoop openen als verkoopdossier (de standaard ingang). Het oude
        /// offertescherm blijft bereikbaar via <see cref="OpenOfferteAsync"/> (menu ⋯ in het dossier).
        /// </summary>
        Task OpenVerkoopAsync(int offerteId) => OpenOfferteAsync(offerteId);
        Task NewOfferteAsync();
    }
}
