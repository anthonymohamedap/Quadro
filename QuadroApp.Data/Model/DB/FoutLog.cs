using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    /// <summary>
    /// Een onverwachte fout (crash) van de app, zodat de beheerder ze op afstand kan opvolgen.
    /// Wordt geschreven naast crash.log en bij opstart aangevuld met wat enkel in crash.log stond.
    /// Bevat geen klantgegevens, bedragen of wachtwoorden: de tekstvelden worden vooraf gefilterd
    /// en ingekort (zie FoutLogFilter).
    /// </summary>
    public class FoutLog
    {
        public int Id { get; set; }

        /// <summary>Moment van de fout, in UTC (op de seconde).</summary>
        public DateTime Tijdstip { get; set; }

        /// <summary>UnobservedTask / Achtergrondthread / UI-thread / Opstarten / Geïmporteerd (…).</summary>
        [MaxLength(60)]
        public string Bron { get; set; } = null!;

        /// <summary>Korte foutmelding (Exception.Message, gefilterd en ingekort).</summary>
        [MaxLength(1000)]
        public string Melding { get; set; } = null!;

        [MaxLength(300)]
        public string ExceptionType { get; set; } = null!;

        /// <summary>Volledige ex.ToString(), gefilterd en ingekort.</summary>
        public string Stacktrace { get; set; } = null!;

        /// <summary>Het scherm (ViewModel) dat open stond.</summary>
        [MaxLength(200)]
        public string? Scherm { get; set; }

        /// <summary>De laatste klikken, één per regel (nieuwste laatst).</summary>
        [MaxLength(4000)]
        public string? LaatsteKlikken { get; set; }

        [MaxLength(50)]
        public string? AppVersie { get; set; }

        [MaxLength(100)]
        public string? Computernaam { get; set; }

        /// <summary>Login van de ingelogde gebruiker, als bekend.</summary>
        [MaxLength(50)]
        public string? Gebruiker { get; set; }

        /// <summary>SHA-256 (hex) op tijdstip + type + eerste stackregels + computer; uniek, tegen dubbels.</summary>
        [MaxLength(64)]
        public string Hash { get; set; } = null!;

        public bool IsBekeken { get; set; }
    }
}
