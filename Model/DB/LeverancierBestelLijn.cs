using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    public enum LeverancierBestelRedenType
    {
        TekortWerkTaak = 0,
        MinimumVoorraadAanvulling = 1,
        Correctie = 2
    }

    public class LeverancierBestelLijn
    {
        public int Id { get; set; }

        public int LeverancierBestellingId { get; set; }
        public LeverancierBestelling LeverancierBestelling { get; set; } = null!;

        public int TypeLijstId { get; set; }
        public TypeLijst TypeLijst { get; set; } = null!;

        public int? WerkBonId { get; set; }
        public WerkBon? WerkBon { get; set; }

        [Precision(10, 2)]
        public decimal AantalMeterBesteld { get; set; }

        [Precision(10, 2)]
        public decimal AantalMeterOntvangen { get; set; }

        public LeverancierBestelRedenType RedenType { get; set; } = LeverancierBestelRedenType.TekortWerkTaak;

        [MaxLength(2000)]
        public string? Opmerking { get; set; }

        [NotMapped]
        public decimal? OntvangstInputMeter { get; set; }

        [NotMapped]
        public decimal ResterendTeOntvangenMeter => AantalMeterBesteld - AantalMeterOntvangen;

        public ICollection<VoorraadMutatie> VoorraadMutaties { get; set; } = new List<VoorraadMutatie>();
    }
}
