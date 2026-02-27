using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IOfferteWorkflowService
    {
        Task<int> BevestigAsync(int offerteId);
        Task AnnuleerAsync(int offerteId);
    }
}
