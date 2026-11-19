using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IOfferteNavigationService
    {
        Task OpenOfferteAsync(int offerteId);
        Task NewOfferteAsync();
    }
}
