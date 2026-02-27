using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class Leverancier
    {
        public int Id { get; set; }

        [MaxLength(3)]
        [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Leveranciercode moet uit 3 hoofdletters bestaan.")]
        public string Code { get; set; } = null!;

        [MaxLength(100)]
        public string? Naam { get; set; }

        public ICollection<TypeLijst> TypeLijsten { get; set; } = new List<TypeLijst>();
        public ICollection<AfwerkingsOptie> AfwerkingsOpties { get; set; } = new List<AfwerkingsOptie>();
    }
}
