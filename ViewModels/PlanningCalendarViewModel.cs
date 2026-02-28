using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model;
using QuadroApp.Model.DB;
using QuadroApp.Model.Toast;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Toast;
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
    [ObservableProperty] private DayRow? selectedDayRow;
    [ObservableProperty] private int selectedWeekNr;
    [ObservableProperty] private ObservableCollection<DayRow> weekDayRows = new();
    public IRelayCommand PrevMonthCommand { get; }
    public IRelayCommand NextMonthCommand { get; }
    public IRelayCommand TodayCommand { get; }

    public IAsyncRelayCommand OpenWeekWerkLijstCommand { get; }

    public ObservableCollection<ToastMessage> ToastMessages { get; }

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

        ToastMessages = ((ToastService)_toast).Messages;

        PrevMonthCommand = new RelayCommand(PrevMonth);
        NextMonthCommand = new RelayCommand(NextMonth);
        TodayCommand = new RelayCommand(SetToday);
        OpenWeekWerkLijstCommand = new AsyncRelayCommand(OpenWeekWerkLijstAsync);

        UpdateMonthTitle();
    }
    private async Task OpenWeekWerkLijstAsync()
    {
        var weekNr = System.Globalization.ISOWeek.GetWeekOfYear(SelectedDate);
        var year = SelectedDate.Year;

        var vm = new WeekWerkLijstViewModel(_factory, _statusWorkflow);
        await vm.InitializeAsync(year, weekNr);

        var win = new QuadroApp.Views.WeekWerkLijstWindow
        {
            DataContext = vm
        };

        if (App.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var owner = desktop.MainWindow;
            if (owner is null)
                return;

            await win.ShowDialog(owner);
        }
    }

    partial void OnSelectedDayRowChanged(DayRow? value)
    {
        if (value is null) return;
        SelectedDate = value.Datum;
    }
    // ───────── HEADER ─────────

    private int _year = DateTime.Today.Year;
    public int Year
    {
        get => _year;
        set
        {
            SetProperty(ref _year, value);
            UpdateMonthTitle();
            _ = LoadAsync();
        }
    }

    private int _month = DateTime.Today.Month;
    public int Month
    {
        get => _month;
        set
        {
            SetProperty(ref _month, value);
            UpdateMonthTitle();
            _ = LoadAsync();
        }
    }

    private string _monthTitle = "";
    public string MonthTitle
    {
        get => _monthTitle;
        set => SetProperty(ref _monthTitle, value);
    }

    private void UpdateMonthTitle()
    {
        MonthTitle = new DateTime(Year, Month, 1)
            .ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

    private void PrevMonth()
    {
        var d = new DateTime(Year, Month, 1).AddMonths(-1);
        Year = d.Year;
        Month = d.Month;
    }

    private void NextMonth()
    {
        var d = new DateTime(Year, Month, 1).AddMonths(1);
        Year = d.Year;
        Month = d.Month;
    }

    private void SetToday()
    {
        var t = DateTime.Today;
        Year = t.Year;
        Month = t.Month;
        SelectedDate = t;
    }
    public List<string> WeekHeaders =>
    CultureInfo.CurrentCulture.DateTimeFormat
        .AbbreviatedDayNames
        .Skip(1)      // start maandag
        .Concat(new[] {
            CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[0]
        })
        .ToList();
    // ───────── SELECTED DAG ─────────

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    partial void OnSelectedDateChanged(DateTime value)
    {
        SelectedWeekNr = ISOWeek.GetWeekOfYear(value);
        BuildWeekDayRows();

        _ = LoadTakenVanDagAsync();
        _ = LoadWeekRowsAsync(SelectedWeekNr);
    }

    // ───────── ACTIEVE WERKBON (die je aan het plannen bent) ─────────
    public int WerkBonId { get; private set; }

    [ObservableProperty]
    private ObservableCollection<RegelPlanItem> regelsVanWerkBon = new();
    public async Task InitializeGlobalAsync()
    {
        WerkBonId = 0;
        RegelsVanWerkBon = new ObservableCollection<RegelPlanItem>(); // leeg
        await LoadAsync();
        await LoadTakenVanDagAsync();
        await LoadWeekRowsAsync(System.Globalization.ISOWeek.GetWeekOfYear(SelectedDate));
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
        await LoadWeekRowsAsync(System.Globalization.ISOWeek.GetWeekOfYear(SelectedDate));
    }
    private void BuildWeekDayRows()
    {
        // filter uit DayRows naar alleen selected week
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

    // ───────── PLAN geselecteerde regels (RB) ─────────

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
            .Where(t => t.GeplandVan.Date == SelectedDate.Date)
            .ToListAsync();

        int totaal = dagTaken.Sum(t => t.DuurMinuten);
        int extra = regels.Sum(CalcMinutenVoorRegel);

        if (totaal + extra > 480)
        {
            _toast.Error("Deze dag is overboekt.");
            return;
        }


        foreach (var r in regels)
        {
            int duur = CalcMinutenVoorRegel(r);
            await _workflow.VoegPlanningToeVoorRegelAsync(
                WerkBonId,
                r.Id,
                SelectedDate.Date,
                duur,
                "Inlijsten"
            );
        }

        await LoadAsync();
        await LoadTakenVanDagAsync();
        await LoadWeekRowsAsync(System.Globalization.ISOWeek.GetWeekOfYear(SelectedDate));
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

        // Maandag als start
        int offset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var start = firstOfMonth.AddDays(-offset);

        // 👉 5 weken = 35 dagen
        var end = start.AddDays(35);

        var taken = await db.WerkTaken
            .Where(t => t.GeplandVan >= start && t.GeplandVan < end)
            .ToListAsync();

        MonthDays.Clear();
        WeekSummaries.Clear();
        DayRows.Clear();

        const int capaciteit = 8 * 60;
        var vandaag = DateTime.Today;

        for (int i = 0; i < 35; i++)
        {
            var date = start.AddDays(i);

            var dagTaken = taken.Where(t => t.GeplandVan.Date == date.Date).ToList();

            var used = dagTaken.Sum(x => x.DuurMinuten);
            var util = Math.Clamp((double)used / capaciteit, 0, 1);

            var kleur = util switch
            {
                <= 0.5 => Brushes.LimeGreen,
                <= 0.75 => Brushes.Goldenrod,
                <= 0.9 => Brushes.OrangeRed,
                _ => Brushes.Red
            };

            // ✅ Vandaag highlighten
            var isVandaag = date.Date == DateTime.Today;
            var isAndereMaand = date.Month != Month;
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday
                         || date.DayOfWeek == DayOfWeek.Sunday;
            var isMaandag = date.DayOfWeek == DayOfWeek.Monday;
            IBrush bg =
    isVandaag
        ? new SolidColorBrush(Color.FromRgb(35, 45, 70))      // vandaag
        : isAndereMaand
            ? new SolidColorBrush(Color.FromRgb(30, 30, 30))  // andere maand
            : isMaandag
                ? new SolidColorBrush(Color.FromRgb(70, 25, 25)) // 🔴 maandag
                : isWeekend
                    ? new SolidColorBrush(Color.FromRgb(40, 40, 40)) // weekend
                    : Brushes.Transparent;

            IBrush border =
        isVandaag
            ? Brushes.DeepSkyBlue
            : isMaandag
                ? Brushes.Red
                : new SolidColorBrush(Color.FromRgb(70, 70, 70));

            MonthDays.Add(new DayTile
            {
                Date = date,
                DayNumber = date.Day.ToString(),
                BusyLabel = used == 0 ? "" : $"{used / 60}u {used % 60}m",
                Busy = util,
                BusyColor = kleur,
                Background = bg,
                Border = border
            });

            DayRows.Add(new DayRow
            {
                WeekNr = System.Globalization.ISOWeek.GetWeekOfYear(date),
                Dag = date.ToString("ddd", CultureInfo.CurrentCulture),
                Datum = date.Date,
                Uren = used / 60,
                Minuten = used % 60,
                Kleur = kleur
            });
        }

        // ✅ Week summaries enkel 5 weken
        var weekStart = start;
        while (weekStart < end)
        {
            var weekEnd = weekStart.AddDays(7);

            var weekMinutes = MonthDays
                .Where(x => x.Date >= weekStart && x.Date < weekEnd)
                .Sum(x => (int)(x.Busy * capaciteit));

            WeekSummaries.Add(new WeekSummary
            {
                Title = $"Week {System.Globalization.ISOWeek.GetWeekOfYear(weekStart)}",
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
        var weekStart = System.Globalization.ISOWeek.ToDateTime(y, weekNr, DayOfWeek.Monday);
        var weekEnd = weekStart.AddDays(7);

        var taken = await db.WerkTaken
            .Include(t => t.WerkBon)
                .ThenInclude(w => w.Offerte)
                    .ThenInclude(o => o.Klant)
            .Include(t => t.OfferteRegel)
                .ThenInclude(r => r.TypeLijst)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Glas)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.PassePartout1)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.PassePartout2)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.DiepteKern)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Opkleven)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Rug)
            .Where(t => t.GeplandVan >= weekStart && t.GeplandVan < weekEnd)
            .OrderBy(t => t.GeplandVan)
            .ToListAsync();

        WeekRows.Clear();
        Console.WriteLine($"[WeekRows] week {weekNr} range {weekStart:yyyy-MM-dd}..{weekEnd:yyyy-MM-dd}");
        Console.WriteLine($"[WeekRows] taken count = {taken.Count}");
        foreach (var t in taken)
        {
            var r = t.OfferteRegel;

            Console.WriteLine(
                $"TaakId={t.Id} WerkBonId={t.WerkBonId} OfferteRegelId={t.OfferteRegelId} " +
                $"GeplandVan={t.GeplandVan:yyyy-MM-dd HH:mm} Duur={t.DuurMinuten} " +
                $"RegelNull={(r is null)}"
            );

            if (r != null)
            {
                Console.WriteLine(
                    $"  RegelId={r.Id} Stuks={r.AantalStuks} " +
                    $"B={r.BreedteCm} H={r.HoogteCm} " +
                    $"TypeLijst={(r.TypeLijst?.Artikelnummer ?? "NULL")} " +
                    $"Legacy={(r.LegacyCode ?? "NULL")}"
                );
            }
            // ook tonen als OfferteRegel ontbreekt (zodat je tenminste iets ziet)
            WeekRows.Add(new WeekRow
            {
                BonNr = t.WerkBonId,
                DuurMin = t.DuurMinuten,
                Stuks = r?.AantalStuks ?? 0,
                Breedte = r?.BreedteCm ?? 0,
                Hoogte = r?.HoogteCm ?? 0,
                G = (r?.Glas?.Volgnummer ?? 0).ToString(),
                P1 = (r?.PassePartout1?.Volgnummer ?? 0).ToString(),
                P2 = (r?.PassePartout2?.Volgnummer ?? 0).ToString(),
                D = (r?.DiepteKern?.Volgnummer ?? 0).ToString(),
                O = (r?.Opkleven?.Volgnummer ?? 0).ToString(),
                R = (r?.Rug?.Volgnummer ?? 0).ToString(),
                Inleg1 = r is null ? "(geen regel)" : $"{r.InlegBreedteCm}×{r.InlegHoogteCm}",
                Lijst = r?.TypeLijst?.Artikelnummer ?? "(geen lijst)",
                Van = t.GeplandVan,
                LijstType = r.TypeLijst?.Artikelnummer ?? "",
                Tot = t.GeplandTot
            });
        }
    }
}

// ───────── SUPPORT CLASSES (TOP LEVEL) ─────────

public class DayTile
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = "";
    public string BusyLabel { get; set; } = "";
    public double Busy { get; set; }
    public IBrush BusyColor { get; set; } = Brushes.LimeGreen;
    public double BusyBarWidth => Busy * 120;
    public IBrush Background { get; set; } = Brushes.Transparent;
    public IBrush Border { get; set; } = Brushes.Gray;
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
    public int Stuks { get; set; }
    public decimal Breedte { get; set; }
    public decimal Hoogte { get; set; }
    public string LijstType { get; set; } = "";
    public string G { get; set; } = "";
    public string P1 { get; set; } = "";
    public string P2 { get; set; } = "";
    public string D { get; set; } = "";
    public string O { get; set; } = "";
    public string R { get; set; } = "";

    public string Inleg1 { get; set; } = "";
    public string Inleg2 { get; set; } = "";
    public string Lijst { get; set; } = "";

    public DateTime Van { get; set; }
    public DateTime Tot { get; set; }
}