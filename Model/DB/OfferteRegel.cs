using System;
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

        /// <summary>Vrije titel die op de bestelbon/factuur verschijnt (bv "Poster A3", "Trouwfoto").
        /// Als leeg → valt terug op TypeLijst.Artikelnummer.</summary>
        [MaxLength(200)]
        public string? Titel { get; set; }

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
        // AFWERKING VARIANTEN (los van de optie; bv. "Brons", "Zwart")
        // Optioneel: een regel kan een afwerkingsoptie hebben zonder gekozen variant.
        // ========================

        private AfwerkingsVariant? _glasVariant;
        public int? GlasVariantId { get; set; }
        public AfwerkingsVariant? GlasVariant
        {
            get => _glasVariant;
            set { _glasVariant = value; GlasVariantId = value?.Id; }
        }

        private AfwerkingsVariant? _passe1Variant;
        public int? PassePartout1VariantId { get; set; }
        public AfwerkingsVariant? PassePartout1Variant
        {
            get => _passe1Variant;
            set { _passe1Variant = value; PassePartout1VariantId = value?.Id; }
        }

        private AfwerkingsVariant? _passe2Variant;
        public int? PassePartout2VariantId { get; set; }
        public AfwerkingsVariant? PassePartout2Variant
        {
            get => _passe2Variant;
            set { _passe2Variant = value; PassePartout2VariantId = value?.Id; }
        }

        private AfwerkingsVariant? _diepteVariant;
        public int? DiepteKernVariantId { get; set; }
        public AfwerkingsVariant? DiepteKernVariant
        {
            get => _diepteVariant;
            set { _diepteVariant = value; DiepteKernVariantId = value?.Id; }
        }

        private AfwerkingsVariant? _opklevenVariant;
        public int? OpklevenVariantId { get; set; }
        public AfwerkingsVariant? OpklevenVariant
        {
            get => _opklevenVariant;
            set { _opklevenVariant = value; OpklevenVariantId = value?.Id; }
        }

        private AfwerkingsVariant? _rugVariant;
        public int? RugVariantId { get; set; }
        public AfwerkingsVariant? RugVariant
        {
            get => _rugVariant;
            set { _rugVariant = value; RugVariantId = value?.Id; }
        }

        // ========================
        // EXTRA
        // ========================

        public int ExtraWerkMinuten { get; set; } = 0;
        public decimal ExtraPrijs { get; set; } = 0m;
        public decimal Korting { get; set; } = 0m;

        [MaxLength(6)]
        public string? LegacyCode { get; set; }

        /// <summary>Gewenste afhaal datum voor dit specifieke werkstuk.
        /// Elke regel kan een eigen afhaal datum hebben (bv. niet alles klaar op hetzelfde moment).</summary>
        public DateTime? AfhaalDatum { get; set; }

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