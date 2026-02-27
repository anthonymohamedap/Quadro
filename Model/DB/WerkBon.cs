using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public enum WerkBonStatus { Nieuw, InPlanning, InUitvoering, Afgewerkt, Geannuleerd }

    [Index(nameof(OfferteId))]
    public class WerkBon
    {
        public int Id { get; set; }
        public int OfferteId { get; set; }
        public Offerte Offerte { get; set; } = null!;

        public DateTime? AfhaalDatum { get; set; }

        [Precision(10, 2)]
        public decimal TotaalPrijsIncl { get; set; }

        // Nieuw
        public WerkBonStatus Status { get; set; } = WerkBonStatus.Nieuw;

        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;
        public DateTime? BijgewerktOp { get; set; }

        [Timestamp] public byte[]? RowVersion { get; set; }

        public ICollection<WerkTaak> Taken { get; set; } = new List<WerkTaak>();
    }
}
