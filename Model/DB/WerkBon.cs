using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

        public ICollection<WerkTaak> Taken { get; set; } = new List<WerkTaak>();
    }
}
