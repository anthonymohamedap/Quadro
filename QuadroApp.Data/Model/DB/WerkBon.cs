using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace QuadroApp.Model.DB
{
    public enum WerkBonStatus
    {
        Gepland = 0,
        InUitvoering = 1,
        Afgewerkt = 2,
        Afgehaald = 3
    }

    [Index(nameof(OfferteId))]
    public class WerkBon
    {
        public int Id { get; set; }
        public int OfferteId { get; set; }
        public Offerte Offerte { get; set; } = null!;

        public DateTime? AfhaalDatum { get; set; }

        [Precision(10, 2)]
        public decimal TotaalPrijsIncl { get; set; }

        public WerkBonStatus Status { get; set; } = WerkBonStatus.Gepland;

        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;
        public DateTime? BijgewerktOp { get; set; }
        public bool StockReservationProcessed { get; set; }

        [Timestamp] public byte[]? RowVersion { get; set; }

        /// <summary>A-04 — versie van de rij op PostgreSQL (systeemkolom xmin), om gelijktijdig wijzigen te
        /// merken. Een gewone eigenschap (geen shadow) zodat ze meereist met een kopie die later bewaard wordt.
        /// Op SQLite niet gebruikt (daar dient RowVersion).</summary>
        public uint PgVersie { get; set; }

        public ICollection<WerkTaak> Taken { get; set; } = new List<WerkTaak>();

        // ── US-51: afgeleide weergave voor de werkbonnenlijst (niet in DB) ──────
        /// <summary>Aantal werktaken op deze werkbon.</summary>
        /// <summary>Het nummer dat op het scherm staat (zie <see cref="VerkoopNummer"/>): het bestelbonnummer na het
        /// akkoord, anders "voorlopig · 412". Wordt bij het laden gevuld (VerkoopNummers.VulAsync); zonder dat
        /// valt het terug op "voorlopig · offertenummer".</summary>
        [NotMapped]
        public string VerkoopNummerTekst
        {
            get => _verkoopNummerTekst ?? VerkoopNummer.Tekst(Offerte?.OfferteNummer ?? 0, (string?)null);
            set => _verkoopNummerTekst = value;
        }
        private string? _verkoopNummerTekst;

        [NotMapped]
        public int AantalTaken => Taken?.Count ?? 0;

        /// <summary>Aantal taken dat besteld is (of geen bestelling nodig heeft = op voorraad).</summary>
        [NotMapped]
        public int AantalBesteldOfKlaar => Taken?.Count(t => t.IsBesteld || t.IsOpVoorraad) ?? 0;

        /// <summary>Compacte bestel-voortgang, bv. "3/5 besteld" — leeg als er geen taken zijn.</summary>
        [NotMapped]
        public string BestelVoortgangLabel =>
            AantalTaken == 0 ? "" : $"{AantalBesteldOfKlaar}/{AantalTaken} besteld";
    }
}
