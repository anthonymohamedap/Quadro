using QuadroApp.Model.DB;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IAfwerkingenService
    {
        Task<List<AfwerkingsGroep>> GetGroepenAsync();
        Task<List<Leverancier>> GetLeveranciersAsync();
        Task<List<AfwerkingsOptie>> GetOptiesAsync(int? groepId);

        Task SaveOptieAsync(AfwerkingsOptie optie);
        Task<AfwerkingsOptie> CreateNieuweOptieAsync(int groepId);

        /// <summary>
        /// Returns true when at least one OfferteRegel still references this optie
        /// via any of the 6 FK columns (Glas, PassePartout1/2, DiepteKern, Opkleven, Rug).
        /// Call before DeleteOptieAsync to show a friendly message instead of a DB error.
        /// </summary>
        Task<int> CountGebruikAsync(int optieId);

        /// <summary>
        /// Deletes the optie. Throws <see cref="InvalidOperationException"/> with a Dutch
        /// message when the optie is still referenced by one or more OfferteRegels.
        /// </summary>
        Task DeleteOptieAsync(AfwerkingsOptie optie);

        // ── Variant management ───────────────────────────────────────────────

        /// <summary>Returns all variants for the given optie, ordered by IsStandaard desc, then Beschrijving.</summary>
        Task<List<AfwerkingsVariant>> GetVariantenAsync(int optieId);

        /// <summary>Inserts or updates a single variant. New records (Id == 0) are inserted; existing ones are updated.</summary>
        Task<AfwerkingsVariant> SaveVariantAsync(AfwerkingsVariant variant);

        /// <summary>Deletes a variant by its Id. No-op if not found.</summary>
        Task DeleteVariantAsync(int variantId);
    }

}
