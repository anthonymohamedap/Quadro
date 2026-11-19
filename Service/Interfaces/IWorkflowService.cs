using QuadroApp.Model.DB;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IWorkflowService
    {
        Task ChangeOfferteStatusAsync(int offerteId, OfferteStatus newStatus);

        /// <summary>US-87 — annuleren met een reden (wordt ook bij het gearchiveerde werkbon-snapshot bewaard).</summary>
        Task AnnuleerOfferteAsync(int offerteId, string reden);
        Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus newStatus);
        Task ReserveStockForWerkBonAsync(int werkBonId);
        Task MarkLijstAsBesteldAsync(int werkTaakId, DateTime bestelDatum, BestelVorm bestelVorm = BestelVorm.Verstek);
    }
}
