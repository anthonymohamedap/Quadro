using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class FacturenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IFactuurWorkflowService _workflow;
    private readonly IFactuurExportService _exportService;
    private readonly IToastService _toast;

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

    public FacturenViewModel(
        IDbContextFactory<AppDbContext> factory,
        IFactuurWorkflowService workflow,
        IFactuurExportService exportService,
        IToastService toast)
    {
        _factory = factory;
        _workflow = workflow;
        _exportService = exportService;
        _toast = toast;

        RefreshCommand = new AsyncRelayCommand(InitializeAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        MarkeerKlaarVoorExportCommand = new AsyncRelayCommand(MarkeerKlaarVoorExportAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
        MarkeerBetaaldCommand = new AsyncRelayCommand(MarkeerBetaaldAsync);
    }

    public async Task InitializeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await FactuurSchemaUpgrade.EnsureAsync(db);
        var query = db.Facturen.Include(x => x.Lijnen).AsQueryable();

        if (!string.IsNullOrWhiteSpace(FilterTekst))
        {
            var ft = FilterTekst.Trim();
            query = query.Where(x => x.FactuurNummer.Contains(ft) || x.KlantNaam.Contains(ft) || x.Status.ToString().Contains(ft));
        }

        var items = await query.OrderByDescending(x => x.Id).ToListAsync();
        Facturen = new ObservableCollection<Factuur>(items);
    }

    partial void OnFilterTekstChanged(string value) => _ = InitializeAsync();

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
        _toast.Success("Factuur opgeslagen.");
        await InitializeAsync();
    }

    private async Task MarkeerKlaarVoorExportAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        await _workflow.MarkeerKlaarVoorExportAsync(GeselecteerdeFactuur.Id);
        _toast.Success("Factuur staat klaar voor export.");
        await InitializeAsync();
    }

    private async Task ExportPdfAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        var result = await _exportService.ExportAsync(GeselecteerdeFactuur.Id, ExportFormaat.Pdf, "exports");
        if (result.Success) _toast.Success(result.Message);
        else _toast.Error(result.Message);
        await InitializeAsync();
    }

    private async Task MarkeerBetaaldAsync()
    {
        if (GeselecteerdeFactuur is null) return;
        await _workflow.MarkeerBetaaldAsync(GeselecteerdeFactuur.Id);
        _toast.Success("Factuur gemarkeerd als betaald.");
        await InitializeAsync();
    }
}
