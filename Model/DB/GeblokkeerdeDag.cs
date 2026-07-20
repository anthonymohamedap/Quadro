using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB;

[Index(nameof(Datum), IsUnique = true)]
public class GeblokkeerdeDag
{
    public int Id { get; set; }

    public DateTime Datum { get; set; }

    [MaxLength(200)]
    public string? Reden { get; set; }
}
