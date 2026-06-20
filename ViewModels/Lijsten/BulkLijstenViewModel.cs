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

    // ─── Filter / status ───────────────────────────────────────────────────────
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string zoekterm = string.Empty;
    [ObservableProperty] private string soortFilter = AllOption;

    // ─── Prijs-veld (speciaal: twee modes, blijft als losse eigenschappen) ────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool bijwerkPrijsPerMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private bool gebruikPercentage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? nieuwePrijsPerMeter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteActionCommand))]
    private decimal? prijsWijzigingPct;

    public bool IsAbsolutePrijs => !GebruikPercentage;

    // ─── Bulk-velden (BulkVeld<T> abstractie) ────────────────────────────────
    public BulkVeld<string>     ArtikelnummerVeld   { get; } = new("artikelnummer",   nameof(TypeLijst.Artikelnummer),  string.Empty);
    public BulkVeld<string>     LevcodeVeld         { get; } = new("levcode",         nameof(TypeLijst.Levcode),        string.Empty);
    public BulkVeld<Leverancier?> LeverancierVeld   { get; } = new("leverancier",     nameof(TypeLijst.LeverancierId));
    public BulkVeld<decimal?>   BreedteCmVeld       { get; } = new("breedte",         nameof(TypeLijst.BreedteCm),      null, v => v.HasValue && v.Value == decimal.Truncate(v.Value));
    public BulkVeld<string>     SoortVeld           { get; } = new("soort",           string.Empty,                     string.Empty);
    public BulkVeld<bool>       IsDealerVeld        { get; } = new("dealerstatus",    string.Empty,                     false,  _ => true);
    public BulkVeld<string>     OpmerkingVeld       { get; } = new("opmerking",       string.Empty,                     string.Empty, _ => true);
    public BulkVeld<decimal?>   WinstFactorVeld     { get; } = new("winstfactor",     string.Empty);
    public BulkVeld<decimal?>   AfvalPercentageVeld { get; } = new("afvalpercentage", string.Empty);
    public BulkVeld<decimal?>   VasteKostVeld       { get; } = new("vaste kost",      nameof(TypeLijst.VasteKost));
    public BulkVeld<decimal?>   WerkMinutenVeld     { get; } = new("werkminuten",     nameof(TypeLijst.WerkMinuten),    null, v => v.HasValue && v.Value == decimal.Truncate(v.Value));
    public BulkVeld<decimal?>   VoorraadMeterVeld   { get; } = new("voorraad",        nameof(TypeLijst.VoorraadMeter));
    public BulkVeld<decimal?>   InventarisKostVeld  { get; } = new("inventariskost",  string.Empty);
    public BulkVeld<decimal?>   MinimumVoorraadVeld { get; } = new("minimumvoorraad", nameof(TypeLijst.MinimumVoorraad));

    // ─── Collecties ───────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TypeLijst>   filteredLijsten   = new();
    [ObservableProperty] private ObservableCollection<TypeLijst>   selectedLijsten   = new();
    [ObservableProperty] private ObservableCollection<string>      soortOptions      = new();
    [ObservableProperty] private ObservableCollection<Leverancier> bulkLeveranciers  = new();

    public int SelectedCount => SelectedLijsten.Count;

    // ─── Alle bulk-velden als IBulkVeld-array (voor lussen) ──────────────────
    private IBulkVeld[]? _alleVelden;
    private IBulkVeld[] AlleVelden => _alleVelden ??= new IBulkVeld[]
    {
        ArtikelnummerVeld, LevcodeVeld, LeverancierVeld, BreedteCmVeld, SoortVeld,
        IsDealerVeld, OpmerkingVeld, WinstFactorVeld, AfvalPercentageVeld,
        VasteKostVeld, WerkMinutenVeld, VoorraadMeterVeld, InventarisKostVeld, MinimumVoorraadVeld
    };

    public BulkLijstenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        ICrudValidator<TypeLijst> validator,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _validator = validator;
        _toast = toast;

        // Koppel Changed-event van elk veld aan ExecuteActionCommand.NotifyCanExecuteChanged
        void OnVeldChanged() => ExecuteActionCommand.NotifyCanExecuteChanged();
        foreach (var veld in AlleVelden)
            veld.Changed += OnVeldChanged;
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
        if (SelectedLijsten.Count == 0) return false;

        if (BijwerkPrijsPerMeter)
        {
            if (GebruikPercentage && !PrijsWijzigingPct.HasValue) return false;
            if (!GebruikPercentage && !NieuwePrijsPerMeter.HasValue) return false;
        }

        return (AlleVelden.Any(v => v.Bijwerken) || BijwerkPrijsPerMeter)
            && AlleVelden.All(v => v.IsGeldig);
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
            _toast.Error($"Bulkbijwerking geblokkeerd: dubbele artikelnummers in selectie: {string.Join(", ", batchDuplicaten)}.");
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
            _toast.Error("Bulkbijwerking geblokkeerd:" + Environment.NewLine + string.Join(Environment.NewLine, foutmeldingen.Take(5)));
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
            _toast.Warning($"{lageVoorraad} lijst(en) zitten onder minimumvoorraad na de bulkbijwerking.");
        }

        _toast.Success($"{string.Join(", ", SelectedFieldLabels())} bijgewerkt voor {lijsten.Count} lijst(en).");
        return true;
    }

    private void PasBulkVeldenToe(TypeLijst lijst)
    {
        if (ArtikelnummerVeld.Bijwerken)
            lijst.Artikelnummer = ArtikelnummerVeld.Waarde.Trim();

        if (LevcodeVeld.Bijwerken)
            lijst.Levcode = LevcodeVeld.Waarde.Trim();

        if (LeverancierVeld.Bijwerken && LeverancierVeld.Waarde is not null)
        {
            lijst.LeverancierId = LeverancierVeld.Waarde.Id;
            lijst.Leverancier = LeverancierVeld.Waarde;
        }

        if (BreedteCmVeld.Bijwerken && BreedteCmVeld.Waarde.HasValue)
            lijst.BreedteCm = Decimal.ToInt32(BreedteCmVeld.Waarde.Value);

        if (SoortVeld.Bijwerken)
            lijst.Soort = SoortVeld.Waarde.Trim();

        if (IsDealerVeld.Bijwerken)
            lijst.IsDealer = IsDealerVeld.Waarde;

        if (OpmerkingVeld.Bijwerken)
            lijst.Opmerking = OpmerkingVeld.Waarde.Trim();

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

        if (WinstFactorVeld.Bijwerken)
            lijst.WinstFactor = WinstFactorVeld.Leegmaken ? null : WinstFactorVeld.Waarde;

        if (AfvalPercentageVeld.Bijwerken)
            lijst.AfvalPercentage = AfvalPercentageVeld.Leegmaken ? null : AfvalPercentageVeld.Waarde;

        if (VasteKostVeld.Bijwerken && VasteKostVeld.Waarde.HasValue)
            lijst.VasteKost = Decimal.Round(VasteKostVeld.Waarde.Value, 2, MidpointRounding.AwayFromZero);

        if (WerkMinutenVeld.Bijwerken && WerkMinutenVeld.Waarde.HasValue)
            lijst.WerkMinuten = Decimal.ToInt32(WerkMinutenVeld.Waarde.Value);

        if (VoorraadMeterVeld.Bijwerken && VoorraadMeterVeld.Waarde.HasValue)
            lijst.VoorraadMeter = Decimal.Round(VoorraadMeterVeld.Waarde.Value, 2, MidpointRounding.AwayFromZero);

        if (InventarisKostVeld.Bijwerken && InventarisKostVeld.Waarde.HasValue)
            lijst.InventarisKost = Decimal.Round(InventarisKostVeld.Waarde.Value, 2, MidpointRounding.AwayFromZero);

        if (MinimumVoorraadVeld.Bijwerken && MinimumVoorraadVeld.Waarde.HasValue)
            lijst.MinimumVoorraad = Decimal.Round(MinimumVoorraadVeld.Waarde.Value, 2, MidpointRounding.AwayFromZero);
    }

    private IEnumerable<string> SelectedFieldLabels()
    {
        foreach (var veld in AlleVelden.Where(v => v.Bijwerken))
            yield return veld.Label;
        if (BijwerkPrijsPerMeter) yield return "prijs per meter";
    }

    private HashSet<string> GetBulkValidationFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var veld in AlleVelden)
        {
            if (veld.Bijwerken && !string.IsNullOrEmpty(veld.ValidationFieldName))
                fields.Add(veld.ValidationFieldName);
        }
        if (BijwerkPrijsPerMeter) fields.Add(nameof(TypeLijst.PrijsPerMeter));
        return fields;
    }

    [RelayCommand]
    private void ResetBulkVelden()
    {
        foreach (var veld in AlleVelden)
            veld.Reset();
        BijwerkPrijsPerMeter = false;
        GebruikPercentage = false;
        NieuwePrijsPerMeter = null;
        PrijsWijzigingPct = null;
    }

    [RelayCommand]
    private void Sluiten() => RequestClose?.Invoke(false);
}
