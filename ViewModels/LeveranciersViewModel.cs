using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
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

    [ObservableProperty] private ObservableCollection<Leverancier> leveranciers = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> lijstenVanLeverancier = new();
    [ObservableProperty] private Leverancier? selectedLeverancier;
    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool isBusy;

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

    private async Task LoadTypeLijstenForSelectedAsync(Leverancier? leverancier)
    {
        if (leverancier is null)
        {
            LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
            IsDetailOpen = false;
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var gekoppeldeLijsten = await db.TypeLijsten
            .AsNoTracking()
            .Where(x => x.LeverancierId == leverancier.Id)
            .OrderBy(x => x.Artikelnummer)
            .ToListAsync();

        LijstenVanLeverancier = new ObservableCollection<TypeLijst>(gekoppeldeLijsten);
        IsDetailOpen = true;
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

            var items = await query.ToListAsync();
            Leveranciers = new ObservableCollection<Leverancier>(items);

            if (SelectedLeverancier is not null)
            {
                SelectedLeverancier = Leveranciers.FirstOrDefault(x => x.Id == SelectedLeverancier.Id);
            }

            if (SelectedLeverancier is null)
            {
                IsDetailOpen = false;
                LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
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
        LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
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
            SelectedLeverancier = Leveranciers.FirstOrDefault(x => x.Id == selectedId);
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
            LijstenVanLeverancier = new ObservableCollection<TypeLijst>();

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
        LijstenVanLeverancier = new ObservableCollection<TypeLijst>();
    }

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        await _nav.NavigateToAsync<HomeViewModel>();
    }
}
