using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    public class TypeLijst
    {
        public int Id { get; set; }

        [MaxLength(20)]
        [Required]
        public string Artikelnummer { get; set; } = string.Empty;

        [MaxLength(50)]
        [Required]
        public string Levcode { get; set; } = null!;

        [Required]
        public int LeverancierId { get; set; }
        public Leverancier Leverancier { get; set; } = null!;

        [Required]
        public int BreedteCm { get; set; }
        public string Soort { get; set; } = string.Empty;
        public bool IsDealer { get; set; }

        public string Opmerking { get; set; } = string.Empty;

        [Precision(10, 2)]
        public decimal PrijsPerMeter { get; set; }

        /// <summary>Winstfactor voor deze lijst. Null = gebruik de globale DefaultWinstFactor uit Instellingen.</summary>
        [Precision(6, 3)]
        public decimal? WinstFactor { get; set; }

        /// <summary>Afvalpercentage voor deze lijst. Null = gebruik de globale DefaultAfvalPercentage uit Instellingen.</summary>
        [Precision(5, 2)]
        public decimal? AfvalPercentage { get; set; }

        [Precision(10, 2)]
        public decimal VasteKost { get; set; }

        public int WerkMinuten { get; set; }

        [Precision(10, 2)]
        public decimal VoorraadMeter { get; set; }

        [Precision(10, 2)]
        public decimal GereserveerdeVoorraadMeter { get; set; }

        [Precision(10, 2)]
        public decimal InBestellingMeter { get; set; }

        [Precision(10, 2)]
        public decimal InventarisKost { get; set; }

        public DateTime LaatsteUpdate { get; set; }
        public DateTime? LaatsteVoorraadCheckOp { get; set; }

        public TypeLijst()
        {
            LaatsteUpdate = DateTime.Now;
        }


        [Precision(10, 2)]
        public decimal MinimumVoorraad { get; set; }

        [Precision(10, 2)]
        public decimal? HerbestelNiveauMeter { get; set; }

        [NotMapped]
        public decimal BeschikbareVoorraadMeter => Math.Max(0m, VoorraadMeter - GereserveerdeVoorraadMeter);

        [NotMapped]
        public decimal EffectiefHerbestelNiveauMeter => HerbestelNiveauMeter ?? MinimumVoorraad;

        [NotMapped]
        public double VoorraadStatusRatio =>
            MinimumVoorraad > 0m && BeschikbareVoorraadMeter < MinimumVoorraad ? 1.0 :
            EffectiefHerbestelNiveauMeter > 0m && BeschikbareVoorraadMeter <= EffectiefHerbestelNiveauMeter ? 0.75 :
            InBestellingMeter > 0m ? 0.5 :
            0.25;

        [NotMapped]
        public string VoorraadStatusLabel =>
            MinimumVoorraad > 0m && BeschikbareVoorraadMeter < MinimumVoorraad ? "Onder minimum" :
            EffectiefHerbestelNiveauMeter > 0m && BeschikbareVoorraadMeter <= EffectiefHerbestelNiveauMeter ? "Bijna op" :
            InBestellingMeter > 0m ? "In bestelling" :
            "Op voorraad";

        public ICollection<LeverancierBestelLijn> LeverancierBestelLijnen { get; set; } = new List<LeverancierBestelLijn>();
        public ICollection<VoorraadMutatie> VoorraadMutaties { get; set; } = new List<VoorraadMutatie>();
        public ICollection<VoorraadAlert> VoorraadAlerts { get; set; } = new List<VoorraadAlert>();

    }
}
