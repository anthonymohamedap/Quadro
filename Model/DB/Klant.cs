using System.Collections.Generic;

namespace QuadroApp.Model.DB
{
    public class Klant
    {
        public int Id { get; set; }
        public string Voornaam { get; set; } = null!;
        public string Achternaam { get; set; } = null!;
        public string? Email { get; set; }
        public string? Telefoon { get; set; }
        public string? Straat { get; set; }
        public string? Nummer { get; set; }
        public string? Postcode { get; set; }
        public string? Gemeente { get; set; }
        public string? BtwNummer { get; set; }
        public string? Opmerking { get; set; }


        // Navigatie: één klant kan meerdere offertes of werkbonnen hebben
        public ICollection<Offerte> Offertes { get; set; } = new List<Offerte>();
    }

}
