using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Verkoop;
using QuadroApp.ViewModels.Verkoop;
using QuadroApp.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OfferteWorkflowViewModel : AsyncViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOfferteWorkflowService _workflow;
    private readonly IWerkBonWorkflowService _werkBonWorkflow;
    private readonly IWorkflowService _statusWorkflow;
    private readonly IFactuurWorkflowService _factuurWorkflow;
    private readonly IFactuurExportService _factuurExportService;
    private readonly IFilePickerService _filePickerService;
    private readonly IDialogService _dialogs;
    private readonly IOntvangstService _ontvangsten;
    private readonly IPathOpener _pathOpener;
    private readonly IOfferteNavigationService _offerteNav;

    // Delegates geleverd door OfferteViewModel
    private readonly Func<int> _getOfferteId;
    private readonly Func<Task> _saveAndReload;
    private readonly Func<Task> _berekenAsync;
    private readonly Func<bool, Task<bool>> _runValidation;
    private readonly Func<Task>? _reloadOnly;

    [ObservableProperty] private WerkBon? gekoppeldeWerkBon;
    [ObservableProperty] private Factuur? gekoppeldeFactuur;
    [ObservableProperty] private bool isBusy;

    public string FactuurButtonText =>
        GekoppeldeFactuur is null ? "Bestelbon aanmaken" : "Bestelbon bekijken";

    public string FactuurStatusText
    {
        get
        {
            if (GekoppeldeFactuur is not null)
                return $"Bestelbon {GekoppeldeFactuur.FactuurNummer} ({GekoppeldeFactuur.Status.Label()})";

            if (GekoppeldeWerkBon is null)
                return "Nog geen bestelbon voor deze offerte.";

            return $"Nog geen bestelbon. Werkbon: {GekoppeldeWerkBon.Status.Label()}.";
        }
    }

    partial void OnGekoppeldeWerkBonChanged(WerkBon? value)
    {
        OnPropertyChanged(nameof(FactuurStatusText));
        OnPropertyChanged(nameof(IsBevestigenZichtbaar));
        OnPropertyChanged(nameof(IsPlanningZichtbaar));
    }

    partial void OnGekoppeldeFactuurChanged(Factuur? value)
    {
        OnPropertyChanged(nameof(FactuurButtonText));
        OnPropertyChanged(nameof(FactuurStatusText));
    }

    // Bevestigen is alleen zichtbaar als er nog geen werkbon is.
    // Zodra de werkbon bestaat → Planning-knop tonen i.p.v. Bevestigen-knop.
    public bool IsBevestigenZichtbaar => GekoppeldeWerkBon is null;
    public bool IsPlanningZichtbaar   => GekoppeldeWerkBon is not null;

    public IAsyncRelayCommand BevestigenCommand { get; }
    public IAsyncRelayCommand OpenPlanningCommand { get; }
    public IAsyncRelayCommand FactuurCommand { get; }
    public IAsyncRelayCommand OpenWerkBonCommand { get; }
    public IAsyncRelayCommand BestelbonAfdrukkenCommand { get; }

    /// <summary>US-83 — stappenbalk bovenaan de offerte.</summary>
    public StappenBalkViewModel StappenBalk { get; } = new();

    public OfferteWorkflowViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IOfferteWorkflowService workflow,
        IWerkBonWorkflowService werkBonWorkflow,
        IWorkflowService statusWorkflow,
        IFactuurWorkflowService factuurWorkflow,
        IFactuurExportService factuurExportService,
        IFilePickerService filePickerService,
        IDialogService dialogs,
        IToastService toast,
        IOntvangstService ontvangsten,
        IPathOpener pathOpener,
        IOfferteNavigationService offerteNav,
        Func<int> getOfferteId,
        Func<Task> saveAndReload,
        Func<Task> berekenAsync,
        Func<bool, Task<bool>> runValidation,
        Func<Task>? reloadOnly = null)
        : base(toast)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _werkBonWorkflow = werkBonWorkflow ?? throw new ArgumentNullException(nameof(werkBonWorkflow));
        _statusWorkflow = statusWorkflow ?? throw new ArgumentNullException(nameof(statusWorkflow));
        _factuurWorkflow = factuurWorkflow ?? throw new ArgumentNullException(nameof(factuurWorkflow));
        _factuurExportService = factuurExportService ?? throw new ArgumentNullException(nameof(factuurExportService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _ontvangsten = ontvangsten ?? throw new ArgumentNullException(nameof(ontvangsten));
        _pathOpener = pathOpener ?? throw new ArgumentNullException(nameof(pathOpener));
        _offerteNav = offerteNav ?? throw new ArgumentNullException(nameof(offerteNav));
        _getOfferteId = getOfferteId;
        _saveAndReload = saveAndReload;
        _berekenAsync = berekenAsync;
        _runValidation = runValidation;
        _reloadOnly = reloadOnly;

        BevestigenCommand   = new AsyncRelayCommand(BevestigenAsync);
        OpenPlanningCommand = new AsyncRelayCommand(OpenPlanningAsync);
        FactuurCommand      = new AsyncRelayCommand(OpenFactuurAsync);
        OpenWerkBonCommand  = new AsyncRelayCommand(OpenWerkBonAsync);
        BestelbonAfdrukkenCommand = new AsyncRelayCommand(BestelbonAfdrukkenAsync);

        ToonStappen(OfferteStatus.Concept, nogTeBetalen: 0m);
    }

    public async Task LoadFactuurContextAsync(AppDbContext db, int offerteId)
    {
        var werkBon = await db.WerkBonnen
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfferteId == offerteId);

        GekoppeldeWerkBon = werkBon;

        var factuur = await db.Facturen
            .Include(x => x.Lijnen)
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OfferteId == offerteId ||
                (werkBon != null && x.WerkBonId == werkBon.Id));

        if (factuur is not null)
            factuur.Lijnen = factuur.Lijnen.OrderBy(x => x.Sortering).ToList();

        GekoppeldeFactuur = factuur;

        // US-83: stap bepalen uit offerte-, werkbon- en bestelbonstatus.
        var offerte = await db.Offertes.AsNoTracking()
            .Where(o => o.Id == offerteId)
            .Select(o => new { o.Status, o.TotaalInclBtw, o.VoorschotBedrag })
            .FirstOrDefaultAsync();

        var nogTeBetalen = factuur is not null
            ? (await _ontvangsten.GetBetaalStandAsync(factuur.Id)).Rest
            : Math.Max(0m, (offerte?.TotaalInclBtw ?? 0m) - (offerte?.VoorschotBedrag ?? 0m));

        ToonStappen(offerte?.Status ?? OfferteStatus.Concept, nogTeBetalen);
    }

    /// <summary>US-83 — werkt de stappenbalk en de knop voor de volgende stap bij.</summary>
    public void ToonStappen(OfferteStatus offerteStatus, decimal nogTeBetalen)
    {
        var info = VerkoopStapResolver.Bepaal(offerteStatus, GekoppeldeWerkBon?.Status, GekoppeldeFactuur?.Status, nogTeBetalen);

        (string? tekst, IAsyncRelayCommand? command) volgende = info switch
        {
            { IsAfgerond: true } => (null, null),
            { Huidig: VerkoopStap.Offerte } => ("Bevestigen als bestelling", BevestigenCommand),
            { Huidig: VerkoopStap.Bestelling or VerkoopStap.InProductie } => ("Werkbon openen", OpenWerkBonCommand),
            { Huidig: VerkoopStap.KlaarOmAfTeHalen } when GekoppeldeFactuur is not null => ("Bestelbon afdrukken", BestelbonAfdrukkenCommand),
            { Huidig: VerkoopStap.KlaarOmAfTeHalen } => ("Werkbon openen", OpenWerkBonCommand),
            { IsAfgehaaldNogTeBetalen: true } => ("Afrekenen", OpenWerkBonCommand),
            _ => (null, null)
        };

        // De klant krijgt de bestelbon ook als prijsvoorstel: vanaf stap 1 aan te maken en af te drukken.
        (string? tekst, IAsyncRelayCommand? command) bestelbon =
            GekoppeldeFactuur is null ? ("Bestelbon aanmaken", FactuurCommand)
            : volgende.command == BestelbonAfdrukkenCommand ? (null, null)
            : ("Bestelbon afdrukken", BestelbonAfdrukkenCommand);

        StappenBalk.Toon(info, volgende.tekst, volgende.command, bestelbon.tekst, bestelbon.command);
    }

    /// <summary>US-83 — opent het werkbonvenster (inplannen, status, afrekenen bij afhalen).</summary>
    private async Task OpenWerkBonAsync()
    {
        if (IsBusy) return;

        var werkBon = GekoppeldeWerkBon;
        if (werkBon is null)
        {
            Toast.Error("Geen werkbon gevonden. Bevestig de offerte eerst.");
            return;
        }

        var owner = GetOwnerWindow();
        if (owner is null) return;

        try
        {
            IsBusy = true;

            var vm = new WerkBonDetailViewModel(_dbFactory, _statusWorkflow, _offerteNav, Toast, werkBon.Id, _ontvangsten);
            await vm.LoadAsync();

            var window = new WerkBonDetailWindow { DataContext = vm };
            await window.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Werkbon openen mislukt", ex.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }

        // Status of betalingen kunnen in het venster gewijzigd zijn.
        if (_reloadOnly is not null) await _reloadOnly();
    }

    /// <summary>US-83/US-82 — bestelbon afdrukken vanuit de stappenbalk, in elke status.</summary>
    private async Task BestelbonAfdrukkenAsync()
    {
        if (IsBusy) return;

        var factuur = GekoppeldeFactuur;
        if (factuur is null)
        {
            Toast.Error("Er is nog geen bestelbon voor deze offerte.");
            return;
        }

        try
        {
            IsBusy = true;

            var result = await _factuurExportService.AfdrukkenAsync(factuur.Id);
            if (!result.Success)
            {
                Toast.Error(result.Message);
                return;
            }

            _pathOpener.OpenFile(result.BestandPad);
        }
        catch (Exception ex)
        {
            Toast.Error($"Afdrukken mislukt: {ex.GetBaseException().Message}");
        }
        finally
        {
            IsBusy = false;
        }

        if (_reloadOnly is not null) await _reloadOnly();
    }

    private async Task BevestigenAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            if (_getOfferteId() == 0)
                await _saveAndReload();

            var offerteId = _getOfferteId();
            if (offerteId == 0)
            {
                Toast.Error("Offerte kon niet opgeslagen worden. Bevestigen is gestopt.");
                return;
            }

            await _workflow.BevestigAsync(offerteId);

            await _saveAndReload();

            await using var db = await _dbFactory.CreateDbContextAsync();
            var werkBonId = await db.WerkBonnen
                .Where(w => w.OfferteId == offerteId)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();

            if (werkBonId == 0)
            {
                Toast.Error("Bevestiging gelukt, maar werkbon werd niet gevonden. Check workflow.");
                return;
            }

            var vm = new PlanningCalendarViewModel(_dbFactory, _werkBonWorkflow, Toast, _statusWorkflow);
            await vm.InitializeAsync(werkBonId);

            var window = new PlanningCalendarWindow { DataContext = vm };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null) return;
                await window.ShowDialog(owner);

                // Herlaad de offerte zodat de via planning gezette afhaaldatum zichtbaar wordt.
                if (_reloadOnly is not null) await _reloadOnly();
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Bevestigen mislukt", ex.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opent de planningskalender voor de bestaande werkbon —
    /// zonder opnieuw te bevestigen of een nieuwe werkbon aan te maken.
    /// </summary>
    private async Task OpenPlanningAsync()
    {
        if (IsBusy) return;

        var werkBon = GekoppeldeWerkBon;
        if (werkBon is null)
        {
            Toast.Error("Geen werkbon gevonden. Bevestig de offerte eerst.");
            return;
        }

        try
        {
            IsBusy = true;

            var vm = new PlanningCalendarViewModel(_dbFactory, _werkBonWorkflow, Toast, _statusWorkflow);
            await vm.InitializeAsync(werkBon.Id);

            var window = new PlanningCalendarWindow { DataContext = vm };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null) return;
                await window.ShowDialog(owner);

                // Herlaad de offerte zodat de via planning gezette afhaaldatum zichtbaar wordt.
                if (_reloadOnly is not null) await _reloadOnly();
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Planning openen mislukt", ex.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenFactuurAsync()
    {
        if (IsBusy) return;

        // De klant krijgt de bestelbon als prijsvoorstel: een nieuwe offerte wordt eerst opgeslagen
        // (en krijgt zo haar nummer), net zoals vroeger bij de offerte-PDF (US-81).
        if (_getOfferteId() == 0)
            await _saveAndReload();

        if (_getOfferteId() == 0)
            return; // opslaan lukte niet (bv. geen klant); de melding is al getoond

        var pricingOk = await _runValidation(true);
        if (!pricingOk) return;

        await _berekenAsync();
        await _saveAndReload();

        if (_getOfferteId() == 0)
        {
            Toast.Error("Sla de offerte eerst op voordat je een bestelbon opent.");
            return;
        }

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();
            await LoadFactuurContextAsync(db, _getOfferteId());

            var wasNieuw = GekoppeldeFactuur is null;
            var factuur = GekoppeldeFactuur;
            if (factuur is null || factuur.Status == FactuurStatus.Draft)
                factuur = await _factuurWorkflow.MaakFactuurVanOfferteAsync(_getOfferteId());
            else
                factuur = await _factuurWorkflow.GetFactuurAsync(factuur.Id) ?? factuur;

            // Toon de info-dialog alleen bij aanmaak van een gloednieuwe factuur.
            // Bij heropenen van een bestaande Draft direct naar de preview — geen extra klik nodig.
            if (wasNieuw && factuur.Status == FactuurStatus.Draft)
            {
                var bijgewerkteFactuur = await ShowFactuurInfoDialogAsync(factuur);
                if (bijgewerkteFactuur is null) return;

                await _factuurWorkflow.SaveDraftAsync(bijgewerkteFactuur);
                factuur = await _factuurWorkflow.GetFactuurAsync(bijgewerkteFactuur.Id) ?? bijgewerkteFactuur;
            }

            GekoppeldeFactuur = factuur;
            await ShowFactuurPreviewAsync(factuur.Id);

            await using var refreshDb = await _dbFactory.CreateDbContextAsync();
            await LoadFactuurContextAsync(refreshDb, _getOfferteId());
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Bestelbon openen mislukt", ex.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<Factuur?> ShowFactuurInfoDialogAsync(Factuur factuur)
    {
        var owner = GetOwnerWindow();
        if (owner is null) throw new InvalidOperationException("Hoofdvenster niet gevonden.");

        var vm = new FactuurInfoDialogViewModel(CloneFactuur(factuur));
        var dialog = new FactuurInfoDialog { DataContext = vm };
        vm.RequestClose = confirmed => dialog.Close(confirmed);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        return confirmed ? vm.ToFactuur() : null;
    }

    private async Task ShowFactuurPreviewAsync(int factuurId)
    {
        var owner = GetOwnerWindow();
        if (owner is null) throw new InvalidOperationException("Hoofdvenster niet gevonden.");

        var factuur = await _factuurWorkflow.GetFactuurAsync(factuurId)
            ?? throw new InvalidOperationException("Bestelbon niet gevonden.");

        var vm = new FactuurPreviewViewModel(
            factuur.Id, factuur, _factuurWorkflow,
            _factuurExportService, _filePickerService, Toast);

        await vm.InitializeAsync();

        var window = new FactuurPreviewWindow { DataContext = vm };
        vm.RequestClose = () => window.Close();
        await window.ShowDialog(owner);
    }

    private static Avalonia.Controls.Window? GetOwnerWindow()
    {
        if (App.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        return desktop.MainWindow;
    }

    private static Factuur CloneFactuur(Factuur factuur) => new()
    {
        Id = factuur.Id, OfferteId = factuur.OfferteId, WerkBonId = factuur.WerkBonId,
        Jaar = factuur.Jaar, VolgNr = factuur.VolgNr, FactuurNummer = factuur.FactuurNummer,
        DocumentType = factuur.DocumentType, KlantNaam = factuur.KlantNaam,
        KlantAdres = factuur.KlantAdres, KlantBtwNummer = factuur.KlantBtwNummer,
        FactuurDatum = factuur.FactuurDatum, VervalDatum = factuur.VervalDatum,
        GeplandeDatum = factuur.GeplandeDatum, AfhaalDatum = factuur.AfhaalDatum,
        Opmerking = factuur.Opmerking, AangenomenDoorInitialen = factuur.AangenomenDoorInitialen,
        IsBtwVrijgesteld = factuur.IsBtwVrijgesteld, TotaalExclBtw = factuur.TotaalExclBtw,
        TotaalBtw = factuur.TotaalBtw, TotaalInclBtw = factuur.TotaalInclBtw,
        VoorschotBedrag = factuur.VoorschotBedrag, ExportPad = factuur.ExportPad,
        Status = factuur.Status, AangemaaktOp = factuur.AangemaaktOp,
        BijgewerktOp = factuur.BijgewerktOp, RowVersion = factuur.RowVersion,
        Lijnen = factuur.Lijnen
            .OrderBy(x => x.Sortering)
            .Select(x => new FactuurLijn
            {
                Id = x.Id, FactuurId = x.FactuurId, Omschrijving = x.Omschrijving,
                Aantal = x.Aantal, Eenheid = x.Eenheid, PrijsExcl = x.PrijsExcl,
                BtwPct = x.BtwPct, TotaalExcl = x.TotaalExcl, TotaalBtw = x.TotaalBtw,
                TotaalIncl = x.TotaalIncl, Sortering = x.Sortering
            })
            .ToList()
    };
}
