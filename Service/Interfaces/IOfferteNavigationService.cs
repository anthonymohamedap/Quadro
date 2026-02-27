using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public interface IOfferteNavigationService
    {
        Task OpenOfferteAsync(int offerteId);
        Task NewOfferteAsync();
    }
}
