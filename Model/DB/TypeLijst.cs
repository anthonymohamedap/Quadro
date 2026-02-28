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
        public string? Serie { get; set; }
        public bool IsDealer { get; set; }

        public string Opmerking { get; set; } = string.Empty;

        [Precision(10, 2)]
        public decimal PrijsPerMeter { get; set; }

        [Precision(6, 3)]
        public decimal WinstMargeFactor { get; set; }

        [Precision(5, 2)]
        public decimal AfvalPercentage { get; set; }

        [Precision(10, 2)]
        public decimal VasteKost { get; set; }

        public int WerkMinuten { get; set; }

        [Precision(10, 2)]
        public decimal VoorraadMeter { get; set; }

        [Precision(10, 2)]
        public decimal InventarisKost { get; set; }

        public DateTime LaatsteUpdate { get; set; }

        public TypeLijst()
        {
            LaatsteUpdate = DateTime.Now;
        }


        [Precision(10, 2)]
        public decimal MinimumVoorraad { get; set; }

    }
}
