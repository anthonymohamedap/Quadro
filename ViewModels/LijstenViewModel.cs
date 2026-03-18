using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using QuadroApp.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty]
    private string? leverancierZoekterm;

    [ObservableProperty]
    private ObservableCollection<Leverancier> gefilterdeLeveranciers = new();

    [ObservableProperty]
    private ObservableCollection<TypeLijst> lijsten = new();

    [ObservableProperty]
    private ObservableCollection<TypeLijst> filteredLijsten = new();

    [ObservableProperty]
    private List<Leverancier> leveranciers = new();

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

    [ObservableProperty] private ObservableCollection<TypeLijst> pagedLijsten = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> selectedLijsten = new();
    [ObservableProperty] private int currentPage = 1;
    [ObservableProperty] private int pageSize = 10;

    public TypeLijst? Selected => GeselecteerdeLijst;

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
    public int AantalLijsten => FilteredLijsten?.Count ?? 0;

    public IRelayCommand PrevPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }

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

        PrevPageCommand = new RelayCommand(
            execute: () =>
            {
                if (!CanPrevPage) return;
                CurrentPage--;
                UpdatePagedLijsten();
            },
            canExecute: () => CanPrevPage
        );

        NextPageCommand = new RelayCommand(
            execute: () =>
            {
                if (!CanNextPage) return;
                CurrentPage++;
                UpdatePagedLijsten();
            },
            canExecute: () => CanNextPage
        );
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilter();

    partial void OnGeselecteerdeLijstChanged(TypeLijst? value)
    {
        IsDetailOpen = value is not null;
        SyncSelectedLeverancierFromLijst();
    }

    partial void OnFilteredLijstenChanged(ObservableCollection<TypeLijst> value)
    {
        CurrentPage = 1;
        UpdatePagedLijsten();
        OnPropertyChanged(nameof(AantalLijsten));
        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();
    }

    partial void OnPageSizeChanged(int value)
    {
        if (value <= 0) PageSize = 10;
        CurrentPage = 1;
        UpdatePagedLijsten();
        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();
    }

    partial void OnCurrentPageChanged(int value)
    {
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        UpdatePagedLijsten();
        NotifyPagingCanExecute();
    }

    partial void OnSelectedLeverancierChanged(Leverancier? value)
    {
        if (GeselecteerdeLijst is null)
        {
            return;
        }

        GeselecteerdeLijst.Leverancier = value;
        GeselecteerdeLijst.LeverancierId = value?.Id ?? GeselecteerdeLijst.LeverancierId;
    }

    partial void OnLeverancierZoektermChanged(string? value) => FilterLeveranciers();

    private void NotifyPagingCanExecute()
    {
        (PrevPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (NextPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrevPage));
        OnPropertyChanged(nameof(CanNextPage));
    }

    private void UpdatePagedLijsten()
    {
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = FilteredLijsten.Skip(skip).Take(PageSize).ToList();
        PagedLijsten = new ObservableCollection<TypeLijst>(page);

        OnPropertyChanged(nameof(TotalPages));
        NotifyPagingCanExecute();
    }

    private void SyncSelectedLeverancierFromLijst()
    {
        if (GeselecteerdeLijst is null)
        {
            SelectedLeverancier = null;
            return;
        }

        var id = GeselecteerdeLijst.LeverancierId;
        if (id > 0)
        {
            SelectedLeverancier = Leveranciers.FirstOrDefault(l => l.Id == id);
            return;
        }

        var naam = (GeselecteerdeLijst.Leverancier?.Naam ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(naam))
        {
            SelectedLeverancier = Leveranciers.FirstOrDefault(l =>
                string.Equals(l.Naam, naam, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            Leveranciers = await db.Leveranciers
                .AsNoTracking()
                .OrderBy(l => l.Naam)
                .ToListAsync();

            var lijstenData = await db.TypeLijsten
                .Include(t => t.Leverancier)
                .AsNoTracking()
                .OrderBy(t => t.Artikelnummer)
                .ToListAsync();

            Lijsten = new ObservableCollection<TypeLijst>(lijstenData);
            FilteredLijsten = new ObservableCollection<TypeLijst>(Lijsten);
            GefilterdeLeveranciers = new ObservableCollection<Leverancier>(Leveranciers);

            SyncSelectedLeverancierFromLijst();
            UpdatePagedLijsten();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden lijsten: {ex.Message}";
            await _dialogs.ShowErrorAsync("Laden mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<HomeViewModel>();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private bool CanEdit() => GeselecteerdeLijst is not null;
    private bool CanDelete() => GeselecteerdeLijst is not null;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync()
    {
        if (GeselecteerdeLijst is null) return;

        try
        {
            var ok = await _lijstDialog.EditAsync(GeselecteerdeLijst);
            if (ok is null) return;

            await SaveAsync();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Bewerken mislukt: {ex.Message}";
            await _dialogs.ShowErrorAsync("Bewerken mislukt", Foutmelding);
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

            GeselecteerdeLijst.LeverancierId = SelectedLeverancier?.Id ?? GeselecteerdeLijst.LeverancierId;
            GeselecteerdeLijst.Artikelnummer = (GeselecteerdeLijst.Artikelnummer ?? string.Empty).Trim();
            GeselecteerdeLijst.Soort = (GeselecteerdeLijst.Soort ?? string.Empty).Trim();
            GeselecteerdeLijst.LaatsteUpdate = DateTime.Now;

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
            Foutmelding = $"Import mislukt: {msg}";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (GeselecteerdeLijst is null)
        {
            return;
        }

        var ok = await _dialogs.ConfirmAsync(
            "Lijst verwijderen",
            $"Ben je zeker dat je lijst '{GeselecteerdeLijst.Artikelnummer}' wil verwijderen?");

        if (!ok) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var vr = await _validator.ValidateDeleteAsync(GeselecteerdeLijst);
            if (!vr.IsValid)
            {
                _toast.Error(vr.ErrorText());
                return;
            }

            db.TypeLijsten.Remove(new TypeLijst { Id = GeselecteerdeLijst.Id });
            await db.SaveChangesAsync();

            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeLijst = null;
        }
        catch (Exception ex)
        {
            Foutmelding = $"Verwijderen mislukt: {ex.Message}";
            await _dialogs.ShowErrorAsync("Verwijderen mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        if (Lijsten.Count == 0)
        {
            FilteredLijsten = new ObservableCollection<TypeLijst>();
            return;
        }

        if (string.IsNullOrWhiteSpace(Zoekterm))
        {
            FilteredLijsten = new ObservableCollection<TypeLijst>(Lijsten);
            return;
        }

        var term = Zoekterm.Trim().ToLowerInvariant();
        var filtered = Lijsten.Where(l =>
            (l.Artikelnummer ?? string.Empty).ToLowerInvariant().Contains(term) ||
            (l.Leverancier?.Naam ?? string.Empty).ToLowerInvariant().Contains(term));

        FilteredLijsten = new ObservableCollection<TypeLijst>(filtered);
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
                (l.Naam ?? string.Empty).ToLowerInvariant().Contains(term));
        }

        var list = filtered.ToList();

        if (SelectedLeverancier is not null && list.All(x => x.Id != SelectedLeverancier.Id))
            list.Insert(0, SelectedLeverancier);

        GefilterdeLeveranciers = new ObservableCollection<Leverancier>(list);
    }

    public void UpdateSelectedLijsten(IEnumerable<TypeLijst> selectedItems)
    {
        SelectedLijsten.Clear();
        foreach (var item in selectedItems.DistinctBy(x => x.Id))
        {
            SelectedLijsten.Add(item);
        }
    }

    [RelayCommand]
    private async Task BulkPrijsUpdateAsync()
    {
        var vm = new BulkLijstenViewModel(_dbFactory, _validator, _toast)
        {
            RefreshRequested = async () => await LoadAsync()
        };
        await vm.InitializeAsync();

        var window = new BulkLijstenWindow
        {
            DataContext = vm
        };

        vm.RequestClose = confirmed => window.Close(confirmed);

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.Show();
            return;
        }

        var owner = desktop.MainWindow;
        if (owner is null)
        {
            window.Show();
            return;
        }

        _ = await window.ShowDialog<bool>(owner);
    }
}
