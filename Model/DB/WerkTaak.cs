using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
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

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}