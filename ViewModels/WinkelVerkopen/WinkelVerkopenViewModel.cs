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

/// <summary>US-66 — los register voor winkelverkopen (toonbankverkopen los van offerte/factuur).
/// 1-op-1 naar het patroon van <see cref="KantKlaarKaderenViewModel"/> (Load/Save/Delete/Filter),
/// met een extra ComboBox voor <see cref="Betaalwijze"/> en een datumfilter i.p.v. enkel
/// naam-filter. Hergebruikt <see cref="Permissie.Factureren"/> — geen nieuwe permissie nodig.
/// Hard delete is hier aanvaardbaar (geen soft-delete-filter), in tegenstelling tot
/// <see cref="KantKlaarKader"/> waar OfferteRegel naar verwijst.</summary>
public partial class WinkelVerkopenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;
    private readonly IAuthService _auth;

    [ObservableProperty] private ObservableCollection<WinkelVerkoop> verkopen = new();
    [ObservableProperty] private ObservableCollection<WinkelVerkoop> gefilterdeVerkopen = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private WinkelVerkoop? geselecteerdeVerkoop;

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
    public decimal TotaalOmzetIncl => GefilterdeVerkopen?.Sum(v => v.TotaalInclBtw) ?? 0m;

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
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilter();
    partial void OnFilterVanafChanged(DateTimeOffset? value) => ApplyFilter();
    partial void OnFilterTotChanged(DateTimeOffset? value) => ApplyFilter();

    partial void OnGeselecteerdeVerkoopChanged(WinkelVerkoop? value)
    {
        IsDetailOpen = value is not null;
        OnPropertyChanged(nameof(VerkoopDatum));
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

            var data = await db.WinkelVerkopen
                .AsNoTracking()
                .OrderByDescending(v => v.Datum)
                .ThenByDescending(v => v.Id)
                .ToListAsync();

            Verkopen = new ObservableCollection<WinkelVerkoop>(data);
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

        GeselecteerdeVerkoop = new WinkelVerkoop();
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
        if (GeselecteerdeVerkoop.PrijsInclBtw < 0m)
        {
            _toast.Error("Prijs (incl. btw) kan niet negatief zijn.");
            return;
        }
        if (GeselecteerdeVerkoop.BtwPct < 0m)
        {
            _toast.Error("Btw-percentage kan niet negatief zijn.");
            return;
        }

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (GeselecteerdeVerkoop.Id == 0)
            {
                db.WinkelVerkopen.Add(GeselecteerdeVerkoop);
            }
            else
            {
                db.WinkelVerkopen.Attach(GeselecteerdeVerkoop);
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

            var dbVerkoop = await db.WinkelVerkopen.FindAsync(GeselecteerdeVerkoop.Id);
            if (dbVerkoop is null) return;

            db.WinkelVerkopen.Remove(dbVerkoop);
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
            GefilterdeVerkopen = new ObservableCollection<WinkelVerkoop>();
            RaiseAggregatesChanged();
            return;
        }

        IEnumerable<WinkelVerkoop> filtered = Verkopen;

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

        GefilterdeVerkopen = new ObservableCollection<WinkelVerkoop>(filtered);
        RaiseAggregatesChanged();
    }

    private void RaiseAggregatesChanged()
    {
        OnPropertyChanged(nameof(AantalVerkopen));
        OnPropertyChanged(nameof(TotaalOmzetIncl));
    }
}
