using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class BulkLijstenViewModel : ObservableObject, IAsyncInitializable
{
    private const string AllOption = "Alle";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICrudValidator<TypeLijst> _validator;
    private readonly IToastService _toast;
    private List<TypeLijst> _allLijsten = new();

    public Action<bool>? RequestClose { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string zoekterm = string.Empty;
    [ObservableProperty] private string soortFilter = AllOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool gebruikPercentage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkArtikelnummer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private string nieuwArtikelnummer = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkLevcode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private string nieuweLevcode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkLeverancier;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private Leverancier? geselecteerdeBulkLeverancier;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkBreedteCm;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweBreedteCm;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkSoort;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private string nieuweSoort = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkIsDealer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool nieuweIsDealer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkOpmerking;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private string nieuweOpmerking = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkPrijsPerMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuwePrijsPerMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? prijsWijzigingPct;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkWinstFactor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweWinstFactor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool winstFactorLeegmaken;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkAfvalPercentage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuwAfvalPercentage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool afvalPercentageLeegmaken;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkVasteKost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweVasteKost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkWerkMinuten;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweWerkMinuten;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkVoorraadMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweVoorraadMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkInventarisKost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweInventarisKost;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkMinimumVoorraad;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuweMinimumVoorraad;

    public bool IsAbsolutePrijs => !GebruikPercentage;

    [ObservableProperty] private ObservableCollection<TypeLijst> filteredLijsten = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> selectedLijsten = new();
    [ObservableProperty] private ObservableCollection<string> soortOptions = new();
    [ObservableProperty] private ObservableCollection<Leverancier> bulkLeveranciers = new();

    public int SelectedCount => SelectedLijsten.Count;

    public BulkLijstenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        ICrudValidator<TypeLijst> validator,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _validator = validator;
        _toast = toast;
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilters();
    partial void OnSoortFilterChanged(string value) => ApplyFilters();

    partial void OnGebruikPercentageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAbsolutePrijs));
        ExecuteActionCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();
            _allLijsten = await db.TypeLijsten
                .Include(x => x.Leverancier)
                .AsNoTracking()
                .OrderBy(x => x.Artikelnummer)
                .ToListAsync();

            BulkLeveranciers = new ObservableCollection<Leverancier>(
                await db.Leveranciers
                    .AsNoTracking()
                    .OrderBy(x => x.Naam)
                    .ToListAsync());

            SoortOptions = new ObservableCollection<string>(
                new[] { AllOption }
                    .Concat(_allLijsten
                        .Select(x => x.Soort)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)));

            ApplyFilters();
            UpdateSelectedLijsten(Array.Empty<TypeLijst>());
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
        {
            query = query.Where(x => string.Equals(x.Soort, SoortFilter, StringComparison.OrdinalIgnoreCase));
        }

        FilteredLijsten = new ObservableCollection<TypeLijst>(query);
    }

    public void UpdateSelectedLijsten(IEnumerable<TypeLijst> selectedItems)
    {
        SelectedLijsten.Clear();
        foreach (var item in selectedItems.DistinctBy(x => x.Id))
        {
            SelectedLijsten.Add(item);
        }

        OnPropertyChanged(nameof(SelectedCount));
        ExecuteActionCommand.NotifyCanExecuteChanged();
    }

    private bool CanExecuteBulkAction()
    {
        if (SelectedLijsten.Count == 0)
        {
            return false;
        }

        var anyFieldSelected = false;

        if (BijwerkArtikelnummer)
        {
            anyFieldSelected = true;
            if (string.IsNullOrWhiteSpace(NieuwArtikelnummer))
            {
                return false;
            }
        }

        if (BijwerkLevcode)
        {
            anyFieldSelected = true;
            if (string.IsNullOrWhiteSpace(NieuweLevcode))
            {
                return false;
            }
        }

        if (BijwerkLeverancier)
        {
            anyFieldSelected = true;
            if (GeselecteerdeBulkLeverancier is null)
            {
                return false;
            }
        }

        if (BijwerkBreedteCm)
        {
            anyFieldSelected = true;
            if (!HasWholeNumber(NieuweBreedteCm))
            {
                return false;
            }
        }

        if (BijwerkSoort)
        {
            anyFieldSelected = true;
            if (string.IsNullOrWhiteSpace(NieuweSoort))
            {
                return false;
            }
        }

        if (BijwerkIsDealer)
        {
            anyFieldSelected = true;
        }

        if (BijwerkOpmerking)
        {
            anyFieldSelected = true;
        }

        if (BijwerkPrijsPerMeter)
        {
            anyFieldSelected = true;
            if (GebruikPercentage && !PrijsWijzigingPct.HasValue)
            {
                return false;
            }

            if (!GebruikPercentage && !NieuwePrijsPerMeter.HasValue)
            {
                return false;
            }
        }

        if (BijwerkWinstFactor)
        {
            anyFieldSelected = true;
            if (!WinstFactorLeegmaken && !NieuweWinstFactor.HasValue)
            {
                return false;
            }
        }

        if (BijwerkAfvalPercentage)
        {
            anyFieldSelected = true;
            if (!AfvalPercentageLeegmaken && !NieuwAfvalPercentage.HasValue)
            {
                return false;
            }
        }

        if (BijwerkVasteKost)
        {
            anyFieldSelected = true;
            if (!NieuweVasteKost.HasValue)
            {
                return false;
            }
        }

        if (BijwerkWerkMinuten)
        {
            anyFieldSelected = true;
            if (!HasWholeNumber(NieuweWerkMinuten))
            {
                return false;
            }
        }

        if (BijwerkVoorraadMeter)
        {
            anyFieldSelected = true;
            if (!NieuweVoorraadMeter.HasValue)
            {
                return false;
            }
        }

        if (BijwerkInventarisKost)
        {
            anyFieldSelected = true;
            if (!NieuweInventarisKost.HasValue)
            {
                return false;
            }
        }

        if (BijwerkMinimumVoorraad)
        {
            anyFieldSelected = true;
            if (!NieuweMinimumVoorraad.HasValue)
            {
                return false;
            }
        }

        return anyFieldSelected;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteBulkAction))]
    private async Task ExecuteActionAsync()
    {
        if (SelectedLijsten.Count == 0)
        {
            return;
        }

        try
        {
            IsBusy = true;

            var applied = await VoerBulkUpdateUitAsync();
            if (!applied)
            {
                return;
            }

            if (RefreshRequested is not null)
            {
                await RefreshRequested();
            }
            else
            {
                await LoadAsync();
            }

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

    private async Task<bool> VoerBulkUpdateUitAsync()
    {
        var ids = SelectedLijsten.Select(x => x.Id).ToHashSet();
        var relevanteVelden = GetBulkValidationFields();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lijsten = await db.TypeLijsten
            .Include(x => x.Leverancier)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();

        foreach (var lijst in lijsten)
        {
            PasBulkVeldenToe(lijst);
            lijst.LaatsteUpdate = DateTime.Now;
        }

        var batchDuplicaten = lijsten
            .GroupBy(x => x.Artikelnummer.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (batchDuplicaten.Count > 0)
        {
            _toast.Error($"Bulk update geblokkeerd: dubbele artikelnummer(s) in selectie: {string.Join(", ", batchDuplicaten)}.");
            return false;
        }

        var foutmeldingen = new List<string>();
        var waarschuwingen = new List<string>();

        foreach (var lijst in lijsten)
        {
            var vr = await _validator.ValidateUpdateAsync(lijst);
            var relevanteItems = vr.Items
                .Where(x => relevanteVelden.Contains(x.Field))
                .ToList();

            var heeftRelevanteFouten = relevanteItems.Any(x => x.Severity == ValidationSeverity.Error);
            if (!vr.IsValid)
            {
                var errors = relevanteItems
                    .Where(x => x.Severity == ValidationSeverity.Error)
                    .Select(x => $"{x.Field}: {x.Message}");

                if (heeftRelevanteFouten)
                {
                    foutmeldingen.Add($"{lijst.Artikelnummer}: {string.Join("; ", errors)}");
                }
            }

            var warns = relevanteItems
                .Where(x => x.Severity == ValidationSeverity.Warning)
                .Select(x => $"{lijst.Artikelnummer} - {x.Field}: {x.Message}");

            waarschuwingen.AddRange(warns);
        }

        if (foutmeldingen.Count > 0)
        {
            _toast.Error("Bulk update geblokkeerd:" + Environment.NewLine + string.Join(Environment.NewLine, foutmeldingen.Take(5)));
            return false;
        }

        await db.SaveChangesAsync();

        var waarschuwingLijst = waarschuwingen.Distinct().Take(3).ToList();
        if (waarschuwingLijst.Count > 0)
        {
            _toast.Warning(string.Join(Environment.NewLine, waarschuwingLijst));
        }

        var lageVoorraad = lijsten.Count(x => x.VoorraadMeter < x.MinimumVoorraad);
        if (lageVoorraad > 0)
        {
            _toast.Warning($"{lageVoorraad} lijst(en) zitten onder minimumvoorraad na de bulk update.");
        }

        _toast.Success($"{string.Join(", ", SelectedFieldLabels())} bijgewerkt voor {lijsten.Count} lijst(en).");
        return true;
    }

    private void PasBulkVeldenToe(TypeLijst lijst)
    {
        if (BijwerkArtikelnummer)
        {
            lijst.Artikelnummer = NieuwArtikelnummer.Trim();
        }

        if (BijwerkLevcode)
        {
            lijst.Levcode = NieuweLevcode.Trim();
        }

        if (BijwerkLeverancier && GeselecteerdeBulkLeverancier is not null)
        {
            lijst.LeverancierId = GeselecteerdeBulkLeverancier.Id;
            lijst.Leverancier = GeselecteerdeBulkLeverancier;
        }

        if (BijwerkBreedteCm && NieuweBreedteCm.HasValue)
        {
            lijst.BreedteCm = Decimal.ToInt32(NieuweBreedteCm.Value);
        }

        if (BijwerkSoort)
        {
            lijst.Soort = NieuweSoort.Trim();
        }

        if (BijwerkIsDealer)
        {
            lijst.IsDealer = NieuweIsDealer;
        }

        if (BijwerkOpmerking)
        {
            lijst.Opmerking = NieuweOpmerking.Trim();
        }

        if (BijwerkPrijsPerMeter)
        {
            if (GebruikPercentage && PrijsWijzigingPct.HasValue)
            {
                var factor = 1m + (PrijsWijzigingPct.Value / 100m);
                lijst.PrijsPerMeter = Math.Round(lijst.PrijsPerMeter * factor, 2, MidpointRounding.AwayFromZero);
            }
            else if (!GebruikPercentage && NieuwePrijsPerMeter.HasValue)
            {
                lijst.PrijsPerMeter = Decimal.Round(NieuwePrijsPerMeter.Value, 2, MidpointRounding.AwayFromZero);
            }
        }

        if (BijwerkWinstFactor)
        {
            lijst.WinstFactor = WinstFactorLeegmaken
                ? null
                : NieuweWinstFactor;
        }

        if (BijwerkAfvalPercentage)
        {
            lijst.AfvalPercentage = AfvalPercentageLeegmaken
                ? null
                : NieuwAfvalPercentage;
        }

        if (BijwerkVasteKost && NieuweVasteKost.HasValue)
        {
            lijst.VasteKost = Decimal.Round(NieuweVasteKost.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (BijwerkWerkMinuten && NieuweWerkMinuten.HasValue)
        {
            lijst.WerkMinuten = Decimal.ToInt32(NieuweWerkMinuten.Value);
        }

        if (BijwerkVoorraadMeter && NieuweVoorraadMeter.HasValue)
        {
            lijst.VoorraadMeter = Decimal.Round(NieuweVoorraadMeter.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (BijwerkInventarisKost && NieuweInventarisKost.HasValue)
        {
            lijst.InventarisKost = Decimal.Round(NieuweInventarisKost.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (BijwerkMinimumVoorraad && NieuweMinimumVoorraad.HasValue)
        {
            lijst.MinimumVoorraad = Decimal.Round(NieuweMinimumVoorraad.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private IEnumerable<string> SelectedFieldLabels()
    {
        if (BijwerkArtikelnummer)
        {
            yield return "artikelnummer";
        }

        if (BijwerkLevcode)
        {
            yield return "levcode";
        }

        if (BijwerkLeverancier)
        {
            yield return "leverancier";
        }

        if (BijwerkBreedteCm)
        {
            yield return "breedte";
        }

        if (BijwerkSoort)
        {
            yield return "soort";
        }

        if (BijwerkIsDealer)
        {
            yield return "dealerstatus";
        }

        if (BijwerkOpmerking)
        {
            yield return "opmerking";
        }

        if (BijwerkPrijsPerMeter)
        {
            yield return "prijs per meter";
        }

        if (BijwerkWinstFactor)
        {
            yield return "winstfactor";
        }

        if (BijwerkAfvalPercentage)
        {
            yield return "afvalpercentage";
        }

        if (BijwerkVasteKost)
        {
            yield return "vaste kost";
        }

        if (BijwerkWerkMinuten)
        {
            yield return "werkminuten";
        }

        if (BijwerkVoorraadMeter)
        {
            yield return "voorraad";
        }

        if (BijwerkInventarisKost)
        {
            yield return "inventariskost";
        }

        if (BijwerkMinimumVoorraad)
        {
            yield return "minimumvoorraad";
        }
    }

    private HashSet<string> GetBulkValidationFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);

        if (BijwerkArtikelnummer)
        {
            fields.Add(nameof(TypeLijst.Artikelnummer));
        }

        if (BijwerkLevcode)
        {
            fields.Add(nameof(TypeLijst.Levcode));
        }

        if (BijwerkLeverancier)
        {
            fields.Add(nameof(TypeLijst.LeverancierId));
        }

        if (BijwerkBreedteCm)
        {
            fields.Add(nameof(TypeLijst.BreedteCm));
        }

        if (BijwerkPrijsPerMeter)
        {
            fields.Add(nameof(TypeLijst.PrijsPerMeter));
        }

        if (BijwerkVasteKost)
        {
            fields.Add(nameof(TypeLijst.VasteKost));
        }

        if (BijwerkWerkMinuten)
        {
            fields.Add(nameof(TypeLijst.WerkMinuten));
        }

        if (BijwerkVoorraadMeter)
        {
            fields.Add(nameof(TypeLijst.VoorraadMeter));
        }

        if (BijwerkMinimumVoorraad)
        {
            fields.Add(nameof(TypeLijst.MinimumVoorraad));
        }

        return fields;
    }

    private static bool HasWholeNumber(decimal? value) =>
        value.HasValue && value.Value == decimal.Truncate(value.Value);

    [RelayCommand]
    private void ResetBulkVelden()
    {
        BijwerkArtikelnummer = false;
        NieuwArtikelnummer = string.Empty;
        BijwerkLevcode = false;
        NieuweLevcode = string.Empty;
        BijwerkLeverancier = false;
        GeselecteerdeBulkLeverancier = null;
        BijwerkBreedteCm = false;
        NieuweBreedteCm = null;
        BijwerkSoort = false;
        NieuweSoort = string.Empty;
        BijwerkIsDealer = false;
        NieuweIsDealer = false;
        BijwerkOpmerking = false;
        NieuweOpmerking = string.Empty;
        BijwerkPrijsPerMeter = false;
        GebruikPercentage = false;
        NieuwePrijsPerMeter = null;
        PrijsWijzigingPct = null;
        BijwerkWinstFactor = false;
        NieuweWinstFactor = null;
        WinstFactorLeegmaken = false;
        BijwerkAfvalPercentage = false;
        NieuwAfvalPercentage = null;
        AfvalPercentageLeegmaken = false;
        BijwerkVasteKost = false;
        NieuweVasteKost = null;
        BijwerkWerkMinuten = false;
        NieuweWerkMinuten = null;
        BijwerkVoorraadMeter = false;
        NieuweVoorraadMeter = null;
        BijwerkInventarisKost = false;
        NieuweInventarisKost = null;
        BijwerkMinimumVoorraad = false;
        NieuweMinimumVoorraad = null;
    }

    [RelayCommand]
    private void Sluiten() => RequestClose?.Invoke(false);
}
