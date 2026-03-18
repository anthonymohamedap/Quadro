using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public enum LeverancierBestellingStatus
    {
        Concept = 0,
        Besteld = 1,
        DeelsOntvangen = 2,
        VolledigOntvangen = 3,
        Geannuleerd = 4
    }

    [Index(nameof(BestelNummer), IsUnique = true)]
    public class LeverancierBestelling
    {
        public int Id { get; set; }

        public int LeverancierId { get; set; }
        public Leverancier Leverancier { get; set; } = null!;

        [MaxLength(40)]
        public string BestelNummer { get; set; } = string.Empty;

        public LeverancierBestellingStatus Status { get; set; } = LeverancierBestellingStatus.Concept;
        public DateTime BesteldOp { get; set; } = DateTime.UtcNow;
        public DateTime? VerwachteLeverdatum { get; set; }
        public DateTime? OntvangenOp { get; set; }

        [MaxLength(2000)]
        public string? Opmerking { get; set; }

        [MaxLength(100)]
        public string? AangemaaktDoor { get; set; }

        public ICollection<LeverancierBestelLijn> Lijnen { get; set; } = new List<LeverancierBestelLijn>();
    }
}
