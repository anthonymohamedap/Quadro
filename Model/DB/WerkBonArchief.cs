using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>
    /// Bevroren kopie van een geannuleerde WerkBon.
    /// Bevat zowel querybare velden (voor overzichtslijst) als
    /// een volledige JSON-snapshot (voor volledige recovery).
    /// </summary>
    [Index(nameof(GearchiveerdOp))]
    [Index(nameof(OrigineleWerkBonId))]
    [Index(nameof(OfferteId))]
    public class WerkBonArchief
    {
        public int Id { get; set; }

        // ── Traceerbaarheid ──────────────────────────────────────────
        /// <summary>Id van de originele WerkBon (kan inmiddels verwijderd zijn).</summary>
        public int OrigineleWerkBonId { get; set; }

        /// <summary>Id van de gekoppelde Offerte (voor terugkoppeling).</summary>
        public int OfferteId { get; set; }

        // ── Denormaliseerde velden voor snelle weergave ──────────────
        [MaxLength(200)]
        public string KlantNaam { get; set; } = string.Empty;

        public int? KlantId { get; set; }

        public DateTime OfferteDatum { get; set; }

        [MaxLength(30)]
        public string OfferteStatusOpMoment { get; set; } = string.Empty;

        [MaxLength(30)]
        public string WerkBonStatusOpMoment { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotaalPrijsIncl { get; set; }

        /// <summary>Wanneer gearchiveerd (= moment van annulering).</summary>
        public DateTime GearchiveerdOp { get; set; } = DateTime.UtcNow;

        /// <summary>Optionele reden voor annulering (ingegeven door gebruiker).</summary>
        [MaxLength(500)]
        public string? AnnuleringsReden { get; set; }

        // ── Volledige snapshot (JSON) ────────────────────────────────
        /// <summary>
        /// JSON-string met alle details: regels, afwerkingen, taken, klantdata.
        /// Gebruik WerkBonArchiefSnapshot voor deserialisatie.
        /// </summary>
        public string Snapshot { get; set; } = "{}";

        /// <summary>True als de werkbon hersteld is naar een nieuwe offerte.</summary>
        public bool IsHersteld { get; set; } = false;

        /// <summary>Id van de nieuwe Offerte als hersteld (voor audit trail).</summary>
        public int? HersteldNaarOfferteId { get; set; }
    }
}
