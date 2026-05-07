using QuadroApp.Model.DB;
using QuadroApp.Model.Snapshot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IOfferteArchiefService
    {
        /// <summary>
        /// Archiveert de opgegeven offerte (incl. werkbon, regels, taken, klant).
        /// Verwijdert de originele offerte uit de actieve lijst.
        /// </summary>
        Task<OfferteArchief> ArchiveerAsync(int offerteId, string? reden = null);

        /// <summary>
        /// Herstelt een archief-entry als een nieuwe offerte (Concept-status).
        /// Markeert het archief als hersteld.
        /// Geeft het ID van de nieuwe offerte terug.
        /// </summary>
        Task<int> HerstellenAsync(int archiefId);

        /// <summary>Haalt alle archief-entries op voor een specifiek jaar (0 = alle jaren).</summary>
        Task<List<OfferteArchief>> GetAlleAsync(int jaar = 0);

        /// <summary>Haal beschikbare archiefjaren op (voor de jaar-filter dropdown).</summary>
        Task<List<int>> GetJarenAsync();

        /// <summary>Haalt de volledige snapshot op voor een archief-entry.</summary>
        Task<OfferteArchiefSnapshot?> GetSnapshotAsync(int archiefId);

        /// <summary>
        /// Verwijdert een archief-entry permanent (geen herstel mogelijk).
        /// </summary>
        Task VerwijderenAsync(int archiefId);
    }
}
