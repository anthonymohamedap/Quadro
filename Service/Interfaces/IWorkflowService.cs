using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IWorkflowService
    {
        Task ChangeOfferteStatusAsync(int offerteId, OfferteStatus newStatus);
        Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus newStatus);
    }
}
