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

    /// <summary>Hoe de lijst besteld wordt bij de leverancier.</summary>
    public enum BestelVorm
    {
        Verstek = 0,
        InLengte = 1,
        Gemonteerd = 2
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

        /// <summary>Bestelwijze: in verstek, in lengte of gemonteerd.</summary>
        public BestelVorm BestelVorm { get; set; } = BestelVorm.Verstek;

        [MaxLength(2000)]
        public string? Opmerking { get; set; }

        [NotMapped]
        public decimal? OntvangstInputMeter { get; set; }

        [NotMapped]
        public decimal ResterendTeOntvangenMeter => AantalMeterBesteld - AantalMeterOntvangen;

        // ── Eenheid-bewuste weergave ──────────────────────────────────────────────
        // Verstek & In lengte → meter ("m"); Gemonteerd → stuks/kaders ("st").
        // De hoeveelheid wordt in hetzelfde veld bewaard; alleen de eenheid verschilt.
        [NotMapped]
        public bool IsGemonteerd => BestelVorm == BestelVorm.Gemonteerd;

        [NotMapped]
        public string EenheidKort => IsGemonteerd ? "st" : "m";

        [NotMapped]
        public string BestelVormLabel => BestelVorm switch
        {
            BestelVorm.InLengte => "In lengte",
            BestelVorm.Gemonteerd => "Gemonteerd",
            _ => "In verstek"
        };

        [NotMapped]
        public string BesteldLabel => $"Besteld: {AantalMeterBesteld:0.##} {EenheidKort}";

        [NotMapped]
        public string OntvangenLabel => $"Ontvangen: {AantalMeterOntvangen:0.##} {EenheidKort}";

        [NotMapped]
        public string ResterendLabel => $"Resterend: {ResterendTeOntvangenMeter:0.##} {EenheidKort}";

        public ICollection<VoorraadMutatie> VoorraadMutaties { get; set; } = new List<VoorraadMutatie>();
    }
}
