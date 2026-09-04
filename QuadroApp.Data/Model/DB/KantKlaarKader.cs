using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    /// <summary>US-58 — reeds afgewerkt kader (besteld bij een leverancier, niet zelf gesneden),
    /// met een vaste stukprijs. Bewust een aparte, kleine tabel i.p.v. <see cref="TypeLijst"/>
    /// hergebruiken: geen voorraad, geen leveranciers-koppeling, geen bestel-integratie.</summary>
    public class KantKlaarKader
    {
        public int Id { get; set; }

        [MaxLength(200)]
        [Required]
        public string Naam { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal BreedteCm { get; set; }

        [Precision(18, 2)]
        public decimal HoogteCm { get; set; }

        [Precision(18, 2)]
        public decimal PrijsPerStukExcl { get; set; }

        /// <summary>True = gearchiveerd. Wordt door de globale query filter uitgesloten.</summary>
        public bool IsGearchiveerd { get; set; } = false;
    }
}
