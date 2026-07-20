using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public enum VoorraadMutatieType
    {
        Reserve = 0,
        Release = 1,
        Receipt = 2,
        Consume = 3,
        Correction = 4
    }

    public class VoorraadMutatie
    {
        public int Id { get; set; }

        public int TypeLijstId { get; set; }
        public TypeLijst TypeLijst { get; set; } = null!;

        public VoorraadMutatieType MutatieType { get; set; }

        [Precision(10, 2)]
        public decimal AantalMeter { get; set; }

        public DateTime MutatieDatum { get; set; } = DateTime.UtcNow;

        public int? WerkBonId { get; set; }
        public WerkBon? WerkBon { get; set; }

        public int? WerkTaakId { get; set; }
        public WerkTaak? WerkTaak { get; set; }

        public int? LeverancierBestelLijnId { get; set; }
        public LeverancierBestelLijn? LeverancierBestelLijn { get; set; }

        [MaxLength(120)]
        public string? Referentie { get; set; }

        [MaxLength(2000)]
        public string? Opmerking { get; set; }
    }
}
