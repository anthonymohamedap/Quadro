using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>US-66 — los register voor winkelverkopen (kleine verkopen aan de toonbank, los van
    /// offertes/facturen). Naar het model van <see cref="KantKlaarKader"/>: eenvoudig, geen
    /// leverancier/voorraad-koppeling. Geen soft-delete-filter nodig — niets anders verwijst naar
    /// deze entiteit, dus hard delete is aanvaardbaar (blijft achteraf traceerbaar via de
    /// automatische <see cref="AuditLog"/>).</summary>
    public class WinkelVerkoop
    {
        public int Id { get; set; }

        public DateTime Datum { get; set; } = DateTime.Today;

        [MaxLength(300)]
        [Required]
        public string Omschrijving { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Aantal { get; set; } = 1m;

        /// <summary>Prijs per stuk, INCL. btw.</summary>
        [Precision(18, 2)]
        public decimal PrijsInclBtw { get; set; }

        /// <summary>Standaard 21 % (gewone Belgische btw) — aanpasbaar per verkoop.</summary>
        [Precision(5, 2)]
        public decimal BtwPct { get; set; } = 21m;

        public Betaalwijze Betaalwijze { get; set; }

        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public decimal TotaalInclBtw => Aantal * PrijsInclBtw;
    }
}
