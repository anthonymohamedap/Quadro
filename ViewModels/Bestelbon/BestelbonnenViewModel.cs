using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class BestelbonnenViewModel : AsyncViewModelBase, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IBestelbonWorkflowService _workflow;
    private readonly IBestelbonExportService _exportService;
    private readonly IPathOpener _pathOpener;
    private readonly IToastService _toast;
    private readonly INavigationService _nav;
    private readonly IOntvangstService _ontvangsten;   // US-70

    [ObservableProperty] private ObservableCollection<Bestelbon> bestelbonnen = new();
    [ObservableProperty] private Bestelbon? geselecteerdeBestelbon;
    [ObservableProperty] private string filterTekst = string.Empty;

    [ObservableProperty] private DateTimeOffset? bestelbonDatum;
    [ObservableProperty] private DateTimeOffset? vervalDatum;
    [ObservableProperty] private string? opmerking;
    [ObservableProperty] private string? aangenomenDoorInitialen;

    public ObservableCollection<BestelbonLijn> Lijnen { get; } = new();
    public Array Statussen => Enum.GetValues(typeof(BestelbonStatus));

    public bool IsDraft => GeselecteerdeBestelbon?.Status == BestelbonStatus.Draft;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand AfdrukkenCommand { get; }
    public IAsyncRelayCommand MarkeerBetaaldCommand { get; }
    public IAsyncRelayCommand GaTerugCommand { get; }

    public BestelbonnenViewModel(
        IDbContextFactory<AppDbContext> factory,
        IBestelbonWorkflowService workflow,
        IBestelbonExportService exportService,
        IToastService toast,
        INavigationService nav,
        IOntvangstService ontvangsten,
        IPathOpener pathOpener)
        : base(toast)
    {
        _pathOpener = pathOpener;
        _ontvangsten = ontvangsten;
        _factory = factory;
        _workflow = workflow;
        _exportService = exportService;
        _toast = toast;
        _nav = nav;

        RefreshCommand = new AsyncRelayCommand(InitializeAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AfdrukkenCommand = new AsyncRelayCommand(AfdrukkenAsync);
        MarkeerBetaaldCommand = new AsyncRelayCommand(MarkeerBetaaldAsync);
        GaTerugCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<HomeViewModel>());
    }

    public async Task InitializeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Bestelbonnen.Include(x => x.Lijnen).AsQueryable();

        if (!string.IsNullOrWhiteSpace(FilterTekst))
        {
            var ft = FilterTekst.Trim();
            query = query.Where(x => x.BestelbonNummer.Contains(ft) || x.KlantNaam.Contains(ft) || x.Status.ToString().Contains(ft));
        }

        var items = await query.OrderByDescending(x => x.Id).ToListAsync();
        Bestelbonnen = new ObservableCollection<Bestelbon>(items);
    }

    partial void OnFilterTekstChanged(string value) => RunAsync(InitializeAsync);

    partial void OnGeselecteerdeBestelbonChanged(Bestelbon? value)
    {
        Lijnen.Clear();
        if (value is null)
            return;

        BestelbonDatum = new DateTimeOffset(value.BestelbonDatum);
        VervalDatum = new DateTimeOffset(value.VervalDatum);
        Opmerking = value.Opmerking;
        AangenomenDoorInitialen = value.AangenomenDoorInitialen;

        foreach (var l in value.Lijnen.OrderBy(x => x.Sortering))
            Lijnen.Add(l);

        OnPropertyChanged(nameof(IsDraft));
        RunAsync(LaadBetalingenAsync);   // US-70
    }

    // ── US-70: betalingen op de bestelbon (vooraf, bij afhalen of achteraf; meerdere mogelijk) ──
    public ObservableCollection<Ontvangst> Betalingen { get; } = new();
    public IReadOnlyList<Betaalwijze> BetaalwijzeOpties { get; } = Enum.GetValues<Betaalwijze>();
    [ObservableProperty] private BetaalStand? stand;
    [ObservableProperty] private decimal? nieuweBetalingBedrag;
    [ObservableProperty] private DateTimeOffset? nieuweBetalingDatum = new DateTimeOffset(DateTime.Today);
    [ObservableProperty] private Betaalwijze nieuweBetalingBetaalwijze = Betaalwijze.Bancontact;

    public decimal StandTotaal => Stand?.TotaalIncl ?? 0m;
    public decimal StandVoorschot => Stand?.Voorschot ?? 0m;
    public decimal StandBetaald => Stand?.Betaald ?? 0m;
    public decimal StandRest => Stand?.Rest ?? 0m;
    public bool HeeftRest => StandRest > 0m;

    partial void OnStandChanged(BetaalStand? value)
    {
        OnPropertyChanged(nameof(StandTotaal));
        OnPropertyChanged(nameof(StandVoorschot));
        OnPropertyChanged(nameof(StandBetaald));
        OnPropertyChanged(nameof(StandRest));
        OnPropertyChanged(nameof(HeeftRest));
    }

    private async Task LaadBetalingenAsync()
    {
        Betalingen.Clear();
        Stand = null;
        if (GeselecteerdeBestelbon is not { Id: > 0 } f) return;

        foreach (var b in await _ontvangsten.GetBetalingenAsync(f.Id))
            Betalingen.Add(b);
        Stand = await _ontvangsten.GetBetaalStandAsync(f.Id);
        NieuweBetalingBedrag = Stand.Rest > 0m ? Stand.Rest : null;   // standaard: het restbedrag
    }

    [RelayCommand]
    private async Task RegistreerBetalingAsync()
    {
        if (GeselecteerdeBestelbon is null) return;
        if (NieuweBetalingBedrag is not > 0m)
        {
            _toast.Warning("Geef een bedrag groter dan 0 in.");
            return;
        }

        try
        {
            var bedrag = NieuweBetalingBedrag.Value;
            var nieuw = await _ontvangsten.RegistreerBetalingAsync(
                GeselecteerdeBestelbon.Id, bedrag, (NieuweBetalingDatum ?? DateTimeOffset.Now).Date, NieuweBetalingBetaalwijze);

            _toast.Success(nieuw.Rest == 0m
                ? $"Betaling van € {bedrag:0.00} geregistreerd — bestelbon volledig betaald."
                : $"Betaling van € {bedrag:0.00} geregistreerd — nog te betalen: € {nieuw.Rest:0.00}.");

            var id = GeselecteerdeBestelbon.Id;
            await InitializeAsync();
            GeselecteerdeBestelbon = Bestelbonnen.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.GetBaseException().Message);
        }
    }

    [RelayCommand]
    private async Task VerwijderBetalingAsync(Ontvangst? betaling)
    {
        if (betaling is null) return;
        try
        {
            await _ontvangsten.VerwijderAsync(betaling.Id);
            _toast.Success("Betaling verwijderd.");

            // US-72: de status van de bestelbon kan terug open gegaan zijn → lijst herladen.
            var id = GeselecteerdeBestelbon?.Id;
            await InitializeAsync();
            GeselecteerdeBestelbon = id is null ? null : Bestelbonnen.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.GetBaseException().Message);
        }
    }

    private async Task SaveAsync()
    {
        if (GeselecteerdeBestelbon is null) return;
        GeselecteerdeBestelbon.BestelbonDatum = (BestelbonDatum ?? DateTimeOffset.Now).Date;
        GeselecteerdeBestelbon.VervalDatum = (VervalDatum ?? DateTimeOffset.Now).Date;
        GeselecteerdeBestelbon.Opmerking = Opmerking;
        GeselecteerdeBestelbon.AangenomenDoorInitialen = AangenomenDoorInitialen;
        GeselecteerdeBestelbon.Lijnen = Lijnen;

        await _workflow.SaveDraftAsync(GeselecteerdeBestelbon);
        _toast.Success("Bestelbon opgeslagen.");
        await InitializeAsync();
    }

    /// <summary>US-82 — één knop: PDF in Documenten/QuadroApp/Bestelbonnen en meteen openen,
    /// in elke status (ook Betaald).</summary>
    private async Task AfdrukkenAsync()
    {
        if (GeselecteerdeBestelbon is null) return;

        try
        {
            var result = await _exportService.AfdrukkenAsync(GeselecteerdeBestelbon.Id);
            if (!result.Success)
            {
                _toast.Error(result.Message);
                return;
            }

            _pathOpener.OpenFile(result.BestandPad);
        }
        catch (Exception ex)
        {
            _toast.Error($"Afdrukken mislukt: {ex.GetBaseException().Message}");
        }

        await InitializeAsync();
    }

    /// <summary>US-70: "Markeer betaald" registreert het restbedrag als betaling (met de gekozen
    /// betaalwijze), zodat het mee in het overzicht betalingen komt. Geen rest meer → enkel status.</summary>
    private async Task MarkeerBetaaldAsync()
    {
        if (GeselecteerdeBestelbon is null) return;

        var stand = await _ontvangsten.GetBetaalStandAsync(GeselecteerdeBestelbon.Id);
        if (stand.Rest > 0m)
        {
            NieuweBetalingBedrag = stand.Rest;
            await RegistreerBetalingAsync();
            return;
        }

        await _workflow.MarkeerBetaaldAsync(GeselecteerdeBestelbon.Id);
        _toast.Success("Bestelbon gemarkeerd als betaald.");
        await InitializeAsync();
    }
}
