using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class Instelling
    {
        [Key]
        [MaxLength(100)]
        public string Sleutel { get; set; } = null!;

        [MaxLength(2000)]
        public string Waarde { get; set; } = null!;
    }
}
