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

public partial class FacturenViewModel : AsyncViewModelBase, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IFactuurWorkflowService _workflow;
    private readonly IFactuurExportService _exportService;
    private readonly IToastService _toast;
    private readonly INavigationService _nav;
    private readonly IOntvangstService _ontvangsten;   // US-70

    [ObservableProperty] private ObservableCollection<Factuur> facturen = new();
    [ObservableProperty] private Factuur? geselecteerdeFactuur;
    [ObservableProperty] private string filterTekst = string.Empty;

    [ObservableProperty] private DateTimeOffset? factuurDatum;
    [ObservableProperty] private DateTimeOffset? vervalDatum;
    [ObservableProperty] private string? opmerking;
    [ObservableProperty] private string? aangenomenDoorInitialen;

    public ObservableCollection<FactuurLijn> Lijnen { get; } = new();
    public Array Statussen => Enum.GetValues(typeof(FactuurStatus));

    public bool IsDraft => GeselecteerdeFactuur?.Status == FactuurStatus.Draft;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand MarkeerKlaarVoorExportCommand { get; }
    public IAsyncRelayCommand ExportPdfCommand { get; }
    public IAsyncRelayCommand MarkeerBetaaldCommand { get; }
    public IAsyncRelayCommand GaTerugCommand { get; }

    public FacturenViewModel(
        IDbContextFactory<AppDbContext> factory,
        IFactuurWorkflowService workflow,
        IFactuurExportService exportService,
        IToastService toast,
        INavigationService nav,
        IOntvangstService ontvangsten)
        : base(toast)
    {
        _ontvangsten = ontvangsten;
        _factory = factory;
        _workflow = workflow;
        _exportService = exportService;
        _toast = toast;
        _nav = nav;

        RefreshCommand = new AsyncRelayCommand(InitializeAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        MarkeerKlaarVoorExportCommand = new AsyncRelayCommand(MarkeerKlaarVoorExportAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        MarkeerBetaaldCommand = new AsyncRelayCommand(MarkeerBetaaldAsync);
        GaTerugCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<HomeViewModel>());
    }

    public async Task InitializeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Facturen.Include(x => x.Lijnen).AsQueryable();

        if (!string.IsNullOrWhiteSpace(FilterTekst))
        {
            var ft = FilterTekst.Trim();
            query = query.Where(x => x.FactuurNummer.Contains(ft) || x.KlantNaam.Contains(ft) || x.Status.ToString().Contains(ft));
        }

        var items = await query.OrderByDescending(x => x.Id).ToListAsync();
        Facturen = new ObservableCollection<Factuur>(items);
    }

    partial void OnFilterTekstChanged(string value) => RunAsync(InitializeAsync);

    partial void OnGeselecteerdeFactuurChanged(Factuur? value)
    {
        Lijnen.Clear();
        if (value is null)
            return;

        FactuurDatum = new DateTimeOffset(value.FactuurDatum);
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
        if (GeselecteerdeFactuur is not { Id: > 0 } f) return;

        foreach (var b in await _ontvangsten.GetBetalingenAsync(f.Id))
            Betalingen.Add(b);
        Stand = await _ontvangsten.GetBetaalStandAsync(f.Id);
        NieuweBetalingBedrag = Stand.Rest > 0m ? Stand.Rest : null;   // standaard: het restbedrag
    }

    [RelayCommand]
    private async Task RegistreerBetalingAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        if (NieuweBetalingBedrag is not > 0m)
        {
            _toast.Warning("Geef een bedrag groter dan 0 in.");
            return;
        }

        try
        {
            var bedrag = NieuweBetalingBedrag.Value;
            var nieuw = await _ontvangsten.RegistreerBetalingAsync(
                GeselecteerdeFactuur.Id, bedrag, (NieuweBetalingDatum ?? DateTimeOffset.Now).Date, NieuweBetalingBetaalwijze);

            _toast.Success(nieuw.Rest == 0m
                ? $"Betaling van € {bedrag:0.00} geregistreerd — bestelbon volledig betaald."
                : $"Betaling van € {bedrag:0.00} geregistreerd — nog te betalen: € {nieuw.Rest:0.00}.");

            var id = GeselecteerdeFactuur.Id;
            await InitializeAsync();
            GeselecteerdeFactuur = Facturen.FirstOrDefault(x => x.Id == id);
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
            await LaadBetalingenAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ex.GetBaseException().Message);
        }
    }

    private async Task SaveAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        GeselecteerdeFactuur.FactuurDatum = (FactuurDatum ?? DateTimeOffset.Now).Date;
        GeselecteerdeFactuur.VervalDatum = (VervalDatum ?? DateTimeOffset.Now).Date;
        GeselecteerdeFactuur.Opmerking = Opmerking;
        GeselecteerdeFactuur.AangenomenDoorInitialen = AangenomenDoorInitialen;
        GeselecteerdeFactuur.Lijnen = Lijnen;

        await _workflow.SaveDraftAsync(GeselecteerdeFactuur);
        _toast.Success("Bestelbon opgeslagen.");
        await InitializeAsync();
    }

    private async Task MarkeerKlaarVoorExportAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        await _workflow.MarkeerKlaarVoorExportAsync(GeselecteerdeFactuur.Id);
        _toast.Success("Bestelbon staat klaar voor export.");
        await InitializeAsync();
    }

    private async Task ExportPdfAsync()
    {
        if (GeselecteerdeFactuur is null) return;

        // Bug 1 fix: guard against wrong status so the user gets clear feedback
        // instead of a silent crash swallowed by AsyncRelayCommand.
        if (GeselecteerdeFactuur.Status != FactuurStatus.KlaarVoorExport)
        {
            _toast.Error("Markeer de bestelbon eerst als 'Klaar voor export'.");
            return;
        }

        // Bug 3 fix: BaseDirectory on a single-file publish points to a temp
        // extraction folder in %TEMP% — nobody finds it. Use MyDocuments instead.
        var exportFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Quadro", "exports");
        Directory.CreateDirectory(exportFolder);

        try
        {
            var result = await _exportService.ExportAsync(GeselecteerdeFactuur.Id, ExportFormaat.Pdf, exportFolder);
            if (result.Success) _toast.Success($"{result.Message} → {exportFolder}");
            else _toast.Error(result.Message);
        }
        catch (Exception ex)
        {
            _toast.Error($"Export mislukt: {ex.GetBaseException().Message}");
        }

        await InitializeAsync();
    }

    /// <summary>US-70: "Markeer betaald" registreert het restbedrag als betaling (met de gekozen
    /// betaalwijze), zodat het mee in het overzicht betalingen komt. Geen rest meer → enkel status.</summary>
    private async Task MarkeerBetaaldAsync()
    {
        if (GeselecteerdeFactuur is null) return;

        var stand = await _ontvangsten.GetBetaalStandAsync(GeselecteerdeFactuur.Id);
        if (stand.Rest > 0m)
        {
            NieuweBetalingBedrag = stand.Rest;
            await RegistreerBetalingAsync();
            return;
        }

        await _workflow.MarkeerBetaaldAsync(GeselecteerdeFactuur.Id);
        _toast.Success("Bestelbon gemarkeerd als betaald.");
        await InitializeAsync();
    }
}
