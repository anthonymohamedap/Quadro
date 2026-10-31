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
    private readonly IBestelbonWorkflowService _bestelbonWorkflow;
    private readonly IBestelbonExportService _bestelbonExportService;
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
    [ObservableProperty] private Bestelbon? gekoppeldeBestelbon;
    [ObservableProperty] private bool isBusy;

    public string BestelbonButtonText =>
        GekoppeldeBestelbon is null ? "Bestelbon aanmaken" : "Bestelbon bekijken";

    public string BestelbonStatusText
    {
        get
        {
            if (GekoppeldeBestelbon is not null)
                return $"Bestelbon {GekoppeldeBestelbon.NummerLabel} ({GekoppeldeBestelbon.Status.Label()})";

            if (GekoppeldeWerkBon is null)
                return "Nog geen bestelbon voor deze offerte.";

            return $"Nog geen bestelbon. Werkbon: {GekoppeldeWerkBon.Status.Label()}.";
        }
    }

    partial void OnGekoppeldeWerkBonChanged(WerkBon? value)
    {
        OnPropertyChanged(nameof(BestelbonStatusText));
        OnPropertyChanged(nameof(IsBevestigenZichtbaar));
        OnPropertyChanged(nameof(IsPlanningZichtbaar));
    }

    partial void OnGekoppeldeBestelbonChanged(Bestelbon? value)
    {
        OnPropertyChanged(nameof(BestelbonButtonText));
        OnPropertyChanged(nameof(BestelbonStatusText));
    }

    // Bevestigen is alleen zichtbaar als er nog geen werkbon is.
    // Zodra de werkbon bestaat → Planning-knop tonen i.p.v. Bevestigen-knop.
    public bool IsBevestigenZichtbaar => GekoppeldeWerkBon is null;
    public bool IsPlanningZichtbaar   => GekoppeldeWerkBon is not null;

    public IAsyncRelayCommand BevestigenCommand { get; }
    public IAsyncRelayCommand OpenPlanningCommand { get; }
    public IAsyncRelayCommand BestelbonCommand { get; }
    public IAsyncRelayCommand OpenWerkBonCommand { get; }
    public IAsyncRelayCommand BestelbonAfdrukkenCommand { get; }

    /// <summary>US-83 — stappenbalk bovenaan de offerte.</summary>
    public StappenBalkViewModel StappenBalk { get; } = new();

    /// <summary>US-100 — de klant heeft bevestigd (stap 2 of verder): de prijs staat vast en wordt
    /// niet meer automatisch herberekend. Vóór de bevestiging rekent de offerte gewoon live mee.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVergrendeld))]
    private bool isPrijsVast;

    /// <summary>US-101 — afgehaald én volledig betaald.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVergrendeld))]
    private bool isAfgerond;

    /// <summary>US-101 — door een admin tijdelijk ontgrendeld (enkel zolang de offerte open staat).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVergrendeld))]
    private bool isOntgrendeld;

    /// <summary>US-101 — afgeronde verkoop: alleen-lezen, geen herberekening en niet opslaan.</summary>
    public bool IsVergrendeld => IsAfgerond && !IsOntgrendeld;

    public OfferteWorkflowViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IOfferteWorkflowService workflow,
        IWerkBonWorkflowService werkBonWorkflow,
        IWorkflowService statusWorkflow,
        IBestelbonWorkflowService bestelbonWorkflow,
        IBestelbonExportService bestelbonExportService,
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
        _bestelbonWorkflow = bestelbonWorkflow ?? throw new ArgumentNullException(nameof(bestelbonWorkflow));
        _bestelbonExportService = bestelbonExportService ?? throw new ArgumentNullException(nameof(bestelbonExportService));
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
        BestelbonCommand      = new AsyncRelayCommand(OpenBestelbonAsync);
        OpenWerkBonCommand  = new AsyncRelayCommand(OpenWerkBonAsync);
        BestelbonAfdrukkenCommand = new AsyncRelayCommand(BestelbonAfdrukkenAsync);

        ToonStappen(OfferteStatus.Concept, nogTeBetalen: 0m);
    }

    public async Task LoadBestelbonContextAsync(AppDbContext db, int offerteId)
    {
        var werkBon = await db.WerkBonnen
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfferteId == offerteId);

        GekoppeldeWerkBon = werkBon;

        var bestelbon = await db.Bestelbonnen
            .Include(x => x.Lijnen)
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.OfferteId == offerteId ||
                (werkBon != null && x.WerkBonId == werkBon.Id));

        if (bestelbon is not null)
            bestelbon.Lijnen = bestelbon.Lijnen.OrderBy(x => x.Sortering).ToList();

        GekoppeldeBestelbon = bestelbon;

        // US-83: stap bepalen uit offerte-, werkbon- en bestelbonstatus.
        var offerte = await db.Offertes.AsNoTracking()
            .Where(o => o.Id == offerteId)
            .Select(o => new { o.Status, o.TotaalInclBtw, o.VoorschotBedrag })
            .FirstOrDefaultAsync();

        var nogTeBetalen = bestelbon is not null
            ? (await _ontvangsten.GetBetaalStandAsync(bestelbon.Id)).Rest
            : Math.Max(0m, (offerte?.TotaalInclBtw ?? 0m) - (offerte?.VoorschotBedrag ?? 0m));

        ToonStappen(offerte?.Status ?? OfferteStatus.Concept, nogTeBetalen);
    }

    /// <summary>US-83 — werkt de stappenbalk en de knop voor de volgende stap bij.</summary>
    public void ToonStappen(OfferteStatus offerteStatus, decimal nogTeBetalen)
    {
        var info = VerkoopStapResolver.Bepaal(offerteStatus, GekoppeldeWerkBon?.Status, GekoppeldeBestelbon?.Status, nogTeBetalen);
        IsPrijsVast = IsPrijsVastNaBevestiging(info);
        IsAfgerond = info.IsAfgerond;

        (string? tekst, IAsyncRelayCommand? command) volgende = info switch
        {
            { IsAfgerond: true } => (null, null),
            { Huidig: VerkoopStap.Offerte } => ("Bevestigen → Werkbon", BevestigenCommand),
            { Huidig: VerkoopStap.Werkbon or VerkoopStap.InProductie } => ("Werkbon openen", OpenWerkBonCommand),
            { Huidig: VerkoopStap.KlaarOmAfTeHalen } when GekoppeldeBestelbon is not null => ("Bestelbon afdrukken", BestelbonAfdrukkenCommand),
            { Huidig: VerkoopStap.KlaarOmAfTeHalen } => ("Werkbon openen", OpenWerkBonCommand),
            { IsAfgehaaldNogTeBetalen: true } => ("Afrekenen", OpenWerkBonCommand),
            _ => (null, null)
        };

        // De klant krijgt de bestelbon ook als prijsvoorstel: vanaf stap 1 aan te maken en af te drukken.
        (string? tekst, IAsyncRelayCommand? command) bestelbon =
            GekoppeldeBestelbon is null ? ("Bestelbon aanmaken", BestelbonCommand)
            : volgende.command == BestelbonAfdrukkenCommand ? (null, null)
            : ("Bestelbon afdrukken", BestelbonAfdrukkenCommand);

        StappenBalk.Toon(info, volgende.tekst, volgende.command, bestelbon.tekst, bestelbon.command);
    }

    /// <summary>US-100 — vanaf "Bevestigen → Werkbon" (stap 2 en verder) staat de prijs vast.</summary>
    public static bool IsPrijsVastNaBevestiging(VerkoopStapInfo info)
        => !info.IsGeannuleerd && (info.IsAfgerond || info.Huidig >= VerkoopStap.Werkbon);

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

        var bestelbon = GekoppeldeBestelbon;
        if (bestelbon is null)
        {
            Toast.Error("Er is nog geen bestelbon voor deze offerte.");
            return;
        }

        try
        {
            IsBusy = true;

            var result = await _bestelbonExportService.AfdrukkenAsync(bestelbon.Id);
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

    private async Task OpenBestelbonAsync()
    {
        if (IsBusy) return;

        // De klant krijgt de bestelbon als prijsvoorstel: een nieuwe offerte wordt eerst opgeslagen
        // (en krijgt zo haar nummer), net zoals vroeger bij de offerte-PDF (US-81).
        if (_getOfferteId() == 0)
            await _saveAndReload();

        if (_getOfferteId() == 0)
            return; // opslaan lukte niet (bv. geen klant); de melding is al getoond

        // US-100/101: na de bevestiging wordt niet meer herberekend (BerekenSilentAsync doet dan
        // niets); bij een afgeronde, vergrendelde verkoop wordt ook niet gevalideerd of opgeslagen.
        if (!IsVergrendeld)
        {
            var pricingOk = await _runValidation(true);
            if (!pricingOk) return;

            await _berekenAsync();
            await _saveAndReload();
        }

        if (_getOfferteId() == 0)
        {
            Toast.Error("Sla de offerte eerst op voordat je een bestelbon opent.");
            return;
        }

        try
        {
            IsBusy = true;

            await using var db = await _dbFactory.CreateDbContextAsync();
            await LoadBestelbonContextAsync(db, _getOfferteId());

            var wasNieuw = GekoppeldeBestelbon is null;
            var bestelbon = GekoppeldeBestelbon;
            if (bestelbon is null || bestelbon.Status == BestelbonStatus.Draft)
                bestelbon = await _bestelbonWorkflow.MaakBestelbonVanOfferteAsync(_getOfferteId());
            else
                bestelbon = await _bestelbonWorkflow.GetBestelbonAsync(bestelbon.Id) ?? bestelbon;

            // Toon de info-dialog alleen bij aanmaak van een gloednieuwe bestelbon.
            // Bij heropenen van een bestaande Draft direct naar de preview — geen extra klik nodig.
            if (wasNieuw && bestelbon.Status == BestelbonStatus.Draft)
            {
                var bijgewerkteBestelbon = await ShowBestelbonGegevensDialogAsync(bestelbon);
                if (bijgewerkteBestelbon is null) return;

                await _bestelbonWorkflow.SaveDraftAsync(bijgewerkteBestelbon);
                bestelbon = await _bestelbonWorkflow.GetBestelbonAsync(bijgewerkteBestelbon.Id) ?? bijgewerkteBestelbon;
            }

            GekoppeldeBestelbon = bestelbon;
            await ShowBestelbonPreviewAsync(bestelbon.Id);

            await using var refreshDb = await _dbFactory.CreateDbContextAsync();
            await LoadBestelbonContextAsync(refreshDb, _getOfferteId());
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

    private async Task<Bestelbon?> ShowBestelbonGegevensDialogAsync(Bestelbon bestelbon)
    {
        var owner = GetOwnerWindow();
        if (owner is null) throw new InvalidOperationException("Hoofdvenster niet gevonden.");

        var vm = new BestelbonGegevensDialogViewModel(CloneBestelbon(bestelbon));
        var dialog = new BestelbonGegevensDialog { DataContext = vm };
        vm.RequestClose = confirmed => dialog.Close(confirmed);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        return confirmed ? vm.ToBestelbon() : null;
    }

    private async Task ShowBestelbonPreviewAsync(int bestelbonId)
    {
        var owner = GetOwnerWindow();
        if (owner is null) throw new InvalidOperationException("Hoofdvenster niet gevonden.");

        var bestelbon = await _bestelbonWorkflow.GetBestelbonAsync(bestelbonId)
            ?? throw new InvalidOperationException("Bestelbon niet gevonden.");

        var vm = new BestelbonPreviewViewModel(
            bestelbon.Id, bestelbon, _bestelbonWorkflow,
            _bestelbonExportService, _filePickerService, Toast);

        await vm.InitializeAsync();

        var window = new BestelbonPreviewWindow { DataContext = vm };
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

    private static Bestelbon CloneBestelbon(Bestelbon bestelbon) => new()
    {
        Id = bestelbon.Id, OfferteId = bestelbon.OfferteId, WerkBonId = bestelbon.WerkBonId,
        Jaar = bestelbon.Jaar, VolgNr = bestelbon.VolgNr, BestelbonNummer = bestelbon.BestelbonNummer,
        DocumentType = bestelbon.DocumentType, KlantNaam = bestelbon.KlantNaam,
        KlantAdres = bestelbon.KlantAdres, KlantBtwNummer = bestelbon.KlantBtwNummer,
        BestelbonDatum = bestelbon.BestelbonDatum, VervalDatum = bestelbon.VervalDatum,
        GeplandeDatum = bestelbon.GeplandeDatum, AfhaalDatum = bestelbon.AfhaalDatum,
        Opmerking = bestelbon.Opmerking, AangenomenDoorInitialen = bestelbon.AangenomenDoorInitialen,
        IsBtwVrijgesteld = bestelbon.IsBtwVrijgesteld, TotaalExclBtw = bestelbon.TotaalExclBtw,
        TotaalBtw = bestelbon.TotaalBtw, TotaalInclBtw = bestelbon.TotaalInclBtw,
        VoorschotBedrag = bestelbon.VoorschotBedrag, ExportPad = bestelbon.ExportPad,
        Status = bestelbon.Status, AangemaaktOp = bestelbon.AangemaaktOp,
        BijgewerktOp = bestelbon.BijgewerktOp, RowVersion = bestelbon.RowVersion,
        Lijnen = bestelbon.Lijnen
            .OrderBy(x => x.Sortering)
            .Select(x => new BestelbonLijn
            {
                Id = x.Id, BestelbonId = x.BestelbonId, Omschrijving = x.Omschrijving,
                Aantal = x.Aantal, Eenheid = x.Eenheid, PrijsExcl = x.PrijsExcl,
                BtwPct = x.BtwPct, TotaalExcl = x.TotaalExcl, TotaalBtw = x.TotaalBtw,
                TotaalIncl = x.TotaalIncl, Sortering = x.Sortering
            })
            .ToList()
    };
}
