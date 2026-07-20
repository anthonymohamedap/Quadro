using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    /// <summary>US-32 — gebruikersaccount met rol.</summary>
    public class Gebruiker
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string GebruikersNaam { get; set; } = null!;

        [MaxLength(100)]
        public string VolledigeNaam { get; set; } = null!;

        /// <summary>PBKDF2-hash in het formaat "iteraties.saltB64.hashB64".</summary>
        [MaxLength(500)]
        public string WachtwoordHash { get; set; } = null!;

        public GebruikersRol Rol { get; set; } = GebruikersRol.Medewerker;

        public bool IsActief { get; set; } = true;

        /// <summary>True zolang het initiële (door admin gezette) wachtwoord niet gewijzigd is.</summary>
        public bool MoetWachtwoordWijzigen { get; set; } = false;

        public DateTime AangemaaktOp { get; set; } = DateTime.Now;
        public DateTime? LaatsteLogin { get; set; }
    }

    public enum GebruikersRol
    {
        Medewerker = 0,
        Admin = 1
    }
}
