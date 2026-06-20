using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace QuadroApp.ViewModels;

// ───────── SUPPORT CLASSES voor PlanningCalendarViewModel ─────────

public partial class DayTile : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = "";
    public string BusyLabel { get; set; } = "";
    public double Busy { get; set; }
    public IBrush BusyColor { get; set; } = Brushes.LimeGreen;
    public double BusyBarWidth => Busy * 120;
    public bool IsGeblokkeerd { get; set; }

    [ObservableProperty] private IBrush background = Brushes.Transparent;
    [ObservableProperty] private IBrush border = Brushes.Gray;
    [ObservableProperty] private bool isSelected;
}

public class WeekSummary
{
    public string Title { get; set; } = "";
    public string Range { get; set; } = "";
    public string TotalLabel { get; set; } = "";
    public IBrush Color { get; set; } = Brushes.Gray;
}

public class DayRow
{
    public int WeekNr { get; set; }
    public string Dag { get; set; } = "";
    public DateTime Datum { get; set; }
    public int Uren { get; set; }
    public int Minuten { get; set; }
    public IBrush Kleur { get; set; } = Brushes.Gray;
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
