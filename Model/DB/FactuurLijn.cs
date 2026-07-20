using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB;

public class FactuurLijn
{
    public int Id { get; set; }

    public int FactuurId { get; set; }
    public Factuur Factuur { get; set; } = null!;

    [MaxLength(500)]
    public string Omschrijving { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal Aantal { get; set; } = 1m;

    [MaxLength(20)]
    public string Eenheid { get; set; } = "st";

    // Hogere precisie: een afgesproken incl.-prijs wordt teruggerekend naar netto
    // (bv. 100 / 1,21 = 82,644628…) en moet die precisie behouden zodat de incl.-prijs
    // exact reproduceerbaar is. Afronding naar 2 decimalen gebeurt pas op de totalen.
    [Precision(18, 6)]
    public decimal PrijsExcl { get; set; }

    [Precision(5, 2)]
    public decimal BtwPct { get; set; }

    [Precision(18, 2)]
    public decimal TotaalExcl { get; set; }

    [Precision(18, 2)]
    public decimal TotaalBtw { get; set; }

    [Precision(18, 2)]
    public decimal TotaalIncl { get; set; }

    public int Sortering { get; set; }
}

