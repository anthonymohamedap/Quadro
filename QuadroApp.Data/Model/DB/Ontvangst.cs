using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>US-66 — soort ontvangst in het centrale ontvangstenregister.</summary>
    public enum OntvangstSoort
    {
        /// <summary>Voorschot op een offerte (US-69).</summary>
        Voorschot,
        /// <summary>Betaling op een bestelbon: bij afhalen, vooraf of achteraf (US-70).</summary>
        Betaling,
        /// <summary>Toonbankverkoop zonder offerte/bestelbon (kassa-verkoop).</summary>
        Winkelverkoop,
        /// <summary>Handmatige correctie (mag negatief zijn).</summary>
        Correctie
    }

    /// <summary>
    /// US-66 — centraal register van alles wat er binnenkomt: voorschotten, betalingen op een
    /// bestelbon en winkelverkopen. Het "overzicht betalingen" (US-67) is één query op deze tabel,
    /// per dag × <see cref="Betaalwijze"/>.
    /// Btw is altijd 21 % (Veerle, 27/09) en wordt dus niet per ontvangst bewaard.
    /// Verwijzingen naar offerte/bestelbon zijn optioneel en worden op NULL gezet als die later
    /// verwijderd/gearchiveerd worden — de ontvangst zelf blijft (boekhouding).
    /// </summary>
    public class Ontvangst
    {
        public const decimal BtwPct = 21m;

        public int Id { get; set; }

        /// <summary>Dag waarop het geld ontvangen werd.</summary>
        public DateTime Datum { get; set; } = DateTime.Today;

        public OntvangstSoort Soort { get; set; } = OntvangstSoort.Winkelverkoop;

        public Betaalwijze Betaalwijze { get; set; } = Betaalwijze.Bancontact;

        /// <summary>Effectief ontvangen bedrag, incl. btw. Bij een winkelverkoop = <see cref="TotaalIncl"/>.</summary>
        [Precision(18, 2)]
        public decimal BedragIncl { get; set; }

        // ── koppelingen (voorschot / betaling op bestelbon) ──
        public int? OfferteId { get; set; }
        public Offerte? Offerte { get; set; }

        public int? FactuurId { get; set; }
        public Factuur? Factuur { get; set; }

        // ── winkelverkoop (kassa) ──
        [MaxLength(300)]
        public string? Omschrijving { get; set; }

        [Precision(18, 2)]
        public decimal Aantal { get; set; } = 1m;

        /// <summary>Prijs per stuk, INCL. btw.</summary>
        [Precision(18, 2)]
        public decimal PrijsPerStukIncl { get; set; }

        [Precision(5, 2)]
        public decimal KortingPct { get; set; }

        // ── traceerbaarheid ──
        [MaxLength(100)]
        public string? AangemaaktDoor { get; set; }

        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;

        /// <summary>Winkelverkoop: aantal × prijs − korting, afgerond op de cent.</summary>
        [NotMapped]
        public decimal TotaalIncl => Math.Round(Aantal * PrijsPerStukIncl * (1m - KortingPct / 100m), 2);

        /// <summary>Label voor lijsten/afdrukken: omschrijving, of het bonnummer bij een betaling.</summary>
        [NotMapped]
        public string SoortLabel => Soort switch
        {
            OntvangstSoort.Voorschot => "Voorschot",
            OntvangstSoort.Betaling => "Betaling",
            OntvangstSoort.Winkelverkoop => "Winkelverkoop",
            _ => "Correctie"
        };
    }
}
