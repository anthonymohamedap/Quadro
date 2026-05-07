using QuadroApp.Model.DB;
using QuadroApp.Model.Snapshot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IWerkBonArchiefService
    {
        /// <summary>
        /// Maakt een archief-entry aan voor de werkbon die bij de gegeven offerte hoort.
        /// Slaat een volledige JSON-snapshot op voor recovery.
        /// Gooit InvalidOperationException als er geen werkbon gevonden wordt.
        /// </summary>
        Task<WerkBonArchief> ArchiveerAsync(int werkBonId, string? annuleringsReden = null);

        /// <summary>
        /// Herstelt een gearchiveerde werkbon als een nieuwe offerte.
        /// Kloont de offerteregels en klantdata uit de snapshot.
        /// Markeert het archief als hersteld.
        /// </summary>
        Task<int> HerstellenAsync(int archiefId);

        /// <summary>Haalt alle archief-entries op, meest recent eerst.</summary>
        Task<List<WerkBonArchief>> GetAlleAsync();

        /// <summary>Haalt snapshot-details op voor een specifiek archief-entry.</summary>
        Task<WerkBonArchiefSnapshot?> GetSnapshotAsync(int archiefId);
    }
}
