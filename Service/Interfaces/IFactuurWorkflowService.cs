using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IFactuurWorkflowService
{
    Task<Factuur> MaakFactuurVanOfferteAsync(int offerteId);
    Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId);
    Task<Factuur?> GetFactuurAsync(int factuurId);
    Task<Factuur?> GetFactuurVoorOfferteAsync(int offerteId);
    Task MarkeerKlaarVoorExportAsync(int factuurId);
    Task MarkeerBetaaldAsync(int factuurId);
    Task SaveDraftAsync(Factuur factuur);
    Task HerberekenTotalenAsync(int factuurId);

    /// <summary>Een voorlopige bestelbon (zonder volgnummer) krijgt bij de bevestiging van de offerte zijn volgnummer.</summary>
    Task KenVolgnummerToeBijBevestigingAsync(int offerteId);
}
