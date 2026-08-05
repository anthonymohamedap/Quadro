using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    /// <summary>
    /// US-36 — onveranderbaar logboek van wijzigingen aan belangrijke entiteiten.
    /// Alleen-schrijven vanuit AppDbContext.SaveChangesAsync; de app biedt geen
    /// bewerk- of verwijder-functionaliteit voor deze tabel.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        public DateTime Tijdstip { get; set; }

        /// <summary>Gebruikersnaam van de ingelogde gebruiker ("(systeem)" buiten een sessie).</summary>
        [MaxLength(50)]
        public string Gebruiker { get; set; } = null!;

        [MaxLength(100)]
        public string EntiteitType { get; set; } = null!;

        [MaxLength(50)]
        public string EntiteitId { get; set; } = null!;

        /// <summary>Toegevoegd / Gewijzigd / Verwijderd</summary>
        [MaxLength(20)]
        public string Actie { get; set; } = null!;

        /// <summary>JSON: { "Veld": { "oud": ..., "nieuw": ... }, ... }</summary>
        public string Wijzigingen { get; set; } = null!;
    }
}
