using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IPricingService
    {
        Task BerekenAsync(int offerteId);
    }
}