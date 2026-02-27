using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class Instelling
    {
        [Key]
        [MaxLength(100)]
        public string Sleutel { get; set; } = null!;

        [MaxLength(255)]
        public string Waarde { get; set; } = null!;
    }
}
