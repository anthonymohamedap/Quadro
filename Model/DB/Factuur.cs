using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB;

public enum FactuurStatus
{
    Draft = 0,
    KlaarVoorExport = 1,
    Geexporteerd = 2,
    Betaald = 3,
    Geannuleerd = 4
}

[Index(nameof(WerkBonId), IsUnique = true)]
[Index(nameof(Jaar), nameof(VolgNr), IsUnique = true)]
public class Factuur
{
    public int Id { get; set; }

    public int WerkBonId { get; set; }
    public WerkBon WerkBon { get; set; } = null!;

    public int Jaar { get; set; }
    public int VolgNr { get; set; }

    [MaxLength(20)]
    public string FactuurNummer { get; set; } = string.Empty;

    [MaxLength(30)]
    public string DocumentType { get; set; } = "Bestelbon";

    [MaxLength(200)]
    public string KlantNaam { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? KlantAdres { get; set; }

    [MaxLength(120)]
    public string? KlantBtwNummer { get; set; }

    public DateTime FactuurDatum { get; set; }
    public DateTime VervalDatum { get; set; }

    [MaxLength(2000)]
    public string? Opmerking { get; set; }

    [MaxLength(10)]
    public string? AangenomenDoorInitialen { get; set; }

    public bool IsBtwVrijgesteld { get; set; }

    [Precision(18, 2)]
    public decimal TotaalExclBtw { get; set; }

    [Precision(18, 2)]
    public decimal TotaalBtw { get; set; }

    [Precision(18, 2)]
    public decimal TotaalInclBtw { get; set; }

    [MaxLength(500)]
    public string? ExportPad { get; set; }

    public FactuurStatus Status { get; set; } = FactuurStatus.Draft;
    public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;
    public DateTime? BijgewerktOp { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }

    public ICollection<FactuurLijn> Lijnen { get; set; } = new List<FactuurLijn>();
}
