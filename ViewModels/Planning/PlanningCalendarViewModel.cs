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

public partial class PlanningCalendarViewModel : AsyncViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IWerkBonWorkflowService _workflow;
    private readonly IToastService _toast;
    private readonly IWorkflowService _statusWorkflow;

    private const int CapaciteitMinuten = 8 * 60;

    // ───────── UITVOERING VM ─────────

    public PlanningUitvoeringViewModel Uitvoering { get; }

    // Injecteer vanuit de code-behind om een PlanningTijdDialog te tonen.
    // Wordt doorgestuurd naar de uitvoering VM.
    public Func<PlanningTijdDialogViewModel, Task<bool>>? ShowTijdDialogAsync
    {
        set => Uitvoering.ShowTijdDialogAsync = value;
    }

    // Forwarding commands — AXAML en code-behind refereren rechtstreeks aan de calendar VM.
    public IAsyncRelayCommand PlanGeselecteerdeRegelsCommand  => Uitvoering.PlanGeselecteerdeRegelsCommand;
    public IAsyncRelayCommand PlanPerRegelCommand             => Uitvoering.PlanPerRegelCommand;
    public IAsyncRelayCommand<WerkTaak> HerplanTaakCommand   => Uitvoering.HerplanTaakCommand;
    public IAsyncRelayCommand<WerkTaak> VerwijderTaakCommand => Uitvoering.VerwijderTaakCommand;

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
            Uitvoering.WerkBonId = value;
        }
    }

    public bool HeeftWerkBon => WerkBonId != 0;
    public bool GeenWerkBon => WerkBonId == 0;

    public PlanningCalendarViewModel(
        IDbContextFactory<AppDbContext> factory,
        IWerkBonWorkflowService workflow,
        IToastService toast,
        IWorkflowService statusWorkflow)
        : base(toast)
    {
        _factory = factory;
        _workflow = workflow;
        _toast = toast;
        _statusWorkflow = statusWorkflow;

        ToastMessages = toast.Messages;

        Uitvoering = new PlanningUitvoeringViewModel(
            factory, workflow, toast,
            RefreshAsync,
            IsDagGeblokkeerd);
        Uitvoering.SelectedDate = SelectedDate;

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

        var vm = new WeekWerkLijstViewModel(_factory, _statusWorkflow, _toast);
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
        set { SetProperty(ref _year, value); UpdateMonthTitle(); RunAsync(LoadAsync); }
    }

    private int _month = DateTime.Today.Month;
    public int Month
    {
        get => _month;
        set { SetProperty(ref _month, value); UpdateMonthTitle(); RunAsync(LoadAsync); }
    }

    private string _monthTitle = "";
    public string MonthTitle
    {
        get => _monthTitle;
        set => SetProperty(ref _monthTitle, value);
    }

    // Nederlandse cultuur voor maand-/dagnamen in de kalender (voluit geschreven, NL).
    private static readonly CultureInfo Nl = CultureInfo.GetCultureInfo("nl-BE");

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Nl) + s.Substring(1);

    private void UpdateMonthTitle() =>
        MonthTitle = Capitalize(new DateTime(Year, Month, 1).ToString("MMMM yyyy", Nl));

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

    // Voluit geschreven, Nederlandse dagnamen, maandag-eerst (maandag … zondag).
    public List<string> WeekHeaders =>
        Nl.DateTimeFormat.DayNames
            .Skip(1)
            .Concat(new[] { Nl.DateTimeFormat.DayNames[0] })
            .Select(Capitalize)
            .ToList();

    // ───────── SELECTED DAG ─────────

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    /// <summary>Geselecteerde dag voluit in het Nederlands (bv. "Vrijdag 20 juni 2026").</summary>
    public string SelectedDateLang => Capitalize(SelectedDate.ToString("dddd dd MMMM yyyy", Nl));

    partial void OnSelectedDayRowChanged(DayRow? value)
    {
        if (value is null) return;
        SelectedDate = value.Datum;
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        SelectedWeekNr = ISOWeek.GetWeekOfYear(value);
        OnPropertyChanged(nameof(SelectedDateLang));
        BuildWeekDayRows();
        UpdateTileSelection(value);
        RunAsync(LoadTakenVanDagAsync);
        RunAsync(() => LoadWeekRowsAsync(SelectedWeekNr));
        OnPropertyChanged(nameof(IsGeselecteerdeDagGeblokkeerd));
        OnPropertyChanged(nameof(BlokkeerDagButtonText));
        OnPropertyChanged(nameof(GeselecteerdeDagRedenTekst));
        Uitvoering.SelectedDate = value;
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
        Uitvoering.RegelsVanWerkBon = RegelsVanWerkBon;
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
        Uitvoering.RegelsVanWerkBon = RegelsVanWerkBon;
    }

    // ═══════════════════════════════════════════════════
    // BLOKKEER LOGICA
    // ═══════════════════════════════════════════════════

    private HashSet<DateTime> _geblokkeerd = new();
    private Dictionary<DateTime, string?> _geblokkeerdMetReden = new();

    [ObservableProperty] private string blokkeerReden = string.Empty;

    public bool IsGeselecteerdeDagGeblokkeerd => _geblokkeerd.Contains(SelectedDate.Date);

    public string BlokkeerDagButtonText =>
        IsGeselecteerdeDagGeblokkeerd ? "🔓 Deblokkeer dag" : "🔒 Blokkeer dag";

    public string GeselecteerdeDagRedenTekst =>
        _geblokkeerdMetReden.TryGetValue(SelectedDate.Date, out var r) && !string.IsNullOrWhiteSpace(r)
            ? r
            : string.Empty;

    private async Task<bool> IsDagGeblokkeerd(DateTime datum)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.GeblokkeerDagen.AnyAsync(g => g.Datum == datum.Date);
    }

    [RelayCommand]
    private async Task ToggleBlokkeerDagAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var datum = SelectedDate.Date;
        var bestaand = await db.GeblokkeerDagen.FirstOrDefaultAsync(g => g.Datum == datum);

        if (bestaand is not null)
        {
            db.GeblokkeerDagen.Remove(bestaand);
            BlokkeerReden = string.Empty;
            _toast.Success($"{datum:dd/MM} gedeblokkeerd.");
        }
        else
        {
            var reden = string.IsNullOrWhiteSpace(BlokkeerReden) ? null : BlokkeerReden.Trim();
            db.GeblokkeerDagen.Add(new GeblokkeerdeDag { Datum = datum, Reden = reden });
            _toast.Success($"{datum:dd/MM} geblokkeerd.");
        }

        await db.SaveChangesAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ToggleBlokkeerWeekAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var weekStart = ISOWeek.ToDateTime(SelectedDate.Year, SelectedWeekNr, DayOfWeek.Monday);
        var weekDagen = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i).Date).ToList();

        var bestaand = await db.GeblokkeerDagen
            .Where(g => weekDagen.Contains(g.Datum))
            .ToListAsync();

        if (bestaand.Count >= 5) // meeste dagen al geblokkeerd → deblokkeer
        {
            db.GeblokkeerDagen.RemoveRange(bestaand);
            _toast.Success($"Week {SelectedWeekNr} gedeblokkeerd.");
        }
        else
        {
            // Blokkeer alle dagen die nog niet geblokkeerd zijn
            var bestaandeDatums = bestaand.Select(g => g.Datum).ToHashSet();
            foreach (var dag in weekDagen.Where(d => !bestaandeDatums.Contains(d)))
            {
                db.GeblokkeerDagen.Add(new GeblokkeerdeDag { Datum = dag, Reden = "Week geblokkeerd" });
            }
            _toast.Success($"Week {SelectedWeekNr} geblokkeerd.");
        }

        await db.SaveChangesAsync();
        await RefreshAsync();
    }

    // ───────── REFRESH HELPER ─────────

    private async Task RefreshAsync()
    {
        await LoadAsync();
        await LoadTakenVanDagAsync();
        await LoadWeekRowsAsync(ISOWeek.GetWeekOfYear(SelectedDate));
        OnPropertyChanged(nameof(IsGeselecteerdeDagGeblokkeerd));
        OnPropertyChanged(nameof(BlokkeerDagButtonText));
        OnPropertyChanged(nameof(GeselecteerdeDagRedenTekst));
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
                .ThenInclude(r => r!.TypeLijst)
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

        // Geblokkeerde dagen laden
        var geblokkeerdeList = await db.GeblokkeerDagen
            .Where(g => g.Datum >= start && g.Datum < end)
            .ToListAsync();

        _geblokkeerd = geblokkeerdeList.Select(g => g.Datum.Date).ToHashSet();
        _geblokkeerdMetReden = geblokkeerdeList.ToDictionary(g => g.Datum.Date, g => g.Reden);

        MonthDays.Clear();
        WeekSummaries.Clear();
        DayRows.Clear();

        for (int i = 0; i < 35; i++)
        {
            var date = start.AddDays(i);
            var dagTaken = taken.Where(t => t.GeplandVan.Date == date.Date).ToList();
            var used = dagTaken.Sum(x => x.DuurMinuten);
            var isGeblokkeerd = _geblokkeerd.Contains(date.Date);
            var util = isGeblokkeerd ? 1.0 : Math.Clamp((double)used / CapaciteitMinuten, 0, 1);

            var kleur = isGeblokkeerd
                ? new SolidColorBrush(Color.Parse("#DC2626"))
                : util switch
                {
                    <= 0.5 => Brushes.LimeGreen,
                    <= 0.75 => Brushes.Goldenrod,
                    <= 0.9 => Brushes.OrangeRed,
                    _ => (IBrush)Brushes.Red
                };

            bool isVandaag = date.Date == DateTime.Today;
            bool isAndereMaand = date.Month != Month;
            bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            IBrush bg =
                isGeblokkeerd
                    ? new SolidColorBrush(Color.FromArgb(180, 60, 10, 10))
                    : isVandaag
                        ? new SolidColorBrush(Color.FromRgb(35, 45, 70))
                        : isAndereMaand
                            ? new SolidColorBrush(Color.FromRgb(30, 30, 30))
                            : isWeekend
                                ? new SolidColorBrush(Color.FromRgb(40, 40, 40))
                                : Brushes.Transparent;

            bool isSelected = date.Date == SelectedDate.Date;

            string busyLabel;
            if (isGeblokkeerd)
            {
                busyLabel = _geblokkeerdMetReden.TryGetValue(date.Date, out var reden) && !string.IsNullOrWhiteSpace(reden) ? $"🚫 {reden}" : "🚫 Geblokkeerd";
            }
            else
            {
                busyLabel = used == 0
                    ? $"/ {CapaciteitMinuten / 60}u"
                    : $"{used / 60}u {used % 60}m / {CapaciteitMinuten / 60}u";
            }

            MonthDays.Add(new DayTile
            {
                Date = date,
                DayNumber = isGeblokkeerd ? $"🚫 {date.Day}" : date.Day.ToString(),
                BusyLabel = busyLabel,
                Busy = util,
                BusyColor = kleur,
                Background = bg,
                Border = ComputeTileBorder(date, isSelected),
                IsSelected = isSelected,
                IsGeblokkeerd = isGeblokkeerd
            });

            DayRows.Add(new DayRow
            {
                WeekNr = ISOWeek.GetWeekOfYear(date),
                Dag = date.ToString("dd d", Nl),
                Datum = date.Date,
                Uren = used / 60,
                Minuten = used % 60,
                Kleur = kleur,
                IsGeblokkeerd = isGeblokkeerd
            });
        }

        // Week summaries
        var weekStart = start;
        while (weekStart < end)
        {
            var weekEnd = weekStart.AddDays(7);
            var weekMinutes = MonthDays
                .Where(x => x.Date >= weekStart && x.Date < weekEnd && !x.IsGeblokkeerd)
                .Sum(x => (int)(x.Busy * CapaciteitMinuten));

            var weekGeblokkeerd = MonthDays
                .Count(x => x.Date >= weekStart && x.Date < weekEnd && x.IsGeblokkeerd);

            var label = weekGeblokkeerd > 0
                ? $"{weekMinutes / 60}u {weekMinutes % 60}m · {weekGeblokkeerd}🚫"
                : $"{weekMinutes / 60}u {weekMinutes % 60}m";

            WeekSummaries.Add(new WeekSummary
            {
                Title = $"Week {ISOWeek.GetWeekOfYear(weekStart)}",
                Range = $"{weekStart:dd/MM} - {weekEnd.AddDays(-1):dd/MM}",
                TotalLabel = label,
                Color = Brushes.LightGray
            });
            weekStart = weekEnd;
        }

        OnPropertyChanged(nameof(IsGeselecteerdeDagGeblokkeerd));
        OnPropertyChanged(nameof(BlokkeerDagButtonText));
        OnPropertyChanged(nameof(GeselecteerdeDagRedenTekst));
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
                .ThenInclude(r => r!.TypeLijst)
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
                Dag = Capitalize(t.GeplandVan.ToString("ddd dd/MM", Nl))
            });
        }
    }
}
