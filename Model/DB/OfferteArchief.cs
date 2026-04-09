 using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>
    /// Archief-entry voor een volledige offerte incl. werkbon, regels, taken en klant.
    /// Aangemaakt vanuit de offertelijst. Kan worden hersteld als nieuwe offerte.
    /// </summary>
    [Index(nameof(Jaar))]
    [Index(nameof(OrigineleOfferteId))]
    [Index(nameof(GearchiveerdOp))]
    public class OfferteArchief
    {
        public int Id { get; set; }

        /// <summary>ID van de originele offerte (kan niet meer bestaan na archivering).</summary>
        public int OrigineleOfferteId { get; set; }

        // ── Querybare gedenormaliseerde velden voor lijst-weergave ──────────

        [MaxLength(200)]
        public string KlantNaam { get; set; } = string.Empty;

        public int? KlantId { get; set; }

        public DateTime OfferteDatum { get; set; }

        /// <summary>Jaar van de offertedatum — voor jaar-filter.</summary>
        public int Jaar { get; set; }

        [MaxLength(30)]
        public string StatusOpMoment { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotaalInclBtw { get; set; }

        /// <summary>Had de offerte een werkbon?</summary>
        public bool HadWerkBon { get; set; }

        public DateTime GearchiveerdOp { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Reden { get; set; }

        // ── Volledige JSON-snapshot voor recovery ──────────────────────────

        /// <summary>Volledig geserialiseerde snapshot (OfferteArchiefSnapshot als JSON).</summary>
        public string Snapshot { get; set; } = "{}";

        // ── Herstel-tracking ───────────────────────────────────────────────

        public bool IsHersteld { get; set; } = false;
        public int? HersteldNaarOfferteId { get; set; }
    }
}
