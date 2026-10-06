using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>US-66 — winkelverkopen (kassa-verkopen): toonbankverkopen zonder offerte/bestelbon.
/// Schrijft naar het centrale ontvangstenregister (<see cref="Ontvangst"/> met
/// <see cref="OntvangstSoort.Winkelverkoop"/>), zodat ze mee in het "overzicht betalingen" komen.
/// Naar het voorbeeld van de kassa-verkopen van Quadro: datum, omschrijving, aantal, prijs incl.,
/// korting %, betaalwijze; btw altijd 21 %; wordt altijd meteen volledig betaald.
/// Hergebruikt <see cref="Permissie.Factureren"/>.</summary>
public partial class WinkelVerkopenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;
    private readonly IAuthService _auth;

    [ObservableProperty] private ObservableCollection<Ontvangst> verkopen = new();
    [ObservableProperty] private ObservableCollection<Ontvangst> gefilterdeVerkopen = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Ontvangst? geselecteerdeVerkoop;

    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private string zoekterm = string.Empty;
    [ObservableProperty] private DateTimeOffset? filterVanaf;
    [ObservableProperty] private DateTimeOffset? filterTot;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    /// <summary>US-32: uitboeken van winkelverkopen valt onder Factureren.</summary>
    public bool MagBewerken { get; }

    public IReadOnlyList<Betaalwijze> BetaalwijzeOpties { get; } = Enum.GetValues<Betaalwijze>();

    public int AantalVerkopen => GefilterdeVerkopen?.Count ?? 0;
    public decimal TotaalOmzetIncl => GefilterdeVerkopen?.Sum(v => v.BedragIncl) ?? 0m;

    public Action? OnTerug { get; set; }

    /// <summary>Proxy voor <see cref="GeselecteerdeVerkoop"/>.Datum t.b.v. Avalonia's
    /// <c>DatePicker</c> (bindt op <c>DateTimeOffset?</c>, het model gebruikt <c>DateTime</c>).</summary>
    public DateTimeOffset? VerkoopDatum
    {
        get => GeselecteerdeVerkoop is null ? null : new DateTimeOffset(DateTime.SpecifyKind(GeselecteerdeVerkoop.Datum, DateTimeKind.Unspecified));
        set
        {
            if (GeselecteerdeVerkoop is null || value is null) return;
            GeselecteerdeVerkoop.Datum = value.Value.Date;
            OnPropertyChanged();
        }
    }

    public WinkelVerkopenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        IToastService toast,
        IAuthService auth)
    {
        _dbFactory = dbFactory;
        _nav = nav;
        _dialogs = dialogs;
        _toast = toast;
        _auth = auth;

        MagBewerken = _auth.HeeftPermissie(Permissie.Factureren);

        // US-72: standaard de lopende maand, zoals de maand-tabbladen van de oude kassa.
        ZetMaand(DateTime.Today);
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilter();
    partial void OnFilterVanafChanged(DateTimeOffset? value) => ApplyFilter();
    partial void OnFilterTotChanged(DateTimeOffset? value) => ApplyFilter();

    partial void OnGeselecteerdeVerkoopChanged(Ontvangst? value)
    {
        IsDetailOpen = value is not null;
        OnPropertyChanged(nameof(VerkoopDatum));
        OnPropertyChanged(nameof(VerkoopAantal));
        OnPropertyChanged(nameof(VerkoopPrijs));
        OnPropertyChanged(nameof(VerkoopKortingPct));
        OnPropertyChanged(nameof(VerkoopTotaal));
    }

    // ── Proxies zodat het totaal live meebeweegt (Ontvangst is een POCO zonder INotifyPropertyChanged) ──
    public decimal? VerkoopAantal
    {
        get => GeselecteerdeVerkoop?.Aantal;
        set { if (GeselecteerdeVerkoop is null) return; GeselecteerdeVerkoop.Aantal = value ?? 0m; OnPropertyChanged(); OnPropertyChanged(nameof(VerkoopTotaal)); }
    }

    public decimal? VerkoopPrijs
    {
        get => GeselecteerdeVerkoop?.PrijsPerStukIncl;
        set { if (GeselecteerdeVerkoop is null) return; GeselecteerdeVerkoop.PrijsPerStukIncl = value ?? 0m; OnPropertyChanged(); OnPropertyChanged(nameof(VerkoopTotaal)); }
    }

    public decimal? VerkoopKortingPct
    {
        get => GeselecteerdeVerkoop?.KortingPct;
        set { if (GeselecteerdeVerkoop is null) return; GeselecteerdeVerkoop.KortingPct = Math.Clamp(value ?? 0m, 0m, 100m); OnPropertyChanged(); OnPropertyChanged(nameof(VerkoopTotaal)); }
    }

    public decimal VerkoopTotaal => GeselecteerdeVerkoop?.TotaalIncl ?? 0m;

    // ── Snelle maandfilter (zoals de maand-tabbladen in de kassa) ──
    [RelayCommand]
    private void DezeMaand() => ZetMaand(DateTime.Today);

    [RelayCommand]
    private void VorigeMaand() => ZetMaand(DateTime.Today.AddMonths(-1));

    [RelayCommand]
    private void AllePeriodes()
    {
        FilterVanaf = null;
        FilterTot = null;
    }

    private void ZetMaand(DateTime dag)
    {
        var eerste = new DateTime(dag.Year, dag.Month, 1);
        FilterVanaf = new DateTimeOffset(eerste);
        FilterTot = new DateTimeOffset(eerste.AddMonths(1).AddDays(-1));
    }

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        if (OnTerug is not null)
            OnTerug();
        else
            await _nav.NavigateToAsync<HomeViewModel>();
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

            var data = await db.Ontvangsten
                .AsNoTracking()
                .Where(v => v.Soort == OntvangstSoort.Winkelverkoop)
                .OrderByDescending(v => v.Datum)
                .ThenByDescending(v => v.Id)
                .ToListAsync();

            Verkopen = new ObservableCollection<Ontvangst>(data);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden winkelverkopen: {ex.Message}";
            await _dialogs.ShowErrorAsync("Laden mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void Nieuw()
    {
        if (!MagBewerken)
        {
            _toast.Warning("Onvoldoende rechten om winkelverkopen te registreren.");
            return;
        }

        GeselecteerdeVerkoop = new Ontvangst { Soort = OntvangstSoort.Winkelverkoop, Datum = DateTime.Today };
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (GeselecteerdeVerkoop is null) return;

        if (!MagBewerken)
        {
            _toast.Warning("Onvoldoende rechten om winkelverkopen te registreren.");
            return;
        }

        GeselecteerdeVerkoop.Omschrijving = (GeselecteerdeVerkoop.Omschrijving ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(GeselecteerdeVerkoop.Omschrijving))
        {
            _toast.Error("Omschrijving is verplicht.");
            return;
        }
        if (GeselecteerdeVerkoop.Aantal <= 0m)
        {
            _toast.Error("Aantal moet groter zijn dan 0.");
            return;
        }
        if (GeselecteerdeVerkoop.PrijsPerStukIncl < 0m)
        {
            _toast.Error("Prijs (incl. btw) kan niet negatief zijn.");
            return;
        }
        if (GeselecteerdeVerkoop.KortingPct is < 0m or > 100m)
        {
            _toast.Error("Korting moet tussen 0 en 100 % liggen.");
            return;
        }

        // Winkelverkoop = altijd meteen volledig betaald (Veerle, 27/09): ontvangen bedrag = totaal.
        GeselecteerdeVerkoop.Soort = OntvangstSoort.Winkelverkoop;
        GeselecteerdeVerkoop.OfferteId = null;
        GeselecteerdeVerkoop.FactuurId = null;
        GeselecteerdeVerkoop.BedragIncl = GeselecteerdeVerkoop.TotaalIncl;
        if (GeselecteerdeVerkoop.Id == 0)
            GeselecteerdeVerkoop.AangemaaktDoor = _auth.CurrentUser?.GebruikersNaam;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (GeselecteerdeVerkoop.Id == 0)
            {
                db.Ontvangsten.Add(GeselecteerdeVerkoop);
            }
            else
            {
                db.Ontvangsten.Attach(GeselecteerdeVerkoop);
                db.Entry(GeselecteerdeVerkoop).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();

            _toast.Success("Winkelverkoop opgeslagen.");
            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeVerkoop = null;
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

    private bool CanDelete() => GeselecteerdeVerkoop is not null && GeselecteerdeVerkoop.Id != 0;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (GeselecteerdeVerkoop is null) return;

        if (!MagBewerken)
        {
            _toast.Warning("Onvoldoende rechten om winkelverkopen te verwijderen.");
            return;
        }

        var ok = await _dialogs.ConfirmAsync(
            "Winkelverkoop verwijderen",
            $"Ben je zeker dat je '{GeselecteerdeVerkoop.Omschrijving}' wil verwijderen?\n\n" +
            "Dit register heeft geen archief — de verkoop wordt definitief verwijderd " +
            "(blijft traceerbaar via het auditlog).");

        if (!ok) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var dbVerkoop = await db.Ontvangsten.FindAsync(GeselecteerdeVerkoop.Id);
            if (dbVerkoop is null) return;

            db.Ontvangsten.Remove(dbVerkoop);
            await db.SaveChangesAsync();

            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeVerkoop = null;
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
        if (Verkopen.Count == 0)
        {
            GefilterdeVerkopen = new ObservableCollection<Ontvangst>();
            RaiseAggregatesChanged();
            return;
        }

        IEnumerable<Ontvangst> filtered = Verkopen;

        if (!string.IsNullOrWhiteSpace(Zoekterm))
        {
            var term = Zoekterm.Trim().ToLowerInvariant();
            filtered = filtered.Where(v => (v.Omschrijving ?? string.Empty).ToLowerInvariant().Contains(term));
        }

        if (FilterVanaf.HasValue)
        {
            var vanaf = FilterVanaf.Value.Date;
            filtered = filtered.Where(v => v.Datum.Date >= vanaf);
        }

        if (FilterTot.HasValue)
        {
            var tot = FilterTot.Value.Date;
            filtered = filtered.Where(v => v.Datum.Date <= tot);
        }

        // FilterVanaf/FilterTot zijn DateTimeOffset? (DatePicker-binding); .Date werkt ook op
        // DateTimeOffset en levert de lokale datumcomponent op voor de vergelijking.

        GefilterdeVerkopen = new ObservableCollection<Ontvangst>(filtered);
        RaiseAggregatesChanged();
    }

    private void RaiseAggregatesChanged()
    {
        OnPropertyChanged(nameof(AantalVerkopen));
        OnPropertyChanged(nameof(TotaalOmzetIncl));
    }
}
