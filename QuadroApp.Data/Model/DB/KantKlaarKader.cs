using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>US-58 — reeds volledig afgewerkt kader (besteld bij een leverancier, niet zelf
    /// gesneden of afgewerkt), met een vaste stukprijs. Gewoon een artikel: geen afmeting (geen
    /// afwerkingen zoals glas/passe-partout komen er nog bovenop — het kader is al af), geen
    /// voorraad, geen leveranciers-koppeling, geen bestel-integratie.</summary>
    public class KantKlaarKader
    {
        public int Id { get; set; }

        [MaxLength(200)]
        [Required]
        public string Naam { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Beschrijving { get; set; }

        [Precision(18, 2)]
        public decimal PrijsPerStukExcl { get; set; }

        /// <summary>True = gearchiveerd. Wordt door de globale query filter uitgesloten.</summary>
        public bool IsGearchiveerd { get; set; } = false;

        /// <summary>US-80 — "Naam – Beschrijving" (bv. "C2 – Alu zwart 50×70"); enkel de naam als er geen beschrijving is.</summary>
        [NotMapped]
        public string Weergave => string.IsNullOrWhiteSpace(Beschrijving) ? Naam : $"{Naam} – {Beschrijving.Trim()}";
    }
}
