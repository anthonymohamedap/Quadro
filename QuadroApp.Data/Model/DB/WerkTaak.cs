using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public enum VoorraadStatus
    {
        Unknown = 0,
        Reserved = 1,
        Shortage = 2,
        Ordered = 3,
        Ready = 4
    }

    [Index(nameof(GeplandVan))]
    [Index(nameof(WerkBonId))]
    public class WerkTaak
    {
        public int Id { get; set; }

        public int WerkBonId { get; set; }
        public WerkBon WerkBon { get; set; } = null!;

        public int? OfferteRegelId { get; set; }
        public OfferteRegel? OfferteRegel { get; set; }

        // Tip: hou het bij lokale tijd in je app; als je ooit timezones nodig hebt, migreer naar DateTimeOffset.
        public DateTime GeplandVan { get; set; }   // start (local)
        public DateTime GeplandTot { get; set; }   // einde (local)

        public int DuurMinuten { get; set; }       // bewaak met check-constraint

        [MaxLength(200)]
        public string Omschrijving { get; set; } = string.Empty;

        // Optioneel: wie voert het uit?
        [MaxLength(80)]
        public string? Resource { get; set; }

        // ✅ Nieuw: notitie die op weeklijst getoond en bewaard wordt
        [MaxLength(2000)]
        public string? WeekNotitie { get; set; }

        public bool IsBesteld { get; set; }
        public DateTime? BestelDatum { get; set; }
        public bool IsOpVoorraad { get; set; }
        public VoorraadStatus VoorraadStatus { get; set; } = VoorraadStatus.Unknown;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool KanBesteldWorden => !IsBesteld;

        /// <summary>US-51 — er is pas écht een bestelling nodig als de lijst niet al besteld is
        /// én niet op voorraad ligt. Voorkomt de tegenstrijdige "op voorraad + bestelling vereist".</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool MoetBesteldWorden => !IsBesteld && !IsOpVoorraad;

        /// <summary>US-51 — keuzelijst voor de bestelwijze in de UI.</summary>
        private static readonly BestelVorm[] _bestelVormOpties = Enum.GetValues<BestelVorm>();
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public IReadOnlyList<BestelVorm> BestelVormOpties => _bestelVormOpties;

        /// <summary>Bestelwijze geselecteerd door de gebruiker in de UI — niet opgeslagen in DB,
        /// wordt doorgegeven bij het aanmaken van de bestelling.</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public BestelVorm GeselecteerdeBestelVorm { get; set; } = BestelVorm.Verstek;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool BestelVormIsVerstek
        {
            get => GeselecteerdeBestelVorm == BestelVorm.Verstek;
            set { if (value) GeselecteerdeBestelVorm = BestelVorm.Verstek; }
        }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool BestelVormIsInLengte
        {
            get => GeselecteerdeBestelVorm == BestelVorm.InLengte;
            set { if (value) GeselecteerdeBestelVorm = BestelVorm.InLengte; }
        }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool BestelVormIsGemonteerd
        {
            get => GeselecteerdeBestelVorm == BestelVorm.Gemonteerd;
            set { if (value) GeselecteerdeBestelVorm = BestelVorm.Gemonteerd; }
        }

        public int? LeverancierBestelLijnId { get; set; }
        public LeverancierBestelLijn? LeverancierBestelLijn { get; set; }

        [Precision(10, 2)]
        public decimal BenodigdeMeter { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
