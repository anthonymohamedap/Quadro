using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class LeveranciersViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;

    private List<Leverancier> _allLeveranciers = new();
    private List<TypeLijst> _allLijstenVanLeverancier = new();

    [ObservableProperty] private ObservableCollection<Leverancier> leveranciers = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> lijstenVanLeverancier = new();
    [ObservableProperty] private Leverancier? selectedLeverancier;
    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private int leveranciersCurrentPage = 1;
    [ObservableProperty] private int leveranciersPageSize = 10;
    [ObservableProperty] private int lijstenCurrentPage = 1;
    [ObservableProperty] private int lijstenPageSize = 8;

    public int LeveranciersTotalPages => Math.Max(1, (int)Math.Ceiling((_allLeveranciers.Count) / (double)LeveranciersPageSize));
    public int LijstenTotalPages => Math.Max(1, (int)Math.Ceiling((_allLijstenVanLeverancier.Count) / (double)LijstenPageSize));

    public bool CanPrevLeveranciersPage => LeveranciersCurrentPage > 1;
    public bool CanNextLeveranciersPage => LeveranciersCurrentPage < LeveranciersTotalPages;
    public bool CanPrevLijstenPage => LijstenCurrentPage > 1;
    public bool CanNextLijstenPage => LijstenCurrentPage < LijstenTotalPages;
    public string LeveranciersPageLabel => $"Pagina {LeveranciersCurrentPage} van {LeveranciersTotalPages}";
    public string LijstenPageLabel => $"Pagina {LijstenCurrentPage} van {LijstenTotalPages}";

    public LeveranciersViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _nav = nav;
        _dialogs = dialogs;
        _toast = toast;
    }

    public async Task InitializeAsync() => await LoadLeveranciersAsync();

    partial void OnZoektermChanged(string? value)
    {
        _ = LoadLeveranciersAsync();
    }

    partial void OnSelectedLeverancierChanged(Leverancier? value)
    {
        _ = LoadTypeLijstenForSelectedAsync(value);
    }

    partial void OnLeveranciersCurrentPageChanged(int value) => UpdateLeveranciersPage();
    partial void OnLeveranciersPageSizeChanged(int value)
    {
        if (value <= 0)
        {
            LeveranciersPageSize = 10;
            return;
        }

        LeveranciersCurrentPage = 1;
        UpdateLeveranciersPage();
    }

    partial void OnLijstenCurrentPageChanged(int value) => UpdateLijstenPage();
    partial void OnLijstenPageSizeChanged(int value)
    {
        if (value <= 0)
        {
            LijstenPageSize = 8;
            return;
        }

        LijstenCurrentPage = 1;
        UpdateLijstenPage();
    }

    private async Task LoadTypeLijstenForSelectedAsync(Leverancier? leverancier)
    {
        if (leverancier is null || leverancier.Id == 0)
        {
            _allLijstenVanLeverancier = new List<TypeLijst>();
            LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
            LijstenCurrentPage = 1;
            IsDetailOpen = leverancier is not null;
            NotifyLijstenPagingChanged();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        _allLijstenVanLeverancier = await db.TypeLijsten
            .AsNoTracking()
            .Where(x => x.LeverancierId == leverancier.Id)
            .OrderBy(x => x.Artikelnummer)
            .ToListAsync();

        LijstenCurrentPage = 1;
        UpdateLijstenPage();
        IsDetailOpen = true;
    }

    private void UpdateLeveranciersPage()
    {
        if (LeveranciersCurrentPage < 1)
            LeveranciersCurrentPage = 1;

        if (LeveranciersCurrentPage > LeveranciersTotalPages)
            LeveranciersCurrentPage = LeveranciersTotalPages;

        var skip = (LeveranciersCurrentPage - 1) * LeveranciersPageSize;
        var page = _allLeveranciers.Skip(skip).Take(LeveranciersPageSize).ToList();
        Leveranciers = new ObservableCollection<Leverancier>(page);

        NotifyLeveranciersPagingChanged();
    }

    private void UpdateLijstenPage()
    {
        if (LijstenCurrentPage < 1)
            LijstenCurrentPage = 1;

        if (LijstenCurrentPage > LijstenTotalPages)
            LijstenCurrentPage = LijstenTotalPages;

        var skip = (LijstenCurrentPage - 1) * LijstenPageSize;
        var page = _allLijstenVanLeverancier.Skip(skip).Take(LijstenPageSize).ToList();
        LijstenVanLeverancier = new ObservableCollection<TypeLijst>(page);

        NotifyLijstenPagingChanged();
    }

    private void NotifyLeveranciersPagingChanged()
    {
        OnPropertyChanged(nameof(LeveranciersTotalPages));
        OnPropertyChanged(nameof(CanPrevLeveranciersPage));
        OnPropertyChanged(nameof(CanNextLeveranciersPage));
        OnPropertyChanged(nameof(LeveranciersPageLabel));
        PrevLeveranciersPageCommand.NotifyCanExecuteChanged();
        NextLeveranciersPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyLijstenPagingChanged()
    {
        OnPropertyChanged(nameof(LijstenTotalPages));
        OnPropertyChanged(nameof(CanPrevLijstenPage));
        OnPropertyChanged(nameof(CanNextLijstenPage));
        OnPropertyChanged(nameof(LijstenPageLabel));
        PrevLijstenPageCommand.NotifyCanExecuteChanged();
        NextLijstenPageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadLeveranciersAsync()
    {
        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = db.Leveranciers
                .AsNoTracking()
                .OrderBy(x => x.Naam)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(Zoekterm))
            {
                var term = Zoekterm.Trim();
                query = query.Where(x => x.Naam.Contains(term));
            }

            _allLeveranciers = await query.ToListAsync();
            LeveranciersCurrentPage = 1;
            UpdateLeveranciersPage();

            if (SelectedLeverancier is not null)
            {
                var kept = _allLeveranciers.FirstOrDefault(x => x.Id == SelectedLeverancier.Id);
                SelectedLeverancier = kept;
            }

            if (SelectedLeverancier is null)
            {
                IsDetailOpen = false;
                _allLijstenVanLeverancier = new List<TypeLijst>();
                LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
                LijstenCurrentPage = 1;
                NotifyLijstenPagingChanged();
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Leveranciers laden", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Nieuw()
    {
        SelectedLeverancier = new Leverancier();
        _allLijstenVanLeverancier = new List<TypeLijst>();
        LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
        LijstenCurrentPage = 1;
        NotifyLijstenPagingChanged();
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        if (SelectedLeverancier is null)
            return;

        var naam = (SelectedLeverancier.Naam ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(naam))
        {
            _toast.Error("Naam is verplicht.");
            return;
        }

        if (naam.Length != 3)
        {
            _toast.Error("Naam moet exact 3 tekens bevatten.");
            return;
        }

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var duplicateExists = await db.Leveranciers
                .AsNoTracking()
                .AnyAsync(x => x.Naam == naam && x.Id != SelectedLeverancier.Id);

            if (duplicateExists)
            {
                _toast.Error("Leverancier met deze code bestaat al.");
                return;
            }

            SelectedLeverancier.Naam = naam;

            if (SelectedLeverancier.Id == 0)
            {
                db.Leveranciers.Add(SelectedLeverancier);
            }
            else
            {
                db.Leveranciers.Attach(SelectedLeverancier);
                db.Entry(SelectedLeverancier).Property(x => x.Naam).IsModified = true;
            }

            await db.SaveChangesAsync();
            _toast.Success("Leverancier opgeslagen.");

            var selectedId = SelectedLeverancier.Id;
            await LoadLeveranciersAsync();
            SelectedLeverancier = _allLeveranciers.FirstOrDefault(x => x.Id == selectedId);
        }
        catch (DbUpdateException ex)
        {
            _toast.Error($"Opslaan mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VerwijderAsync()
    {
        if (SelectedLeverancier is null || SelectedLeverancier.Id == 0)
            return;

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var heeftTypeLijsten = await db.TypeLijsten
                .AsNoTracking()
                .AnyAsync(x => x.LeverancierId == SelectedLeverancier.Id);

            if (heeftTypeLijsten)
            {
                _toast.Warning("Deze leverancier kan niet verwijderd worden omdat er TypeLijsten aan gekoppeld zijn.");
                return;
            }

            var ok = await _dialogs.ConfirmAsync(
                "Leverancier verwijderen",
                $"Ben je zeker dat je leverancier '{SelectedLeverancier.Naam}' wil verwijderen?");

            if (!ok)
                return;

            db.Leveranciers.Remove(new Leverancier { Id = SelectedLeverancier.Id });
            await db.SaveChangesAsync();

            _toast.Success("Leverancier verwijderd.");

            SelectedLeverancier = null;
            IsDetailOpen = false;
            _allLijstenVanLeverancier = new List<TypeLijst>();
            LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
            LijstenCurrentPage = 1;
            NotifyLijstenPagingChanged();

            await LoadLeveranciersAsync();
        }
        catch (Exception ex)
        {
            _toast.Error($"Verwijderen mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Sluiten()
    {
        IsDetailOpen = false;
        SelectedLeverancier = null;
        _allLijstenVanLeverancier = new List<TypeLijst>();
        LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
        LijstenCurrentPage = 1;
        NotifyLijstenPagingChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrevLeveranciersPage))]
    private void PrevLeveranciersPage() => LeveranciersCurrentPage--;

    [RelayCommand(CanExecute = nameof(CanNextLeveranciersPage))]
    private void NextLeveranciersPage() => LeveranciersCurrentPage++;

    [RelayCommand(CanExecute = nameof(CanPrevLijstenPage))]
    private void PrevLijstenPage() => LijstenCurrentPage--;

    [RelayCommand(CanExecute = nameof(CanNextLijstenPage))]
    private void NextLijstenPage() => LijstenCurrentPage++;

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        await _nav.NavigateToAsync<HomeViewModel>();
    }
}
