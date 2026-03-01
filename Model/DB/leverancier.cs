using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class Leverancier
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(3)]
        public string Naam { get; set; } = null!;

        public ICollection<TypeLijst> TypeLijsten { get; set; } = new List<TypeLijst>();
    }
}
