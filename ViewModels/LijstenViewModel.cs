using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using QuadroApp.Services.Import;
using QuadroApp.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class LijstenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ILijstDialogService _lijstDialog;
    private readonly TypeLijstImportDefinition _typeLijstImportDefinition;
    private readonly ICrudValidator<TypeLijst> _validator;
    private readonly IToastService _toast;
    public TypeLijst? Selected => GeselecteerdeLijst;
    [ObservableProperty]
    private string? leverancierZoekterm;



    [ObservableProperty]
    private ObservableCollection<Leverancier> gefilterdeLeveranciers = new();



    // ======= LOGGING =======
    private const string LogPrefix = "[LijstenVM]";
    private static void Log(
        string message,
        Exception? ex = null,
        [CallerMemberName] string caller = "",
        [CallerLineNumber] int line = 0)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss.fff");
        var tid = Thread.CurrentThread.ManagedThreadId;
        var full = $"{ts} {LogPrefix} (T{tid}) {caller}:{line} | {message}";
        if (ex is null)
        {
            Debug.WriteLine(full);
            Console.WriteLine(full);
        }
        else
        {
            Debug.WriteLine(full);
            Debug.WriteLine(ex);
            Console.WriteLine(full);
            Console.WriteLine(ex);
        }
    }

    private string DumpState(string tag = "")
    {
        var sel = GeselecteerdeLijst is null ? "null" : $"{GeselecteerdeLijst.Id}/{GeselecteerdeLijst.Artikelnummer}";
        var lijstenCount = Lijsten?.Count ?? 0;
        var filteredCount = FilteredLijsten?.Count ?? 0;
        var pagedCount = PagedLijsten?.Count ?? 0;

        // TotalPages kan nu safe gebruikt worden, maar we houden het toch robuust
        var totalPages = TotalPages;

        return $"{tag} State => IsBusy={IsBusy}, Zoekterm='{Zoekterm}', Lijsten={lijstenCount}, Filtered={filteredCount}, Paged={pagedCount}, Page={CurrentPage}/{totalPages}, Selected={sel}, DetailOpen={IsDetailOpen}";
    }

    public ObservableCollection<Leverancier> AlleLeveranciers { get; } = new();

    [ObservableProperty] private ObservableCollection<TypeLijst> lijsten = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> filteredLijsten = new();
    [ObservableProperty] private List<Leverancier> leveranciers = new();
    [ObservableProperty]
    private Leverancier? selectedLeverancier;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private TypeLijst? geselecteerdeLijst;

    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private string zoekterm = string.Empty;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    // =========================
    // PAGING
    // =========================
    [ObservableProperty] private ObservableCollection<TypeLijst> pagedLijsten = new();

    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int pageSize = 10;

    public int TotalPages
    {
        get
        {
            var count = FilteredLijsten?.Count ?? 0;
            if (count == 0) return 1;
            return (int)Math.Ceiling(count / (double)PageSize);
        }
    }

    public bool CanPrevPage => CurrentPage > 1;
    public bool CanNextPage => CurrentPage < TotalPages;

    public IRelayCommand PrevPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }

    public int AantalLijsten => FilteredLijsten?.Count ?? 0;

    public LijstenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        ILijstDialogService lijstDialog,
        TypeLijstImportDefinition typeLijstImportDefinition,
        ICrudValidator<TypeLijst> validator,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _nav = nav;
        _dialogs = dialogs;
        _lijstDialog = lijstDialog;
        _typeLijstImportDefinition = typeLijstImportDefinition;

        _validator = validator;
        _toast = toast;

        // rest van jouw ctor...


        Log("CTOR start");

        PrevPageCommand = new RelayCommand(
            execute: () =>
            {
                Log($"PrevPage clicked. {DumpState("BEFORE")}");
                if (!CanPrevPage) { Log("PrevPage blocked (CanPrevPage=false)"); return; }
                CurrentPage--;
                UpdatePagedLijsten();
                Log($"PrevPage done. {DumpState("AFTER")}");
            },
            canExecute: () => CanPrevPage
        );

        NextPageCommand = new RelayCommand(
            execute: () =>
            {
                Log($"NextPage clicked. {DumpState("BEFORE")}");
                if (!CanNextPage) { Log("NextPage blocked (CanNextPage=false)"); return; }
                CurrentPage++;
                UpdatePagedLijsten();
                Log($"NextPage done. {DumpState("AFTER")}");
            },
            canExecute: () => CanNextPage
        );

        Log($"CTOR done. {DumpState("INIT")}");
    }
    partial void OnSelectedLeverancierChanged(Leverancier? value)
    {
        if (GeselecteerdeLijst is not null)
        {
            GeselecteerdeLijst.Leverancier = value;
            GeselecteerdeLijst.LeverancierId = (int)(value?.Id);
        }
    }
    private void SyncSelectedLeverancierFromLijst()
    {
        if (GeselecteerdeLijst is null)
        {
            SelectedLeverancier = null;
            return;
        }

        // prefer FK
        var id = GeselecteerdeLijst.LeverancierId;

        if (id > 0)
        {
            SelectedLeverancier = Leveranciers.FirstOrDefault(l => l.Id == id);
            return;
        }

        // fallback: op code als Id ontbreekt
        var code = (GeselecteerdeLijst.Leverancier?.Code ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(code))
            SelectedLeverancier = Leveranciers.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
    }
    public async Task InitializeAsync()
    {
        Log($"InitializeAsync start. {DumpState("BEFORE")}");
        await LoadAsync();
        Log($"InitializeAsync done. {DumpState("AFTER")}");
    }

    partial void OnZoektermChanged(string value)
    {
        Log($"OnZoektermChanged => '{value}'. {DumpState("BEFORE")}");
        ApplyFilter();
        Log($"OnZoektermChanged done. {DumpState("AFTER")}");
    }

    partial void OnGeselecteerdeLijstChanged(TypeLijst? value)
    {
        Log($"OnGeselecteerdeLijstChanged => {(value is null ? "null" : $"{value.Id}/{value.Artikelnummer}")}. {DumpState("BEFORE")}");
        IsDetailOpen = value is not null;
        OnPropertyChanged(nameof(VerkoopPrijsPreview));
        SyncSelectedLeverancierFromLijst();

        Log($"OnGeselecteerdeLijstChanged done. {DumpState("AFTER")}");
    }

    partial void OnFilteredLijstenChanged(ObservableCollection<TypeLijst> value)
    {
        Log($"OnFilteredLijstenChanged => Count={value?.Count ?? 0}. {DumpState("BEFORE")}");
        CurrentPage = 1;
        UpdatePagedLijsten();
        OnPropertyChanged(nameof(AantalLijsten));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();
        Log($"OnFilteredLijstenChanged done. {DumpState("AFTER")}");
    }

    partial void OnPageSizeChanged(int value)
    {
        Log($"OnPageSizeChanged => {value}. {DumpState("BEFORE")}");

        if (value <= 0) PageSize = 10;
        CurrentPage = 1;
        UpdatePagedLijsten();
        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();

        Log($"OnPageSizeChanged done. {DumpState("AFTER")}");
    }

    partial void OnCurrentPageChanged(int value)
    {
        Log($"OnCurrentPageChanged => {value}. {DumpState("BEFORE")}");

        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        UpdatePagedLijsten();
        NotifyPagingCanExecute();

        Log($"OnCurrentPageChanged done. {DumpState("AFTER")}");
    }

    private void NotifyPagingCanExecute()
    {
        Log($"NotifyPagingCanExecute. CanPrev={CanPrevPage}, CanNext={CanNextPage}. {DumpState()}");
        (PrevPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NextPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrevPage));
        OnPropertyChanged(nameof(CanNextPage));
    }

    private void UpdatePagedLijsten()
    {
        Log($"UpdatePagedLijsten start. {DumpState("BEFORE")}");

        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;

        var page = FilteredLijsten
            .Skip(skip)
            .Take(PageSize)
            .ToList();

        PagedLijsten = new ObservableCollection<TypeLijst>(page);

        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();

        Log($"UpdatePagedLijsten done. skip={skip}, take={PageSize}, pageCount={PagedLijsten.Count}. {DumpState("AFTER")}");
    }

    [ObservableProperty] private decimal breedte = 30m;
    [ObservableProperty] private decimal hoogte = 40m;
    [ObservableProperty] private decimal werkloon = 15m;

    public string VerkoopPrijsPreview =>
        GeselecteerdeLijst == null
            ? "Selecteer een lijst om prijs te berekenen"
            : $"💰 Geschatte verkoopprijs: € {BerekenPrijs(GeselecteerdeLijst, Breedte, Hoogte, Werkloon):F2}";

    private static decimal BerekenPrijs(TypeLijst lijst, decimal breedteCm, decimal hoogteCm, decimal werkloon)
    {
        var lengteMeter = ((breedteCm + hoogteCm) * 2m + 10m * (lijst.BreedteCm / 10m)) / 100m;
        var kost = lijst.PrijsPerMeter * lengteMeter;
        var marge = kost * lijst.WinstMargeFactor;
        var afval = kost * (lijst.AfvalPercentage / 100m);
        var werk = lijst.WerkMinuten > 0 ? werkloon * (lijst.WerkMinuten / 60m) : 0m;

        return marge + afval + lijst.VasteKost;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Log($"LoadAsync start. {DumpState("BEFORE")}");

        try
        {
            if (IsBusy) { Log("LoadAsync blocked: IsBusy=true"); return; }

            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();
            Log("DbContext created");

            Log("Loading leveranciers...");
            Leveranciers = await db.Leveranciers
                .AsNoTracking()
                .OrderBy(l => l.Code)
                .ThenBy(l => l.Naam)
                .ToListAsync();
            Log($"Loaded leveranciers: {Leveranciers.Count}");

            Log("Loading typelijsten (Include Leverancier)...");
            var lijstenData = await db.TypeLijsten
                .Include(t => t.Leverancier)
                .AsNoTracking()
                .OrderBy(t => t.Artikelnummer)
                .ToListAsync();
            Log($"Loaded typelijsten: {lijstenData.Count}");

            Lijsten = new ObservableCollection<TypeLijst>(lijstenData);
            Log($"Lijsten set. Count={Lijsten.Count}");

            FilteredLijsten = new ObservableCollection<TypeLijst>(Lijsten);
            Log($"FilteredLijsten set. Count={FilteredLijsten.Count}");
            GefilterdeLeveranciers = new ObservableCollection<Leverancier>(Leveranciers);

            // ✅ Sync SelectedLeverancier met huidige lijst (indien detail open is)
            SyncSelectedLeverancierFromLijst();
            UpdatePagedLijsten();
            Log($"After paging. Paged={PagedLijsten.Count}");

            OnPropertyChanged(nameof(VerkoopPrijsPreview));

            Log($"LoadAsync done OK. {DumpState("AFTER")}");
        }
        catch (Exception ex)
        {
            Foutmelding = $"❌ Fout bij laden lijsten: {ex.Message}";
            Log($"LoadAsync FAILED: {Foutmelding}", ex);
            await _dialogs.ShowErrorAsync("Laden mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            Log($"LoadAsync finally. {DumpState("FINALLY")}");
        }
    }

    private void ApplyFilter()
    {
        Log($"ApplyFilter start. Zoekterm='{Zoekterm}'. {DumpState("BEFORE")}");

        if (Lijsten.Count == 0)
        {
            FilteredLijsten = new ObservableCollection<TypeLijst>();
            Log("ApplyFilter: Lijsten empty => FilteredLijsten empty");
            return;
        }

        if (string.IsNullOrWhiteSpace(Zoekterm))
        {
            FilteredLijsten = new ObservableCollection<TypeLijst>(Lijsten);
            Log($"ApplyFilter: empty term => {FilteredLijsten.Count} items");
        }
        else
        {
            var term = Zoekterm.Trim().ToLowerInvariant();
            var filtered = Lijsten.Where(l =>
                (l.Artikelnummer ?? "").ToLowerInvariant().Contains(term) ||
                (l.Leverancier?.Code ?? "").ToLowerInvariant().Contains(term) ||
                (l.Leverancier?.Naam ?? "").ToLowerInvariant().Contains(term)
            );

            var list = filtered.ToList();
            FilteredLijsten = new ObservableCollection<TypeLijst>(list);
            Log($"ApplyFilter: term='{term}' => {FilteredLijsten.Count} items");
        }

        Log($"ApplyFilter done. {DumpState("AFTER")}");
    }

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        Log($"GaTerugAsync start. {DumpState("BEFORE")}");
        await _nav.NavigateToAsync<HomeViewModel>();
        Log("GaTerugAsync done");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Log($"RefreshAsync start. {DumpState("BEFORE")}");
        await LoadAsync();
        Log($"RefreshAsync done. {DumpState("AFTER")}");
    }

    private bool CanEdit() => GeselecteerdeLijst is not null;
    private bool CanDelete() => GeselecteerdeLijst is not null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync()
    {
        Log($"EditAsync start. Selected={(GeselecteerdeLijst is null ? "null" : GeselecteerdeLijst.Id.ToString())}. {DumpState("BEFORE")}");

        if (GeselecteerdeLijst is null) return;

        try
        {
            var ok = await _lijstDialog.EditAsync(GeselecteerdeLijst);
            Log($"Edit dialog result => {(ok is null ? "null" : ok.ToString())}");

            if (ok == null) return;

            await SaveAsync();
            Log("EditAsync saved OK");
        }
        catch (Exception ex)
        {
            Foutmelding = $"❌ Bewerken mislukt: {ex.Message}";
            Log($"EditAsync FAILED: {Foutmelding}", ex);
            await _dialogs.ShowErrorAsync("Bewerken mislukt", Foutmelding);
        }
        finally
        {
            Log($"EditAsync finally. {DumpState("FINALLY")}");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (GeselecteerdeLijst is null) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            // ✅ Zet FK vanuit combobox selectie (belangrijk!)
            GeselecteerdeLijst.LeverancierId = SelectedLeverancier?.Id ?? GeselecteerdeLijst.LeverancierId;

            // Normalize
            GeselecteerdeLijst.Artikelnummer = (GeselecteerdeLijst.Artikelnummer ?? "").Trim();
            GeselecteerdeLijst.Soort = (GeselecteerdeLijst.Soort ?? "").Trim();
            GeselecteerdeLijst.Opmerking = (GeselecteerdeLijst.Opmerking ?? "").Trim();
            GeselecteerdeLijst.LaatsteUpdate = DateTime.Now;

            // ✅ Validatie
            var vr = await _validator.ValidateUpdateAsync(GeselecteerdeLijst);

            var warn = vr.WarningText();
            if (!string.IsNullOrWhiteSpace(warn)) _toast.Warning(warn);

            if (!vr.IsValid)
            {
                _toast.Error(vr.ErrorText());
                return;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();

            db.TypeLijsten.Attach(GeselecteerdeLijst);
            db.Entry(GeselecteerdeLijst).State = EntityState.Modified;

            // voorkom EF navigation gedoe
            db.Entry(GeselecteerdeLijst).Reference(x => x.Leverancier).IsModified = false;

            await db.SaveChangesAsync();

            if (GeselecteerdeLijst.VoorraadMeter < GeselecteerdeLijst.MinimumVoorraad)
            {
                _toast.Warning($"Voorraad bijna op voor lijst {GeselecteerdeLijst.Artikelnummer}");
            }

            _toast.Success("Lijst opgeslagen.");
            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeLijst = null;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"Opslaan mislukt: {msg}";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        Log("ImportExcelAsync start");

        try
        {
            if (IsBusy) return;
            IsBusy = true;

            var ok = await _dialogs.ShowUnifiedImportPreviewAsync(_typeLijstImportDefinition);
            if (!ok)
            {
                return;
            }

            await LoadAsync();
            _toast.Success("Import van lijsten voltooid.");
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"❌ Import mislukt: {msg}";
            Log(Foutmelding, ex);
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
            Log("ImportExcelAsync done");
        }
    }


    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        Log($"DeleteAsync start. {DumpState("BEFORE")}");

        if (GeselecteerdeLijst is null)
        {
            Log("DeleteAsync aborted: GeselecteerdeLijst=null");
            return;
        }

        var ok = await _dialogs.ConfirmAsync(
            "Lijst verwijderen",
            $"Ben je zeker dat je lijst '{GeselecteerdeLijst.Artikelnummer}' wil verwijderen?"
        );
        Log($"Delete confirm => ok={ok}");
        if (!ok) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();
            Log($"DeleteAsync: DbContext created. Removing TypeLijst Id={GeselecteerdeLijst.Id}");
            var vr = await _validator.ValidateDeleteAsync(GeselecteerdeLijst);
            if (!vr.IsValid)
            {
                _toast.Error(vr.ErrorText());
                return;
            }
            db.TypeLijsten.Remove(new TypeLijst { Id = GeselecteerdeLijst.Id });
            var saved = await db.SaveChangesAsync();
            Log($"DeleteAsync: SaveChangesAsync => {saved} rows");

            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeLijst = null;

            Log($"DeleteAsync done OK. {DumpState("AFTER")}");
        }
        catch (Exception ex)
        {
            Foutmelding = $"❌ Verwijderen mislukt: {ex.Message}";
            Log($"DeleteAsync FAILED: {Foutmelding}", ex);
            await _dialogs.ShowErrorAsync("Verwijderen mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
            Log($"DeleteAsync finally. {DumpState("FINALLY")}");
        }

    }
    partial void OnLeverancierZoektermChanged(string? value)
    {
        FilterLeveranciers();
    }

    private void FilterLeveranciers()
    {
        if (Leveranciers is null) return;

        IEnumerable<Leverancier> filtered;

        if (string.IsNullOrWhiteSpace(LeverancierZoekterm))
        {
            filtered = Leveranciers;
        }
        else
        {
            var term = LeverancierZoekterm.Trim().ToLowerInvariant();
            filtered = Leveranciers.Where(l =>
                (l.Code ?? "").ToLowerInvariant().Contains(term) ||
                (l.Naam ?? "").ToLowerInvariant().Contains(term));
        }

        var list = filtered.ToList();

        // ✅ hou de selected in de dropdown zelfs als filter hem wegduwt
        if (SelectedLeverancier is not null && list.All(x => x.Id != SelectedLeverancier.Id))
            list.Insert(0, SelectedLeverancier);

        GefilterdeLeveranciers = new ObservableCollection<Leverancier>(list);
    }


}
