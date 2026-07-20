using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IPricingService
    {
        Task BerekenAsync(int offerteId);
        Task BerekenAsync(Offerte offerte);
    }
}
