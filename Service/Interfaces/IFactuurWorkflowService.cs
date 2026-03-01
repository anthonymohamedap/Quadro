using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IFactuurWorkflowService
{
    Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId);
    Task<Factuur?> GetFactuurAsync(int factuurId);
    Task MarkeerKlaarVoorExportAsync(int factuurId);
    Task MarkeerBetaaldAsync(int factuurId);
    Task SaveDraftAsync(Factuur factuur);
    Task HerberekenTotalenAsync(int factuurId);
}
