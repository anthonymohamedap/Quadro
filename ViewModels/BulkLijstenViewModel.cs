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

public enum BulkLijstenActionMode
{
    BulkPrijsUpdate,
    HerberekenSelectie
}

public partial class BulkLijstenViewModel : ObservableObject, IAsyncInitializable
{
    private const string AllOption = "Alle";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IToastService _toast;
    private List<TypeLijst> _allLijsten = new();

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string zoekterm = string.Empty;
    [ObservableProperty] private string soortFilter = AllOption;
    [ObservableProperty] private string serieFilter = AllOption;
    [ObservableProperty] private BulkLijstenActionMode selectedAction;

    // Price update inputs
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuwePrijsPerMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? prijsWijzigingPct;

    [ObservableProperty] private bool gebruikPercentage;

    // Computed visibility helpers for the UI
    public bool IsPrijsUpdateMode => SelectedAction == BulkLijstenActionMode.BulkPrijsUpdate;
    public bool IsAbsolutePrijs => !GebruikPercentage;

    [ObservableProperty] private ObservableCollection<TypeLijst> filteredLijsten = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> selectedLijsten = new();
    [ObservableProperty] private ObservableCollection<string> soortOptions = new();
    [ObservableProperty] private ObservableCollection<string> serieOptions = new();

    public Array ActionModes => Enum.GetValues(typeof(BulkLijstenActionMode));

    public int SelectedCount => SelectedLijsten.Count;

    public BulkLijstenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IToastService toast,
        BulkLijstenActionMode initialAction)
    {
        _dbFactory = dbFactory;
        _toast = toast;
        SelectedAction = initialAction;
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilters();
    partial void OnSoortFilterChanged(string value) => ApplyFilters();
    partial void OnSerieFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedActionChanged(BulkLijstenActionMode value)
    {
        OnPropertyChanged(nameof(IsPrijsUpdateMode));
        ExecuteActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnGebruikPercentageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAbsolutePrijs));
        ExecuteActionCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();
            _allLijsten = await db.TypeLijsten
                .Include(x => x.Leverancier)
                .AsNoTracking()
                .OrderBy(x => x.Artikelnummer)
                .ToListAsync();

            SoortOptions = new ObservableCollection<string>(
                new[] { AllOption }
                    .Concat(_allLijsten
                        .Select(x => x.Soort)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)));

            SerieOptions = new ObservableCollection<string>(
                new[] { AllOption }
                    .Concat(_allLijsten
                        .Select(x => x.Serie)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)));

            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<TypeLijst> query = _allLijsten;

        if (!string.IsNullOrWhiteSpace(Zoekterm))
        {
            var term = Zoekterm.Trim();
            query = query.Where(x =>
                x.Artikelnummer.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (x.Leverancier?.Naam?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.Equals(SoortFilter, AllOption, StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => string.Equals(x.Soort, SoortFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SerieFilter, AllOption, StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => string.Equals(x.Serie, SerieFilter, StringComparison.OrdinalIgnoreCase));

        FilteredLijsten = new ObservableCollection<TypeLijst>(query);
    }

    public void UpdateSelectedLijsten(IEnumerable<TypeLijst> selectedItems)
    {
        SelectedLijsten.Clear();
        foreach (var item in selectedItems.Distinct())
            SelectedLijsten.Add(item);

        OnPropertyChanged(nameof(SelectedCount));
        ExecuteActionCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteBulkAction()
    {
        if (SelectedLijsten.Count == 0) return false;

        if (SelectedAction == BulkLijstenActionMode.BulkPrijsUpdate)
        {
            return GebruikPercentage
                ? PrijsWijzigingPct.HasValue
                : NieuwePrijsPerMeter.HasValue && NieuwePrijsPerMeter.Value >= 0;
        }

        // HerberekenSelectie only needs items selected
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteBulkAction))]
    private async Task ExecuteActionAsync()
    {
        if (SelectedLijsten.Count == 0) return;

        try
        {
            IsBusy = true;

            if (SelectedAction == BulkLijstenActionMode.BulkPrijsUpdate)
                await VoerPrijsUpdateUitAsync();
            else
                await VoerHerberekenUitAsync();

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            _toast.Error($"Fout bij uitvoeren: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task VoerPrijsUpdateUitAsync()
    {
        var ids = SelectedLijsten.Select(x => x.Id).ToHashSet();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lijsten = await db.TypeLijsten
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();

        foreach (var lijst in lijsten)
        {
            if (GebruikPercentage && PrijsWijzigingPct.HasValue)
            {
                var factor = 1m + (PrijsWijzigingPct.Value / 100m);
                lijst.PrijsPerMeter = Math.Round(lijst.PrijsPerMeter * factor, 2);
            }
            else if (!GebruikPercentage && NieuwePrijsPerMeter.HasValue)
            {
                lijst.PrijsPerMeter = NieuwePrijsPerMeter.Value;
            }

            lijst.LaatsteUpdate = DateTime.Now;
        }

        await db.SaveChangesAsync();
        _toast.Success($"Prijs per meter bijgewerkt voor {lijsten.Count} lijst(en).");
    }

    private async Task VoerHerberekenUitAsync()
    {
        var ids = SelectedLijsten.Select(x => x.Id).ToHashSet();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lijsten = await db.TypeLijsten
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();

        foreach (var lijst in lijsten)
        {
            lijst.IsStaaflijst = string.Equals(lijst.Soort, "HOU", StringComparison.OrdinalIgnoreCase);
            lijst.LaatsteUpdate = DateTime.Now;
        }

        await db.SaveChangesAsync();
        _toast.Success($"IsStaaflijst vlag herberekend voor {lijsten.Count} lijst(en).");
    }

    [RelayCommand]
    private void Sluiten() => RequestClose?.Invoke(false);
}
