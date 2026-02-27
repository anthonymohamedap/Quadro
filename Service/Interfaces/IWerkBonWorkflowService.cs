using QuadroApp.Model.DB;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IWerkBonWorkflowService
    {
        Task<WerkBon> MaakWerkBonAsync(int offerteId);

        // (optioneel) oud: algemene taak op werkbon-niveau
        Task VoegPlanningToeAsync(int werkBonId, DateTime start, int duurMinuten, string? omschrijving = null);

        // ✅ nieuw: regel-basis planning (1 OfferteRegel = 1 WerkTaak)
        Task VoegPlanningToeVoorRegelAsync(
            int werkBonId,
            int offerteRegelId,
            DateTime dag,
            int duurMinuten,
            string? omschrijving = null);

        // (optioneel) herplannen: vervang/verplaats taken
        Task HerplanWerkBonRegelsAsync(
            int werkBonId,
            DateTime nieuweDag,
            int? startUur = 9,
            int? startMinuut = 0);

        Task VeranderStatusAsync(int werkBonId, WerkBonStatus nieuweStatus);
        Task MarkeerTaakAlsVoltooidAsync(int taakId);
    }
}
