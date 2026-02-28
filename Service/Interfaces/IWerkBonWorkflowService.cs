using QuadroApp.Model.DB;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IWerkBonWorkflowService
    {
        Task<WerkBon> MaakWerkBonAsync(int offerteId);
        Task VoegPlanningToeAsync(int werkBonId, DateTime start, int duurMinuten, string? omschrijving = null);
        Task VoegPlanningToeVoorRegelAsync(int werkBonId, int offerteRegelId, DateTime dag, int duurMinuten, string? omschrijving = null);
        Task HerplanWerkBonRegelsAsync(int werkBonId, DateTime nieuweDag, int? startUur = 9, int? startMinuut = 0);
        Task VeranderStatusAsync(int werkBonId, WerkBonStatus nieuweStatus);
        Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus nieuweStatus);
        Task MarkeerTaakAlsVoltooidAsync(int taakId);
    }
}
