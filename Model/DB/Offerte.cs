using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    public class Offerte
    {
        public int Id { get; set; }

        // Nieuw: klant-koppeling
        public int? KlantId { get; set; }
        public Klant? Klant { get; set; }

        // Verzameling regels
        public ICollection<OfferteRegel> Regels { get; set; } = new List<OfferteRegel>();

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubtotaalExBtw { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BtwBedrag { get; set; }

        public DateTime Datum { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotaalInclBtw { get; set; }

        // Optioneel: opmerking, status, geldigheidsdatum, …
        public string? Opmerking { get; set; }

        // ───────── NIEUW: Planning-velden ─────────

        /// <summary>Dag waarop je de offerte gaat uitwerken.</summary>
        public DateTime? GeplandeDatum { get; set; }

        /// <summary>Tegen wanneer de offerte klaar moet zijn.</summary>
        public DateTime? DeadlineDatum { get; set; }

        /// <summary>Hoeveel minuten werk je inschat voor deze offerte.</summary>
        public int? GeschatteMinuten { get; set; }

        /// <summary>Status in het offertetraject.</summary>
        public OfferteStatus Status { get; set; } = OfferteStatus.Concept;

        /// <summary>Gekoppelde bestelling (WerkBon) — kan null zijn.</summary>
        public WerkBon? WerkBon { get; set; }
        // 🔹 Korting op volledige offerte (excl btw)
        [Column(TypeName = "decimal(18,2)")]
        public decimal KortingPct { get; set; } = 0m;   // bv 10 = 10%

        // 🔹 Meerprijs ingegeven INCL btw (bv spoedkost)
        [Column(TypeName = "decimal(18,2)")]
        public decimal MeerPrijsIncl { get; set; } = 0m;

        // 🔹 Voorschot
        public bool IsVoorschotBetaald { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal VoorschotBedrag { get; set; } = 0m;

        // 🔹 Restbedrag (niet in DB nodig maar mag)
        [NotMapped]
        public decimal RestTeBetalen =>
            TotaalInclBtw - (IsVoorschotBetaald ? VoorschotBedrag : 0m);
    }
}
