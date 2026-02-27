using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    public class OfferteRegel
    {
        public int Id { get; set; }

        // ========================
        // Parent
        // ========================

        public int OfferteId { get; set; }
        public Offerte? Offerte { get; set; }

        [MaxLength(500)]
        public string? Opmerking { get; set; }

        // ========================
        // Basis invoer
        // ========================

        [Range(1, 9999)]
        public int AantalStuks { get; set; } = 1;

        public decimal BreedteCm { get; set; }
        public decimal HoogteCm { get; set; }

        public decimal? InlegBreedteCm { get; set; }
        public decimal? InlegHoogteCm { get; set; }

        // ========================
        // TYPE LIJST
        // ========================

        private TypeLijst? _typeLijst;

        public int? TypeLijstId { get; set; }

        public TypeLijst? TypeLijst
        {
            get => _typeLijst;
            set
            {
                _typeLijst = value;
                TypeLijstId = value?.Id;
            }
        }

        // ========================
        // AFWERKINGEN (ALLEMAAL SYNCHROON)
        // ========================

        private AfwerkingsOptie? _glas;
        public int? GlasId { get; set; }
        public AfwerkingsOptie? Glas
        {
            get => _glas;
            set
            {
                _glas = value;
                GlasId = value?.Id;
            }
        }

        private AfwerkingsOptie? _passe1;
        public int? PassePartout1Id { get; set; }
        public AfwerkingsOptie? PassePartout1
        {
            get => _passe1;
            set
            {
                _passe1 = value;
                PassePartout1Id = value?.Id;
            }
        }

        private AfwerkingsOptie? _passe2;
        public int? PassePartout2Id { get; set; }
        public AfwerkingsOptie? PassePartout2
        {
            get => _passe2;
            set
            {
                _passe2 = value;
                PassePartout2Id = value?.Id;
            }
        }

        private AfwerkingsOptie? _diepte;
        public int? DiepteKernId { get; set; }
        public AfwerkingsOptie? DiepteKern
        {
            get => _diepte;
            set
            {
                _diepte = value;
                DiepteKernId = value?.Id;
            }
        }

        private AfwerkingsOptie? _opkleven;
        public int? OpklevenId { get; set; }
        public AfwerkingsOptie? Opkleven
        {
            get => _opkleven;
            set
            {
                _opkleven = value;
                OpklevenId = value?.Id;
            }
        }

        private AfwerkingsOptie? _rug;
        public int? RugId { get; set; }
        public AfwerkingsOptie? Rug
        {
            get => _rug;
            set
            {
                _rug = value;
                RugId = value?.Id;
            }
        }

        // ========================
        // EXTRA
        // ========================

        public int ExtraWerkMinuten { get; set; } = 0;
        public decimal ExtraPrijs { get; set; } = 0m;
        public decimal Korting { get; set; } = 0m;

        [MaxLength(6)]
        public string? LegacyCode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AfgesprokenPrijsExcl { get; set; }

        // ========================
        // PRIJZEN
        // ========================

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotaalExcl { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotaalExBtw { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BtwBedrag { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotaalInclBtw { get; set; }
    }
}