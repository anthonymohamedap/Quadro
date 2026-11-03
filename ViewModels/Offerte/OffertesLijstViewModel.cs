using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Verkoop;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>US-84 — één tabblad van de offertelijst, met het aantal offertes in die fase.</summary>
public sealed record FaseTab(VerkoopFase Fase, string Titel, int Aantal)
{
    public string Label => $"{Titel} ({Aantal})";
}

public partial class OffertesLijstViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOfferteNavigationService _offerteNav;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly IOfferteArchiefService _archiefService;
    private readonly IToastService _toast;
    private readonly IVerkoopOverzichtService _overzicht;
    private readonly IWerkBonWorkflowService? _werkBonWorkflow;
    private readonly IWorkflowService? _workflow;

    private List<VerkoopOverzichtRegel> _alle = new();
    public ObservableCollection<VerkoopOverzichtRegel> FilteredOffertes { get; } = new();

    // ── US-84: tabbladen per fase ────────────────────────────────────────────
    private static readonly (VerkoopFase Fase, string Titel)[] TabDefinities =
    {
        (VerkoopFase.OpenOffertes, "Open offertes"),
        (VerkoopFase.LopendeWerkbonnen, "Lopende werkbonnen"),
        (VerkoopFase.KlaarOmAfTeHalen, "Klaar om af te halen"),
        (VerkoopFase.Afgerond, "Afgerond"),
        (VerkoopFase.Geannuleerd, "Geannuleerd")
    };

    public ObservableCollection<FaseTab> Tabs { get; } = new();

    /// <summary>Standaard "Open offertes" (AC 2).</summary>
    [ObservableProperty] private VerkoopFase geselecteerdeFase = VerkoopFase.OpenOffertes;

    /// <summary>Zoeken over alle tabbladen heen in plaats van enkel in het gekozen tabblad.</summary>
    [ObservableProperty] private bool zoekInAlle;

    private bool _syncingTab;
    [ObservableProperty] private FaseTab? geselecteerdeTab;

    public bool IsLeeg => FilteredOffertes.Count == 0;

    // ── Jaar-filter ──────────────────────────────────────────────────────────
    public ObservableCollection<int> BeschikbareJaren { get; } = new();

    /// <summary>0 = alle jaren.</summary>
    [ObservableProperty] private int geselecteerdJaar = 0;

    [ObservableProperty] private VerkoopOverzichtRegel? selectedOfferte;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    public OffertesLijstViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IOfferteNavigationService offerteNav,
        IDialogService dialogs,
        INavigationService nav,
        IToastService toast,
        IOfferteArchiefService archiefService,
        IVerkoopOverzichtService overzicht,
        IWerkBonWorkflowService? werkBonWorkflow = null,
        IWorkflowService? workflow = null)
    {
        _werkBonWorkflow = werkBonWorkflow;
        _workflow       = workflow;
        _overzicht      = overzicht;
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
    partial void OnGeselecteerdeFaseChanged(VerkoopFase value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsWerkbonTab));
    }

    /// <summary>US-89 — tabblad "Lopende werkbonnen": ook Planning en Werklijst van een week (zoals de Werkbonnenlijst).</summary>
    public bool IsWerkbonTab => GeselecteerdeFase == VerkoopFase.LopendeWerkbonnen;
    partial void OnZoekInAlleChanged(bool value) => ApplyFilter();

    partial void OnGeselecteerdeTabChanged(FaseTab? value)
    {
        if (_syncingTab || value is null) return;
        GeselecteerdeFase = value.Fase;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        // Onthoud huidige selectie zodat we die na het herladen kunnen herstellen.
        var previousId = SelectedOfferte?.OfferteId;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            var list = (await _overzicht.GetOverzichtAsync()).ToList();
            _alle = list;

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
                SelectedOfferte = FilteredOffertes.FirstOrDefault(o => o.OfferteId == previousId.Value);
        }
        catch (Exception ex)
        {
            // Fix: laadfouten waren onzichtbaar (Foutmelding was nergens gebonden).
            // Toon nu een toast, consistent met DeleteAsync.
            Foutmelding = $"Fout bij laden: {ex.Message}";
            _toast.Error($"Laden van offertes mislukt: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        var binnenJaar = _alle.AsEnumerable();
        if (GeselecteerdJaar > 0)
            binnenJaar = binnenJaar.Where(o => o.Datum.Year == GeselecteerdJaar);
        var jaarLijst = binnenJaar.ToList();

        // Tabbladen met aantallen (binnen het gekozen jaar).
        _syncingTab = true;
        try
        {
            Tabs.Clear();
            foreach (var (fase, titel) in TabDefinities)
                Tabs.Add(new FaseTab(fase, titel, jaarLijst.Count(o => o.Fase == fase)));
            GeselecteerdeTab = Tabs.FirstOrDefault(t => t.Fase == GeselecteerdeFase);
        }
        finally { _syncingTab = false; }

        FilteredOffertes.Clear();
        foreach (var o in Filter(jaarLijst, GeselecteerdeFase, Zoekterm, ZoekInAlle))
            FilteredOffertes.Add(o);
        OnPropertyChanged(nameof(IsLeeg));
    }

    /// <summary>US-84 — zoeken binnen het gekozen tabblad, of in alle tabbladen als <paramref name="zoekInAlle"/>
    /// aan staat en er een zoekterm is. Zoekt op offertenummer, klantnaam en bestelbonnummer (US-89).</summary>
    public static IEnumerable<VerkoopOverzichtRegel> Filter(
        IEnumerable<VerkoopOverzichtRegel> regels, VerkoopFase fase, string? zoekterm, bool zoekInAlle)
    {
        var term = zoekterm?.Trim();
        var heeftTerm = !string.IsNullOrWhiteSpace(term);

        var q = zoekInAlle && heeftTerm ? regels : regels.Where(o => o.Fase == fase);

        if (heeftTerm)
            q = q.Where(o =>
                o.OfferteNummer.ToString().Contains(term!, StringComparison.OrdinalIgnoreCase) ||
                o.KlantNaam.Contains(term!, StringComparison.OrdinalIgnoreCase) ||
                (o.BestelbonNummer ?? "").Contains(term!, StringComparison.OrdinalIgnoreCase));

        return q;
    }

    // ── Navigatie ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewAsync() => await _offerteNav.NewOfferteAsync();

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (SelectedOfferte is null) return;
        await _offerteNav.OpenOfferteAsync(SelectedOfferte.OfferteId);
    }

    /// <summary>US-90 — het nieuwe verkoopdossier, naast de oude schermen (fase B: Veerle test).</summary>
    [RelayCommand]
    private async Task OpenDossierAsync()
    {
        if (SelectedOfferte is null) return;
        await _nav.NavigateToAsync<QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel, int>(SelectedOfferte.OfferteId);
    }

    // ── US-89: knoppen van de Werkbonnenlijst ────────────────────────────────

    /// <summary>Planning openen voor de werkbon van de gekozen verkoop (zelfde venster als in Werkbonnen).</summary>
    [RelayCommand]
    private async Task OpenPlanningAsync()
    {
        if (SelectedOfferte?.WerkBonId is not int werkBonId || _werkBonWorkflow is null || _workflow is null)
        {
            _toast.Warning("Kies eerst een verkoop met een werkbon.");
            return;
        }

        // US-92: planning als pagina; "Terug" komt terug in deze lijst (zelfde tabblad, herlaadt).
        var lijst = this;
        await PlanningPaginaViewModel.ToonAsync(_nav, _dbFactory, _werkBonWorkflow, _toast, _workflow,
            werkBonId, () => _nav.NavigateToAsync(lijst));
    }

    /// <summary>Werklijst van een week (US-98), opent op de huidige week.</summary>
    [RelayCommand]
    private async Task OpenWeekLijstAsync()
    {
        if (_workflow is null) return;
        var vandaag = DateTime.Today;
        var vm = new WeekWerkLijstViewModel(_dbFactory, _workflow, _toast);
        await vm.InitializeAsync(System.Globalization.ISOWeek.GetYear(vandaag), System.Globalization.ISOWeek.GetWeekOfYear(vandaag));
        var window = new QuadroApp.Views.WeekWerkLijstWindow { DataContext = vm };
        if (App.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            await window.ShowDialog(owner);

        // Afdrukken van de weeklijst kan verkopen op In productie zetten (US-87).
        await LoadAsync();
    }

    // ── Verwijderen = archiveren ──────────────────────────────────────────────
    // Offertes worden nooit permanent verwijderd vanuit de lijst.
    // Ze gaan naar het archief (volledige snapshot bewaard) zodat ze herstelbaar blijven.
    // Permanent verwijderen kan enkel vanuit het archief zelf.

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedOfferte is null) return;

        var klantNaam = !string.IsNullOrWhiteSpace(SelectedOfferte.KlantNaam)
            ? SelectedOfferte.KlantNaam
            : $"offerte #{SelectedOfferte.OfferteNummer}";

        var ok = await _dialogs.ConfirmAsync(
            "Offerte naar archief",
            $"'{klantNaam}' (#{SelectedOfferte.OfferteNummer}) verplaatsen naar het archief? " +
            $"De offerte verdwijnt uit de actieve lijst maar blijft volledig herstelbaar via Archief.");
        if (!ok) return;

        IsBusy = true;
        try
        {
            await _archiefService.ArchiveerAsync(SelectedOfferte.OfferteId);
            _toast.Success($"Offerte #{SelectedOfferte.OfferteNummer} gearchiveerd.");
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
