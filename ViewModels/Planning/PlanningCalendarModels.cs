using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace QuadroApp.ViewModels;

// ───────── SUPPORT CLASSES voor PlanningCalendarViewModel ─────────
// Geen IBrush/kleuren in de ViewModel-laag: tegels leveren alleen status
// (booleans); de AXAML-styles in PlanningCalendarWindow mappen status → kleur
// via theme-tokens (Classes.*-bindingen).

public partial class DayTile : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = "";
    public string BusyLabel { get; set; } = "";

    /// <summary>Bezetting 0..1 t.o.v. dagcapaciteit (geblokkeerd = 1).</summary>
    public double Busy { get; set; }

    public bool IsToday { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsOtherMonth { get; set; }
    public bool IsGeblokkeerd { get; set; }

    // Capaciteitsniveau als booleans voor Classes.*-bindingen (zelfde
    // drempels als voorheen: ≤50% laag, ≤75% midden, ≤90% hoog, anders vol).
    public bool CapLow => !IsGeblokkeerd && Busy <= 0.5;
    public bool CapMid => !IsGeblokkeerd && Busy > 0.5 && Busy <= 0.75;
    public bool CapHigh => !IsGeblokkeerd && Busy > 0.75 && Busy <= 0.9;
    public bool CapFull => IsGeblokkeerd || Busy > 0.9;

    [ObservableProperty] private bool isSelected;
}

public class WeekSummary
{
    public string Title { get; set; } = "";
    public string Range { get; set; } = "";
    public string TotalLabel { get; set; } = "";
}

public class DayRow
{
    public int WeekNr { get; set; }
    public string Dag { get; set; } = "";
    public DateTime Datum { get; set; }
    public int Uren { get; set; }
    public int Minuten { get; set; }
    public bool IsGeblokkeerd { get; set; }
    public string UurMinText => IsGeblokkeerd ? "🚫" : $"{Uren:00}:{Minuten:00}";
}

public class WeekRow
{
    public int BonNr { get; set; }
    public int DuurMin { get; set; }
    public string KlantNaam { get; set; } = "";
    public string Afmeting { get; set; } = "";
    public string Lijst { get; set; } = "";
    public string LijstType { get; set; } = "";
    public string Dag { get; set; } = "";
}
