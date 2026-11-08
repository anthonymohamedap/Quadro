using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    public class Offerte
    {
        public int Id { get; set; }

        /// <summary>
        /// Doorlopend, zichtbaar offertenummer. Toegekend bij aanmaak en NOOIT hergebruikt of
        /// teruggedraaid — ook niet als deze offerte later gearchiveerd (= fysiek verwijderd uit
        /// deze tabel) wordt. Dit is bewust losgekoppeld van <see cref="Id"/> (de databank-PK),
        /// zodat archiveren geen gat laat vallen in de reeks die de gebruiker ziet.
        /// Zie <see cref="QuadroApp.Service.OfferteNummering"/>.
        /// </summary>
        public int OfferteNummer { get; set; }

        /// <summary>
        /// Optimistic concurrency token. EF Core updates this on every save.
        /// A <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
        /// is thrown when two users save the same version simultaneously —
        /// the second save is rejected with a clear "iemand anders heeft dit gewijzigd" message.
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

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

        /// <summary>Datum waarop de klant zijn werk komt afhalen.</summary>
        public DateTime? AfhaalDatum { get; set; }

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

        /// <summary>US-87 — stappen die bewust overgeslagen werden (bv. standaardkader uit voorraad).</summary>
        public VerkoopStapFlags OvergeslagenStappen { get; set; } = VerkoopStapFlags.Geen;

        /// <summary>US-87 — reden bij het overslaan ("Standaardkader uit voorraad", …).</summary>
        public string? OverslaanReden { get; set; }

        /// <summary>US-87 — wanneer de verkoop geannuleerd werd.</summary>
        public DateTime? GeannuleerdOp { get; set; }

        /// <summary>US-87 — verplichte reden bij annuleren.</summary>
        public string? AnnuleerReden { get; set; }

        /// <summary>US-102 — wanneer de verkoop afgehaald en volledig betaald was (afgerond).</summary>
        public DateTime? AfgerondOp { get; set; }

        /// <summary>
        /// US-102 — momentopname (JSON, zelfde opbouw als het archief: <c>OfferteArchiefSnapshot</c>) van
        /// offerte, klant en inlijstingen bij het afronden, zodat latere wijzigingen aan lijsten, afwerkingen,
        /// standaardkaders of klant een afgeronde verkoop niet meer veranderen.
        /// </summary>
        public string? Momentopname { get; set; }

        // 🔹 Restbedrag (niet in DB nodig maar mag)
        [NotMapped]
        public decimal RestTeBetalen =>
            TotaalInclBtw - VoorschotBedrag;
    }
}
