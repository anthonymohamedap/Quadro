using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model;
using QuadroApp.Model.DB;
using QuadroApp.Service.Toast;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class PlanningCalendarViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IWerkBonWorkflowService _workflow;
    private readonly IToastService _toast;
    private readonly IWorkflowService _statusWorkflow;

    private const int CapaciteitMinuten = 8 * 60;

    // Injecteer vanuit de code-behind om een PlanningTijdDialog te tonen.
    public Func<PlanningTijdDialogViewModel, Task<bool>>? ShowTijdDialogAsync { get; set; }

    [ObservableProperty] private DayRow? selectedDayRow;
    [ObservableProperty] private int selectedWeekNr;
    [ObservableProperty] private ObservableCollection<DayRow> weekDayRows = new();

    public IRelayCommand PrevMonthCommand { get; }
    public IRelayCommand NextMonthCommand { get; }
    public IRelayCommand TodayCommand { get; }
    public IAsyncRelayCommand OpenWeekWerkLijstCommand { get; }
    public ReadOnlyObservableCollection<ToastMessage> ToastMessages { get; }

    // ───────── ACTIEVE WERKBON ─────────

    private int _werkBonId;
    public int WerkBonId
    {
        get => _werkBonId;
        private set
        {
            _werkBonId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HeeftWerkBon));
            OnPropertyChanged(nameof(GeenWerkBon));
        }
    }

    public bool HeeftWerkBon => WerkBonId != 0;
    public bool GeenWerkBon => WerkBonId == 0;

    public PlanningCalendarViewModel(
        IDbContextFactory<AppDbContext> factory,
        IWerkBonWorkflowService workflow,
        IToastService toast,
        IWorkflowService statusWorkflow)
    {
        _factory = factory;
        _workflow = workflow;
        _toast = toast;
        _statusWorkflow = statusWorkflow;

        ToastMessages = toast.Messages;

        PrevMonthCommand = new RelayCommand(PrevMonth);
        NextMonthCommand = new RelayCommand(NextMonth);
        TodayCommand = new RelayCommand(SetToday);
        OpenWeekWerkLijstCommand = new AsyncRelayCommand(OpenWeekWerkLijstAsync);

        UpdateMonthTitle();
    }

    // ───────── OPEN WEEKLIJST ─────────

    private async Task OpenWeekWerkLijstAsync()
    {
        var weekNr = ISOWeek.GetWeekOfYear(SelectedDate);
        var year = SelectedDate.Year;

        var vm = new WeekWerkLijstViewModel(_factory, _statusWorkflow);
        await vm.InitializeAsync(year, weekNr);

        var win = new QuadroApp.Views.WeekWerkLijstWindow { DataContext = vm };

        if (App.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var owner = desktop.MainWindow;
            if (owner is null) return;
            await win.ShowDialog(owner);
        }
    }

    // ───────── HEADER ─────────

    private int _year = DateTime.Today.Year;
    public int Year
    {
        get => _year;
        set { SetProperty(ref _year, value); UpdateMonthTitle(); _ = LoadAsync(); }
    }

    private int _month = DateTime.Today.Month;
    public int Month
    {
        get => _month;
        set { SetProperty(ref _month, value); UpdateMonthTitle(); _ = LoadAsync(); }
    }

    private string _monthTitle = "";
    public string MonthTitle
    {
        get => _monthTitle;
        set => SetProperty(ref _monthTitle, value);
    }

    private void UpdateMonthTitle() =>
        MonthTitle = new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    private void PrevMonth()
    {
        var d = new DateTime(Year, Month, 1).AddMonths(-1);
        Year = d.Year; Month = d.Month;
    }

    private void NextMonth()
    {
        var d = new DateTime(Year, Month, 1).AddMonths(1);
        Year = d.Year; Month = d.Month;
    }

    private void SetToday()
    {
        var t = DateTime.Today;
        Year = t.Year; Month = t.Month;
        SelectedDate = t;
    }

    public List<string> WeekHeaders =>
        CultureInfo.CurrentCulture.DateTimeFormat
            .AbbreviatedDayNames
            .Skip(1)
            .Concat(new[] { CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[0] })
            .ToList();

    // ───────── SELECTED DAG ─────────

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    partial void OnSelectedDayRowChanged(DayRow? value)
    {
        if (value is null) return;
        SelectedDate = value.Datum;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        SelectedWeekNr = ISOWeek.GetWeekOfYear(value);
        BuildWeekDayRows();
        UpdateTileSelection(value);
        _ = LoadTakenVanDagAsync();
        _ = LoadWeekRowsAsync(SelectedWeekNr);
    }

    // ───────── TILE SELECTION ─────────

    private static IBrush ComputeTileBorder(DateTime date, bool isSelected)
    {
        if (isSelected) return new SolidColorBrush(Color.Parse("#F5C242"));
        if (date.Date == DateTime.Today) return Brushes.DeepSkyBlue;
        return new SolidColorBrush(Color.FromRgb(70, 70, 70));
    }

    private void UpdateTileSelection(DateTime selectedDate)
    {
        foreach (var tile in MonthDays)
        {
            tile.IsSelected = tile.Date.Date == selectedDate.Date;
            tile.Border = ComputeTileBorder(tile.Date, tile.IsSelected);
        }
    }

    // ───────── REGELS VAN WERKBON ─────────

    [ObservableProperty]
    private ObservableCollection<RegelPlanItem> regelsVanWerkBon = new();

    public async Task InitializeGlobalAsync()
    {
        WerkBonId = 0;
        RegelsVanWerkBon = new ObservableCollection<RegelPlanItem>();
        await LoadAsync();
        await LoadTakenVanDagAsync();
        await LoadWeekRowsAsync(ISOWeek.GetWeekOfYear(SelectedDate));
    }

    public async Task InitializeAsync(int werkBonId)
    {
        WerkBonId = werkBonId;
        await LoadRegelsVanWerkBonAsync();
        await LoadAsync();
        SelectedWeekNr = ISOWeek.GetWeekOfYear(SelectedDate);
        BuildWeekDayRows();
        await LoadWeekRowsAsync(SelectedWeekNr);
        await LoadTakenVanDagAsync();
    }

    private void BuildWeekDayRows()
    {
        var filtered = DayRows
            .Where(d => d.WeekNr == SelectedWeekNr)
            .OrderBy(d => d.Datum)
            .ToList();
        WeekDayRows = new ObservableCollection<DayRow>(filtered);
    }

    private async Task LoadRegelsVanWerkBonAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var offerteId = await db.WerkBonnen
            .Where(w => w.Id == WerkBonId)
            .Select(w => w.OfferteId)
            .FirstAsync();

        var regels = await db.OfferteRegels
            .Include(r => r.TypeLijst)
            .Where(r => r.OfferteId == offerteId)
            .ToListAsync();

        RegelsVanWerkBon = new ObservableCollection<RegelPlanItem>(
            regels.Select(r => new RegelPlanItem
            {
                RegelId = r.Id,
                Label = $"{r.AantalStuks}x {r.BreedteCm}×{r.HoogteCm} — {r.TypeLijst?.Artikelnummer ?? "?"}",
                IsSelected = false
            })
        );
    }

    private static int CalcMinutenVoorRegel(OfferteRegel r)
    {
        int min = 0;
        min += r.TypeLijst?.WerkMinuten ?? 0;
        min += r.Glas?.WerkMinuten ?? 0;
        min += r.PassePartout1?.WerkMinuten ?? 0;
        min += r.PassePartout2?.WerkMinuten ?? 0;
        min += r.DiepteKern?.WerkMinuten ?? 0;
        min += r.Opkleven?.WerkMinuten ?? 0;
        min += r.Rug?.WerkMinuten ?? 0;
        min += r.ExtraWerkMinuten;

        int stuks = r.AantalStuks <= 0 ? 1 : r.AantalStuks;
        min *= stuks;
        return Math.Max(min, 15);
    }

    // ───────── PLAN GESELECTEERDE REGELS ─────────

    [RelayCommand]
    private async Task PlanGeselecteerdeRegelsAsync()
    {
        if (WerkBonId == 0)
        {
            _toast.Error("Open planning vanuit een werkbon om regels te plannen.");
            return;
        }

        var selectedIds = RegelsVanWerkBon.Where(x => x.IsSelected).Select(x => x.RegelId).ToList();
        if (selectedIds.Count == 0) return;

        // Tijdstip bepalen via dialoog
        DateTime van;
        if (ShowTijdDialogAsync is not null)
        {
            await using var dbPreview = await _factory.CreateDbContextAsync();
            var werkBonLabel = await dbPreview.WerkBonnen
                .Where(w => w.Id == WerkBonId)
                .Select(w => w.Offerte.Klant.Achternaam)
                .FirstOrDefaultAsync() ?? $"WerkBon #{WerkBonId}";

            var dialogVm = new PlanningTijdDialogViewModel
            {
                ContextLabel = $"WerkBon #{WerkBonId} — {werkBonLabel} · {selectedIds.Count} regel(s)",
                GeplandeDatum = new DateTimeOffset(SelectedDate.Date),
            };

            bool ok = await ShowTijdDialogAsync(dialogVm);
            if (!ok) return;

            van = dialogVm.GetVanDateTime();
        }
        else
        {
            van = SelectedDate.Date.AddHours(9);
        }

        await using var db = await _factory.CreateDbContextAsync();

        var regels = await db.OfferteRegels
            .Include(r => r.TypeLijst)
            .Include(r => r.Glas)
            .Include(r => r.PassePartout1)
            .Include(r => r.PassePartout2)
            .Include(r => r.DiepteKern)
            .Include(r => r.Opkleven)
            .Include(r => r.Rug)
            .Where(r => selectedIds.Contains(r.Id))
            .ToListAsync();

        var dagTaken = await db.WerkTaken
            .Where(t => t.GeplandVan.Date == van.Date)
            .ToListAsync();

        int totaal = dagTaken.Sum(t => t.DuurMinuten);
        int extra = regels.Sum(CalcMinutenVoorRegel);

        if (totaal + extra > CapaciteitMinuten)
            _toast.Warning($"Let op: dag heeft {(totaal + extra) / 60}u {(totaal + extra) % 60}m gepland (max {CapaciteitMinuten / 60}u).");

        foreach (var r in regels)
        {
            int duur = CalcMinutenVoorRegel(r);
            await _workflow.VoegPlanningToeVoorRegelAsync(
                WerkBonId, r.Id, van, duur, "Inlijsten");
        }

        await RefreshAsync();
    }

    // ───────── HERPLANNEN ─────────

    [RelayCommand]
    private async Task HerplanTaakAsync(WerkTaak taak)
    {
        if (ShowTijdDialogAsync is null) return;

        var dialogVm = new PlanningTijdDialogViewModel
        {
            ContextLabel = $"WerkBon #{taak.WerkBonId} — {taak.Omschrijving}",
            GeplandeDatum = new DateTimeOffset(taak.GeplandVan.Date),
            VanUur = taak.GeplandVan.Hour,
            VanMinuut = taak.GeplandVan.Minute,
            TotUur = taak.GeplandTot.Hour,
            TotMinuut = taak.GeplandTot.Minute,
        };

        bool ok = await ShowTijdDialogAsync(dialogVm);
        if (!ok) return;

        var nieuweVan = dialogVm.GetVanDateTime();
        var nieuweTot = nieuweVan.AddMinutes(dialogVm.DuurMinuten);

        await using var db = await _factory.CreateDbContextAsync();
        var dbTaak = await db.WerkTaken.FindAsync(taak.Id);
        if (dbTaak is null) return;

        dbTaak.GeplandVan = nieuweVan;
        dbTaak.GeplandTot = nieuweTot;
        dbTaak.DuurMinuten = dialogVm.DuurMinuten;

        await db.SaveChangesAsync();

        _toast.Success($"Taak herplanned naar {nieuweVan:dd/MM HH:mm}.");
        await RefreshAsync();
    }

    // ───────── VERWIJDEREN ─────────

    [RelayCommand]
    private async Task VerwijderTaakAsync(WerkTaak taak)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var dbTaak = await db.WerkTaken.FindAsync(taak.Id);
        if (dbTaak is null) return;

        db.WerkTaken.Remove(dbTaak);
        await db.SaveChangesAsync();

        _toast.Success("Taak verwijderd.");
        await RefreshAsync();
    }

    // ───────── REFRESH HELPER ─────────

    private async Task RefreshAsync()
    {
        await LoadAsync();
        await LoadTakenVanDagAsync();
        await LoadWeekRowsAsync(ISOWeek.GetWeekOfYear(SelectedDate));
    }

    // ───────── DAG DETAIL ─────────

    [ObservableProperty]
    private ObservableCollection<WerkTaak> takenVanDag = new();

    public async Task LoadTakenVanDagAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var taken = await db.WerkTaken
            .Include(t => t.WerkBon)
                .ThenInclude(w => w.Offerte)
                    .ThenInclude(o => o.Klant)
            .Include(t => t.OfferteRegel)
                .ThenInclude(r => r.TypeLijst)
            .Where(t => t.GeplandVan.Date == SelectedDate.Date)
            .OrderBy(t => t.GeplandVan)
            .ToListAsync();

        TakenVanDag = new ObservableCollection<WerkTaak>(taken);
    }

    // ───────── MAAND OVERZICHT ─────────

    public ObservableCollection<DayTile> MonthDays { get; } = new();
    public ObservableCollection<WeekSummary> WeekSummaries { get; } = new();

    [ObservableProperty] private ObservableCollection<DayRow> dayRows = new();
    [ObservableProperty] private ObservableCollection<WeekRow> weekRows = new();

    public async Task LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var firstOfMonth = new DateTime(Year, Month, 1);
        int offset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var start = firstOfMonth.AddDays(-offset);
        var end = start.AddDays(35);

        var taken = await db.WerkTaken
            .Where(t => t.GeplandVan >= start && t.GeplandVan < end)
            .ToListAsync();

        MonthDays.Clear();
        WeekSummaries.Clear();
        DayRows.Clear();

        for (int i = 0; i < 35; i++)
        {
            var date = start.AddDays(i);
            var dagTaken = taken.Where(t => t.GeplandVan.Date == date.Date).ToList();
            var used = dagTaken.Sum(x => x.DuurMinuten);
            var util = Math.Clamp((double)used / CapaciteitMinuten, 0, 1);

            var kleur = util switch
            {
                <= 0.5 => Brushes.LimeGreen,
                <= 0.75 => Brushes.Goldenrod,
                <= 0.9 => Brushes.OrangeRed,
                _ => Brushes.Red
            };

            bool isVandaag = date.Date == DateTime.Today;
            bool isAndereMaand = date.Month != Month;
            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            IBrush bg =
                isVandaag
                    ? new SolidColorBrush(Color.FromRgb(35, 45, 70))
                    : isAndereMaand
                        ? new SolidColorBrush(Color.FromRgb(30, 30, 30))
                        : isWeekend
                            ? new SolidColorBrush(Color.FromRgb(40, 40, 40))
                            : Brushes.Transparent;

            bool isSelected = date.Date == SelectedDate.Date;

            // BusyLabel: "3u 30m / 8u" (toon ook capaciteitsmaximum)
            string busyLabel = used == 0
                ? $"/ {CapaciteitMinuten / 60}u"
                : $"{used / 60}u {used % 60}m / {CapaciteitMinuten / 60}u";

            MonthDays.Add(new DayTile
            {
                Date = date,
                DayNumber = date.Day.ToString(),
                BusyLabel = busyLabel,
                Busy = util,
                BusyColor = kleur,
                Background = bg,
                Border = ComputeTileBorder(date, isSelected),
                IsSelected = isSelected
            });

            DayRows.Add(new DayRow
            {
                WeekNr = ISOWeek.GetWeekOfYear(date),
                Dag = date.ToString("ddd", CultureInfo.CurrentCulture),
                Datum = date.Date,
                Uren = used / 60,
                Minuten = used % 60,
                Kleur = kleur
            });
        }

        // Week summaries
        var weekStart = start;
        while (weekStart < end)
        {
            var weekEnd = weekStart.AddDays(7);
            var weekMinutes = MonthDays
                .Where(x => x.Date >= weekStart && x.Date < weekEnd)
                .Sum(x => (int)(x.Busy * CapaciteitMinuten));

            WeekSummaries.Add(new WeekSummary
            {
                Title = $"Week {ISOWeek.GetWeekOfYear(weekStart)}",
                Range = $"{weekStart:dd/MM} - {weekEnd.AddDays(-1):dd/MM}",
                TotalLabel = $"{weekMinutes / 60}u {weekMinutes % 60}m",
                Color = Brushes.LightGray
            });
            weekStart = weekEnd;
        }
    }

    // ───────── WEEKDETAIL ─────────

    public async Task LoadWeekRowsAsync(int weekNr)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var y = SelectedDate.Year;
        var weekStart = ISOWeek.ToDateTime(y, weekNr, DayOfWeek.Monday);
        var weekEnd = weekStart.AddDays(7);

        var taken = await db.WerkTaken
            .Include(t => t.WerkBon)
                .ThenInclude(w => w.Offerte)
                    .ThenInclude(o => o.Klant)
            .Include(t => t.OfferteRegel)
                .ThenInclude(r => r.TypeLijst)
            .Where(t => t.GeplandVan >= weekStart && t.GeplandVan < weekEnd)
            .OrderBy(t => t.GeplandVan)
            .ToListAsync();

        WeekRows.Clear();

        foreach (var t in taken)
        {
            var r = t.OfferteRegel;
            WeekRows.Add(new WeekRow
            {
                BonNr = t.WerkBonId,
                DuurMin = t.DuurMinuten,
                KlantNaam = t.WerkBon?.Offerte?.Klant?.Achternaam ?? "",
                Afmeting = r is null ? "" : $"{r.AantalStuks}× {r.BreedteCm}×{r.HoogteCm}",
                Lijst = r?.TypeLijst?.Artikelnummer ?? "",
                LijstType = r?.TypeLijst?.Soort ?? "",
                Van = t.GeplandVan,
                Tot = t.GeplandTot
            });
        }
    }
}

// ───────── SUPPORT CLASSES ─────────

public partial class DayTile : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = "";
    public string BusyLabel { get; set; } = "";
    public double Busy { get; set; }
    public IBrush BusyColor { get; set; } = Brushes.LimeGreen;
    public double BusyBarWidth => Busy * 120;

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
    public string UurMinText => $"{Uren:00}:{Minuten:00}";
}

public class WeekRow
{
    public int BonNr { get; set; }
    public int DuurMin { get; set; }
    public string KlantNaam { get; set; } = "";
    public string Afmeting { get; set; } = "";
    public string Lijst { get; set; } = "";
    public string LijstType { get; set; } = "";
    public DateTime Van { get; set; }
    public DateTime Tot { get; set; }
    public string Tijdstip => $"{Van:HH:mm} – {Tot:HH:mm}";
}
