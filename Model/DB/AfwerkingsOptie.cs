using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class AfwerkingsOptie
    {
        public int Id { get; set; }

        [Required]
        public int AfwerkingsGroepId { get; set; }
        public AfwerkingsGroep AfwerkingsGroep { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Naam { get; set; } = string.Empty;

        [Required]
        public char Volgnummer { get; set; }

        [Precision(10, 2)]
        public decimal KostprijsPerM2 { get; set; }

        [Precision(6, 3)]
        public decimal WinstMarge { get; set; }

        [Precision(5, 2)]
        public decimal AfvalPercentage { get; set; }

        [Precision(10, 2)]
        public decimal VasteKost { get; set; }

        [Range(0, 1440)]
        public int WerkMinuten { get; set; }

        public int? LeverancierId { get; set; }
        public Leverancier? Leverancier { get; set; }
    }
}
