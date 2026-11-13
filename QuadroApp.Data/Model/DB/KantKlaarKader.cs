using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB
{
    /// <summary>US-58 — reeds volledig afgewerkt kader (besteld bij een leverancier, niet zelf
    /// gesneden of afgewerkt), met een vaste stukprijs. Gewoon een artikel: geen afmeting (geen
    /// afwerkingen zoals glas/passe-partout komen er nog bovenop — het kader is al af), geen
    /// voorraad, geen leveranciers-koppeling, geen bestel-integratie.</summary>
    public class KantKlaarKader
    {
        public int Id { get; set; }

        [MaxLength(200)]
        [Required]
        public string Naam { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Beschrijving { get; set; }

        /// <summary>Stukprijs excl. btw; de prijsberekening werkt hiermee. Vier decimalen, zodat een
        /// ingegeven prijs incl. btw (bv. € 10,00) na omrekenen exact terugkomt.</summary>
        [Precision(18, 4)]
        public decimal PrijsPerStukExcl { get; set; }

        /// <summary>Btw op standaardkaders: altijd 21 % (Veerle, 27/09).</summary>
        public const decimal BtwPct = 21m;

        /// <summary>Feedback Veerle (12/11): de prijs wordt incl. btw ingegeven en getoond; bewaard
        /// wordt de prijs excl. btw, zodat de prijsberekening ongewijzigd blijft. Die rondt de btw per
        /// regel af op de cent; daarom wordt de prijs excl. zo gekozen dat één stuk in de offerte
        /// exact het ingegeven bedrag incl. geeft (zie FeedbackVeerle1211Tests).</summary>
        [NotMapped]
        public decimal PrijsPerStukIncl
        {
            get => InclVoor(PrijsPerStukExcl);
            set
            {
                var doel = Math.Round(value, 2);
                var basis = Math.Round(doel / (1m + BtwPct / 100m), 4);
                for (var stap = 0; stap <= 200; stap++)
                {
                    foreach (var kandidaat in new[] { basis + stap * 0.0001m, basis - stap * 0.0001m })
                    {
                        if (kandidaat >= 0m && InclVoor(kandidaat) == doel)
                        {
                            PrijsPerStukExcl = kandidaat;
                            return;
                        }
                    }
                }
                PrijsPerStukExcl = basis;
            }
        }

        /// <summary>Zoals de prijsberekening één stuk rekent: btw op de cent, dan het totaal op de cent.</summary>
        private static decimal InclVoor(decimal excl)
        {
            var btw = Math.Round(excl * (BtwPct / 100m), 2);
            return Math.Round(excl + btw, 2);
        }

        /// <summary>True = gearchiveerd. Wordt door de globale query filter uitgesloten.</summary>
        public bool IsGearchiveerd { get; set; } = false;

        /// <summary>US-80 — "Naam – Beschrijving" (bv. "C2 – Alu zwart 50×70"); enkel de naam als er geen beschrijving is.</summary>
        [NotMapped]
        public string Weergave => string.IsNullOrWhiteSpace(Beschrijving) ? Naam : $"{Naam} – {Beschrijving.Trim()}";
    }
}
