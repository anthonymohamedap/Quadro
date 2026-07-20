using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public class AfwerkingsGroep
    {
        public int Id { get; set; }

        [Required]
        public char Code { get; set; }  // G / P / D / O / R

        [Required]
        public string Naam { get; set; } = string.Empty;

        public ICollection<AfwerkingsOptie> Opties { get; set; } = new List<AfwerkingsOptie>();
    }
}
