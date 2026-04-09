using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OffertesLijstViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOfferteNavigationService _offerteNav;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly IOfferteArchiefService _archiefService;
    private readonly IToastService _toast;

    public ObservableCollection<Offerte> Offertes { get; } = new();
    public ObservableCollection<Offerte> FilteredOffertes { get; } = new();

    // ── Jaar-filter ──────────────────────────────────────────────────────────
    public ObservableCollection<int> BeschikbareJaren { get; } = new();

    /// <summary>0 = alle jaren.</summary>
    [ObservableProperty] private int geselecteerdJaar = 0;

    [ObservableProperty] private Offerte? selectedOfferte;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    public OffertesLijstViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IOfferteNavigationService offerteNav,
        IDialogService dialogs,
        INavigationService nav,
        IToastService toast,
        IOfferteArchiefService archiefService)
    {
        _dbFactory      = dbFactory;
        _offerteNav     = offerteNav;
        _dialogs        = dialogs;
        _nav            = nav;
        _toast          = toast;
        _archiefService = archiefService;
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string? value) => ApplyFilter();
    partial void OnGeselecteerdJaarChanged(int value) => ApplyFilter();

    // ── Laden ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        // Onthoud huidige selectie zodat we die na het herladen kunnen herstellen.
        var previousId = SelectedOfferte?.Id;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var list = await db.Offertes
                .Include(o => o.Klant)
                .OrderByDescending(o => o.Datum)
                .ToListAsync();

            Offertes.Clear();
            foreach (var o in list) Offertes.Add(o);

            // Bouw jaar-dropdown
            var jaren = list
                .Select(o => o.Datum.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            BeschikbareJaren.Clear();
            BeschikbareJaren.Add(0); // "Alle jaren"
            foreach (var j in jaren) BeschikbareJaren.Add(j);

            ApplyFilter();

            // Herstel selectie na herladen (nieuwe objectinstanties, zelfde Id).
            if (previousId.HasValue)
                SelectedOfferte = FilteredOffertes.FirstOrDefault(o => o.Id == previousId.Value);
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredOffertes.Clear();
        var q = Offertes.AsEnumerable();

        if (GeselecteerdJaar > 0)
            q = q.Where(o => o.Datum.Year == GeselecteerdJaar);

        var term = Zoekterm?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(o =>
                o.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (o.Klant != null &&
                 $"{o.Klant.Voornaam} {o.Klant.Achternaam}"
                    .Contains(term, StringComparison.OrdinalIgnoreCase)));

        foreach (var o in q) FilteredOffertes.Add(o);
    }

    // ── Navigatie ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewAsync() => await _offerteNav.NewOfferteAsync();

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (SelectedOfferte is null) return;
        await _offerteNav.OpenOfferteAsync(SelectedOfferte.Id);
    }

    // ── Verwijderen = archiveren ──────────────────────────────────────────────
    // Offertes worden nooit permanent verwijderd vanuit de lijst.
    // Ze gaan naar het archief (volledige snapshot bewaard) zodat ze herstelbaar blijven.
    // Permanent verwijderen kan enkel vanuit het archief zelf.

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedOfferte is null) return;

        var klantNaam = SelectedOfferte.Klant is { } k
            ? $"{k.Voornaam} {k.Achternaam}".Trim()
            : $"offerte #{SelectedOfferte.Id}";

        var ok = await _dialogs.ConfirmAsync(
            "Offerte naar archief",
            $"'{klantNaam}' (#{SelectedOfferte.Id}) verplaatsen naar het archief? " +
            $"De offerte verdwijnt uit de actieve lijst maar blijft volledig herstelbaar via Archief.");
        if (!ok) return;

        IsBusy = true;
        try
        {
            await _archiefService.ArchiveerAsync(SelectedOfferte.Id);
            _toast.Success($"Offerte #{SelectedOfferte.Id} gearchiveerd.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _toast.Error($"Archiveren mislukt: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // ── Archief openen ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenArchiefAsync() => await _nav.NavigateToAsync<ArchiefViewModel>();

    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<HomeViewModel>();
}
