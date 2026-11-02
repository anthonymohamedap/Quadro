using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IBestelbonWorkflowService
{
    Task<Bestelbon> MaakBestelbonVanOfferteAsync(int offerteId);
    Task<Bestelbon> MaakBestelbonVanWerkBonAsync(int werkBonId);
    Task<Bestelbon?> GetBestelbonAsync(int bestelbonId);
    Task<Bestelbon?> GetBestelbonVoorOfferteAsync(int offerteId);
    Task MarkeerKlaarVoorExportAsync(int bestelbonId);
    Task MarkeerBetaaldAsync(int bestelbonId);
    Task SaveDraftAsync(Bestelbon bestelbon);
    Task HerberekenTotalenAsync(int bestelbonId);

    /// <summary>Een voorlopige bestelbon (zonder volgnummer) krijgt bij de bevestiging van de offerte zijn volgnummer.</summary>
    Task KenVolgnummerToeBijBevestigingAsync(int offerteId);
}
