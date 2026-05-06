using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OfferteViewModel : AsyncViewModelBase, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IOfferteValidator _validator;

    // ── Root aggregate state ──
    [ObservableProperty] private Offerte? offerte;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    private bool _suppressRecalc;

    // ── Sub-ViewModels ──
    public KlantSelectieViewModel KlantSelectie { get; }
    public OfferteRegelViewModel Regelbeheer { get; }
    public OffertePrijsViewModel Prijzen { get; }
    public OfferteWorkflowViewModel Workflow { get; }

    // ── Forwarding properties: AXAML hoeft NIET te wijzigen ──

    // Totalen (footer)
    public decimal OfferteEx    => Offerte?.SubtotaalExBtw ?? 0m;
    public decimal OfferteBtw   => Offerte?.BtwBedrag ?? 0m;
    public decimal OfferteIncl  => Offerte?.TotaalInclBtw ?? 0m;

    // Workflow
    public string FactuurButtonText  => Workflow.FactuurButtonText;
    public string FactuurStatusText  => Workflow.FactuurStatusText;

    // Commands (alle geforward vanuit sub-VMs)
    public IAsyncRelayCommand BerekenCommand     => Prijzen.BerekenCommand;
    public IAsyncRelayCommand BevestigenCommand  => Workflow.BevestigenCommand;
    public IAsyncRelayCommand FactuurCommand     => Workflow.FactuurCommand;
    public IAsyncRelayCommand NieuweKlantCommand => KlantSelectie.NieuweKlantCommand;
    public IRelayCommand  RegelDuplicerenCommand    => Regelbeheer.RegelDuplicerenCommand;
    public IAsyncRelayCommand ApplyLegacyCodeCommand => Regelbeheer.ApplyLegacyCodeCommand;
    public IRelayCommand  GenerateLegacyCodeCommand  => Regelbeheer.GenerateLegacyCodeCommand;
    public IAsyncRelayCommand OpenTypeLijstCommand   => Regelbeheer.OpenTypeLijstCommand;

    // KlantSelectie doorsturen
    public string? KlantZoekterm
    {
        get => KlantSelectie.KlantZoekterm;
        set => KlantSelectie.KlantZoekterm = value;
    }
    public System.Collections.ObjectModel.ObservableCollection<Klant> GefilterdeKlanten
        => KlantSelectie.GefilterdeKlanten;
    public Klant? SelectedKlant
    {
        get => KlantSelectie.SelectedKlant;
        set => KlantSelectie.SelectedKlant = value;
    }

    // Regelbeheer doorsturen
    public System.Collections.ObjectModel.ObservableCollection<OfferteRegel> Regels
        => Regelbeheer.Regels;
    public OfferteRegel? SelectedRegel
    {
        get => Regelbeheer.SelectedRegel;
        set => Regelbeheer.SelectedRegel = value;
    }
    public string? TypeLijstZoekterm
    {
        get => Regelbeheer.TypeLijstZoekterm;
        set => Regelbeheer.TypeLijstZoekterm = value;
    }
    public System.Collections.ObjectModel.ObservableCollection<TypeLijst> GefilterdeTypeLijsten
        => Regelbeheer.GefilterdeTypeLijsten;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> GlasOpties
        => Regelbeheer.GlasOpties;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> Passe1Opties
        => Regelbeheer.Passe1Opties;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> Passe2Opties
        => Regelbeheer.Passe2Opties;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> DiepteOpties
        => Regelbeheer.DiepteOpties;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> OpkleefOpties
        => Regelbeheer.OpkleefOpties;
    public System.Collections.ObjectModel.ObservableCollection<AfwerkingsOptie> RugOpties
        => Regelbeheer.RugOpties;
    public string? LegacyCode
    {
        get => Regelbeheer.LegacyCode;
        set => Regelbeheer.LegacyCode = value;
    }

    // ── TypeLijst selectie: zelfde patroon als SelectedKlant in KlantSelectieViewModel.
    //    SelectedTypeLijst is een echte [ObservableProperty] op Regelbeheer, dus Avalonia
    //    kan het betrouwbaar tracken zonder multi-segment path binding issues. ──
    public TypeLijst? SelectedRegelTypeLijst
    {
        get => Regelbeheer.SelectedTypeLijst;
        set => Regelbeheer.SelectedTypeLijst = value;
    }

    // ── Overige regel-navigatie: dedicated single-segment properties voor afwerkingen. ──
    public AfwerkingsOptie? SelectedRegelGlas
    {
        get => Regelbeheer.SelectedRegel?.Glas;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.Glas = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelPasse1
    {
        get => Regelbeheer.SelectedRegel?.PassePartout1;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.PassePartout1 = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelPasse2
    {
        get => Regelbeheer.SelectedRegel?.PassePartout2;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.PassePartout2 = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelDiepte
    {
        get => Regelbeheer.SelectedRegel?.DiepteKern;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.DiepteKern = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelOpkleven
    {
        get => Regelbeheer.SelectedRegel?.Opkleven;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.Opkleven = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelRug
    {
        get => Regelbeheer.SelectedRegel?.Rug;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.Rug = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }

    public OfferteViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        IPricingService pricing,
        IOfferteWorkflowService workflow,
        IWerkBonWorkflowService werkBonWorkflow,
        IWorkflowService statusWorkflow,
        IFactuurWorkflowService factuurWorkflow,
        IFactuurExportService factuurExportService,
        IFilePickerService filePickerService,
        IOfferteValidator validator,
        IToastService toast,
        ICrudValidator<Klant> crudValidator,
        IKlantDialogService klantDialog)
        : base(toast)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

        KlantSelectie = new KlantSelectieViewModel(dbFactory, crudValidator, klantDialog, dialogs, toast);
        KlantSelectie.KlantSelected += k =>
        {
            if (Offerte is not null) Offerte.KlantId = k?.Id;
        };

        Regelbeheer = new OfferteRegelViewModel(dbFactory, dialogs, toast);
        Regelbeheer.RegelChanged += () =>
        {
            if (!_suppressRecalc) Prijzen?.TriggerRecalc();
        };

        Prijzen = new OffertePrijsViewModel(
            pricing, dialogs, toast,
            runFullValidation: showFeedback => RunValidationOrToastAsync(
                o => validator.ValidateForPricingAsync(o), showFeedback),
            refreshLijstPrijzen: RefreshLijstPrijzenAsync,
            buildSnapshot: BuildSnapshotForPricing,
            applySnapshot: ApplyPricingSnapshot);

        Workflow = new OfferteWorkflowViewModel(
            dbFactory, workflow, werkBonWorkflow, statusWorkflow,
            factuurWorkflow, factuurExportService, filePickerService,
            dialogs, toast,
            getOfferteId: () => Offerte?.Id ?? 0,
            saveAndReload: () => SaveCoreAsync(reloadAfterSave: true),
            berekenAsync: () => BerekenSilentAsync(),
            runValidation: showFeedback => RunValidationOrToastAsync(
                o => validator.ValidateForPricingAsync(o), showFeedback));

        // Propageer PropertyChanged van sub-VMs naar root zodat AXAML bindings updaten
        KlantSelectie.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(KlantSelectie.SelectedKlant):       OnPropertyChanged(nameof(SelectedKlant)); break;
                case nameof(KlantSelectie.KlantZoekterm):       OnPropertyChanged(nameof(KlantZoekterm)); break;
                case nameof(KlantSelectie.GefilterdeKlanten):   OnPropertyChanged(nameof(GefilterdeKlanten)); break;
            }
        };

        Regelbeheer.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(Regelbeheer.SelectedRegel):
                    OnPropertyChanged(nameof(SelectedRegel));
                    OnPropertyChanged(nameof(LegacyCode));
                    // SelectedRegelTypeLijst is now forwarded via Regelbeheer.SelectedTypeLijst
                    // (a real [ObservableProperty]), so no manual notification needed here.
                    OnPropertyChanged(nameof(SelectedRegelGlas));
                    OnPropertyChanged(nameof(SelectedRegelPasse1));
                    OnPropertyChanged(nameof(SelectedRegelPasse2));
                    OnPropertyChanged(nameof(SelectedRegelDiepte));
                    OnPropertyChanged(nameof(SelectedRegelOpkleven));
                    OnPropertyChanged(nameof(SelectedRegelRug));
                    break;
                case nameof(Regelbeheer.SelectedTypeLijst):   OnPropertyChanged(nameof(SelectedRegelTypeLijst)); break;
                case nameof(Regelbeheer.Regels):              OnPropertyChanged(nameof(Regels)); break;
                case nameof(Regelbeheer.TypeLijstZoekterm):   OnPropertyChanged(nameof(TypeLijstZoekterm)); break;
                case nameof(Regelbeheer.GefilterdeTypeLijsten): OnPropertyChanged(nameof(GefilterdeTypeLijsten)); break;
                case nameof(Regelbeheer.GlasOpties):          OnPropertyChanged(nameof(GlasOpties)); break;
                case nameof(Regelbeheer.Passe1Opties):        OnPropertyChanged(nameof(Passe1Opties)); break;
                case nameof(Regelbeheer.Passe2Opties):        OnPropertyChanged(nameof(Passe2Opties)); break;
                case nameof(Regelbeheer.DiepteOpties):        OnPropertyChanged(nameof(DiepteOpties)); break;
                case nameof(Regelbeheer.OpkleefOpties):       OnPropertyChanged(nameof(OpkleefOpties)); break;
                case nameof(Regelbeheer.RugOpties):           OnPropertyChanged(nameof(RugOpties)); break;
                case nameof(Regelbeheer.LegacyCode):          OnPropertyChanged(nameof(LegacyCode)); break;
            }
        };

        Workflow.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Workflow.FactuurButtonText) or nameof(Workflow.FactuurStatusText))
                OnPropertyChanged(e.PropertyName);
        };
    }

    public async Task InitializeAsync() => await LoadCatalogAsync();

    // ── Catalog ──
    public async Task LoadCatalogAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var lijsten = await db.TypeLijsten.AsNoTracking()
            .OrderBy(t => t.Artikelnummer)
            .ToListAsync();

        Regelbeheer.TypeLijsten.Clear();
        foreach (var l in lijsten)
            Regelbeheer.TypeLijsten.Add(l);

        // Sync GefilterdeTypeLijsten via diff (nooit Clear/replace) zodat de ComboBox
        // zijn SelectedItem niet verliest als de catalog wordt herladen.
        Regelbeheer.ApplyTypeLijstFilter(Regelbeheer.TypeLijstZoekterm);

        var klanten = await db.Klanten.AsNoTracking()
            .OrderBy(k => k.Achternaam).ThenBy(k => k.Voornaam)
            .ToListAsync();

        Regelbeheer.GlasOpties   = await LoadOptiesAsync(db, 'G');
        Regelbeheer.Passe1Opties = await LoadOptiesAsync(db, 'P');
        Regelbeheer.Passe2Opties = await LoadOptiesAsync(db, 'P');
        Regelbeheer.DiepteOpties = await LoadOptiesAsync(db, 'D');
        Regelbeheer.OpkleefOpties = await LoadOptiesAsync(db, 'O');
        Regelbeheer.RugOpties    = await LoadOptiesAsync(db, 'R');

        KlantSelectie.SetKlanten(klanten, Offerte?.KlantId);

        RelinkSelectionsAfterCatalog();
    }

    private static async Task<ObservableCollection<AfwerkingsOptie>> LoadOptiesAsync(AppDbContext db, char code)
    {
        var groepId = await db.AfwerkingsGroepen
            .Where(g => g.Code == code)
            .Select(g => g.Id)
            .FirstAsync();

        var list = await db.AfwerkingsOpties.AsNoTracking()
            .Where(a => a.AfwerkingsGroepId == groepId)
            .OrderBy(a => a.Volgnummer).ThenBy(a => a.Kleur).ThenBy(a => a.Naam)
            .ToListAsync();

        return new ObservableCollection<AfwerkingsOptie>(list);
    }

    private void RelinkSelectionsAfterCatalog()
    {
        foreach (var regel in Regelbeheer.Regels)
        {
            if (regel.TypeLijstId is int tid)
                regel.TypeLijst = Regelbeheer.TypeLijsten.FirstOrDefault(t => t.Id == tid);
            if (regel.GlasId is int gid)
                regel.Glas = Regelbeheer.GlasOpties.FirstOrDefault(g => g.Id == gid);
            if (regel.PassePartout1Id is int p1id)
                regel.PassePartout1 = Regelbeheer.Passe1Opties.FirstOrDefault(p => p.Id == p1id);
            if (regel.PassePartout2Id is int p2id)
                regel.PassePartout2 = Regelbeheer.Passe2Opties.FirstOrDefault(p => p.Id == p2id);
            if (regel.DiepteKernId is int did)
                regel.DiepteKern = Regelbeheer.DiepteOpties.FirstOrDefault(d => d.Id == did);
            if (regel.OpklevenId is int oid)
                regel.Opkleven = Regelbeheer.OpkleefOpties.FirstOrDefault(o => o.Id == oid);
            if (regel.RugId is int rid)
                regel.Rug = Regelbeheer.RugOpties.FirstOrDefault(r => r.Id == rid);
        }

        // Na het relinken van catalog-referenties, sync SelectedTypeLijst zodat
        // de ComboBox de nieuwe catalog-instantie toont.
        Regelbeheer.SyncTypeLijstFromSelectedRegel();
    }

    // ── Load offerte ──
    public async Task LoadAsync(int offerteId) => await LoadAsync(offerteId, reloadCatalog: true);

    private async Task LoadAsync(int offerteId, bool reloadCatalog)
    {
        _suppressRecalc = true;
        try
        {
            // ── Null SelectedRegel FIRST ──────────────────────────────────────────────
            // When LoadCatalogAsync replaces ItemsSource collections (GlasOpties, etc.),
            // Avalonia's ComboBoxes lose their SelectedItem and write null back via the
            // TwoWay binding. The SelectedRegelXxx setters guard against this with
            // "if (Regelbeheer.SelectedRegel is null) return;" so they cannot wipe FK IDs
            // — but only when SelectedRegel is already null before the catalog reload.
            Regelbeheer.SelectedRegel = null;

            if (reloadCatalog)
                await LoadCatalogAsync();

            await using var db = await _dbFactory.CreateDbContextAsync();

            var o = await db.Offertes
                .Include(x => x.Regels)
                .AsNoTracking()
                .FirstAsync(x => x.Id == offerteId);

            Offerte = o;
            await Workflow.LoadFactuurContextAsync(db, offerteId);

            // Selecteer de juiste klant uit de al-geladen catalogus.
            // Niet SetKlanten aanroepen met de eigen collectie — dat wist hem eerst (self-clear bug).
            KlantSelectie.SelectedKlant = KlantSelectie.Klanten.FirstOrDefault(k => k.Id == o.KlantId);

            var regels = new ObservableCollection<OfferteRegel>();
            foreach (var dbRule in o.Regels)
            {
                var rule = new OfferteRegel
                {
                    Id = dbRule.Id, OfferteId = dbRule.OfferteId,
                    AantalStuks = dbRule.AantalStuks, BreedteCm = dbRule.BreedteCm,
                    HoogteCm = dbRule.HoogteCm, InlegBreedteCm = dbRule.InlegBreedteCm,
                    InlegHoogteCm = dbRule.InlegHoogteCm, Titel = dbRule.Titel,
                    Opmerking = dbRule.Opmerking, TypeLijstId = dbRule.TypeLijstId,
                    GlasId = dbRule.GlasId, PassePartout1Id = dbRule.PassePartout1Id,
                    PassePartout2Id = dbRule.PassePartout2Id, DiepteKernId = dbRule.DiepteKernId,
                    OpklevenId = dbRule.OpklevenId, RugId = dbRule.RugId,
                    ExtraWerkMinuten = dbRule.ExtraWerkMinuten, ExtraPrijs = dbRule.ExtraPrijs,
                    Korting = dbRule.Korting, LegacyCode = dbRule.LegacyCode,
                    AfgesprokenPrijsExcl = dbRule.AfgesprokenPrijsExcl,
                    TotaalExcl = dbRule.TotaalExcl, SubtotaalExBtw = dbRule.SubtotaalExBtw,
                    BtwBedrag = dbRule.BtwBedrag, TotaalInclBtw = dbRule.TotaalInclBtw
                };

                rule.TypeLijst    = Regelbeheer.TypeLijsten.FirstOrDefault(t => t.Id == rule.TypeLijstId);
                rule.Glas         = Regelbeheer.GlasOpties.FirstOrDefault(g => g.Id == rule.GlasId);
                rule.PassePartout1 = Regelbeheer.Passe1Opties.FirstOrDefault(p => p.Id == rule.PassePartout1Id);
                rule.PassePartout2 = Regelbeheer.Passe2Opties.FirstOrDefault(p => p.Id == rule.PassePartout2Id);
                rule.DiepteKern   = Regelbeheer.DiepteOpties.FirstOrDefault(x => x.Id == rule.DiepteKernId);
                rule.Opkleven     = Regelbeheer.OpkleefOpties.FirstOrDefault(x => x.Id == rule.OpklevenId);
                rule.Rug          = Regelbeheer.RugOpties.FirstOrDefault(x => x.Id == rule.RugId);

                regels.Add(rule);
            }

            Regelbeheer.Regels = regels;
            Regelbeheer.SelectedRegel = null;  // Geen auto-selectie — gebruiker klikt zelf een regel aan.

            RefreshTotals();
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    // ── New ──
    [RelayCommand]
    public Task NewAsync()
    {
        Offerte = new Offerte();
        Regelbeheer.Regels = new ObservableCollection<OfferteRegel>();
        Regelbeheer.SelectedRegel = null;
        Workflow.GekoppeldeWerkBon = null;
        Workflow.GekoppeldeFactuur = null;
        RefreshTotals();
        return Task.CompletedTask;
    }

    // ── Regel CRUD commands (forwarding naar Regelbeheer, zodat AXAML RelayCommand attrs werken) ──
    [RelayCommand]
    private void RegelToevoegen() => Regelbeheer.RegelToevoegen();

    [RelayCommand]
    private void RegelVerwijderen(OfferteRegel? regel) => Regelbeheer.RegelVerwijderen(regel);

    // ── Save ──
    [RelayCommand]
    private async Task SaveAsync() => await SaveCoreAsync(reloadAfterSave: true);

    public async Task SaveCoreAsync(bool reloadAfterSave)
    {
        if (Offerte is null) return;
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await using var db = await _dbFactory.CreateDbContextAsync();

            Offerte.KlantId = KlantSelectie.SelectedKlant?.Id;

            if (Offerte.KlantId is null)
            {
                Toast.Error("Selecteer een klant.");
                return;
            }

            Offerte.Klant = null;
            if (Offerte.Datum == default) Offerte.Datum = DateTime.Today;

            var shouldSaveAtEnd = true;

            if (Offerte.Id == 0)
            {
                db.Offertes.Add(Offerte);
                await db.SaveChangesAsync();

                foreach (var vmRule in Regelbeheer.Regels)
                {
                    var newRule = BuildDbRegel(vmRule, Offerte.Id);
                    db.OfferteRegels.Add(newRule);
                }

                await db.SaveChangesAsync();
                shouldSaveAtEnd = false;
            }
            else
            {
                var offerteStub = new Offerte
                {
                    Id = Offerte.Id, KlantId = Offerte.KlantId, Datum = Offerte.Datum,
                    Opmerking = Offerte.Opmerking, GeplandeDatum = Offerte.GeplandeDatum,
                    DeadlineDatum = Offerte.DeadlineDatum, GeschatteMinuten = Offerte.GeschatteMinuten,
                    Status = Offerte.Status, KortingPct = Offerte.KortingPct,
                    MeerPrijsIncl = Offerte.MeerPrijsIncl, IsVoorschotBetaald = Offerte.IsVoorschotBetaald,
                    VoorschotBedrag = Offerte.VoorschotBedrag, SubtotaalExBtw = Offerte.SubtotaalExBtw,
                    BtwBedrag = Offerte.BtwBedrag, TotaalInclBtw = Offerte.TotaalInclBtw,
                    // RowVersion must be included so EF Core can detect concurrent edits.
                    // If two users save the same version, the second save throws
                    // DbUpdateConcurrencyException and we show a clear Dutch message.
                    RowVersion = Offerte.RowVersion
                };

                db.Offertes.Attach(offerteStub);
                db.Entry(offerteStub).State = EntityState.Modified;

                var existingRules = await db.OfferteRegels
                    .Where(x => x.OfferteId == Offerte.Id)
                    .ToListAsync();

                var currentIds = Regelbeheer.Regels.Where(r => r.Id > 0).Select(r => r.Id).ToHashSet();
                var toDelete = existingRules.Where(x => !currentIds.Contains(x.Id)).ToList();
                if (toDelete.Count > 0)
                    db.OfferteRegels.RemoveRange(toDelete);

                foreach (var vmRule in Regelbeheer.Regels)
                {
                    if (vmRule.Id == 0)
                    {
                        db.OfferteRegels.Add(BuildDbRegel(vmRule, Offerte.Id));
                    }
                    else
                    {
                        var dbRule = existingRules.First(x => x.Id == vmRule.Id);
                        ApplyToDbRegel(dbRule, vmRule);
                    }
                }
            }

            if (shouldSaveAtEnd)
                await db.SaveChangesAsync();

            Toast.Success("Offerte opgeslagen");

            if (reloadAfterSave)
                await LoadAsync(Offerte.Id, reloadCatalog: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another user saved this offerte while you had it open.
            // Reload to get the latest version — their changes are preserved.
            Foutmelding = "Iemand anders heeft deze offerte gewijzigd terwijl je hem open had. " +
                          "De offerte wordt herladen met de laatste versie.";
            await _dialogs.ShowErrorAsync("Opslaan mislukt — gelijktijdige wijziging", Foutmelding);
            await LoadAsync(Offerte!.Id, reloadCatalog: false);
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            await _dialogs.ShowErrorAsync("Opslaan mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Pricing helpers (blijven hier want ze werken op root state) ──
    private async Task<bool> RunValidationOrToastAsync(
        Func<Offerte, Task<ValidationResult>> validate, bool showFeedback)
    {
        var snapshot = BuildSnapshotForValidation();
        var vr = await validate(snapshot);

        var warn = vr.WarningText();
        if (showFeedback && !string.IsNullOrWhiteSpace(warn))
            Toast.Warning(warn);

        if (!vr.IsValid)
        {
            if (showFeedback) Toast.Error(vr.ErrorText());
            return false;
        }

        if (Offerte is not null)
        {
            Offerte.Datum = snapshot.Datum;
            Offerte.KlantId = snapshot.KlantId;
        }

        return true;
    }

    private Offerte BuildSnapshotForValidation()
    {
        var source = Offerte ?? new Offerte();
        var o = new Offerte
        {
            Id = source.Id, Datum = source.Datum, Opmerking = source.Opmerking,
            GeplandeDatum = source.GeplandeDatum, DeadlineDatum = source.DeadlineDatum,
            GeschatteMinuten = source.GeschatteMinuten, Status = source.Status,
            KortingPct = source.KortingPct, MeerPrijsIncl = source.MeerPrijsIncl,
            IsVoorschotBetaald = source.IsVoorschotBetaald, VoorschotBedrag = source.VoorschotBedrag,
            SubtotaalExBtw = source.SubtotaalExBtw, BtwBedrag = source.BtwBedrag,
            TotaalInclBtw = source.TotaalInclBtw
        };

        if (o.Datum == default) o.Datum = DateTime.Today;
        o.KlantId = KlantSelectie.SelectedKlant?.Id;
        o.Regels = Regelbeheer.Regels.Select(r => CloneRegelForSnapshot(r)).ToList();
        return o;
    }

    private Offerte BuildSnapshotForPricing()
    {
        var snapshot = BuildSnapshotForValidation();
        snapshot.Regels = Regelbeheer.Regels
            .Select(r => CloneRegelForSnapshot(r, includeNavigations: true))
            .ToList();
        return snapshot;
    }

    private static OfferteRegel CloneRegelForSnapshot(OfferteRegel r, bool includeNavigations = false)
    {
        return new OfferteRegel
        {
            Id = r.Id, OfferteId = r.OfferteId, AantalStuks = r.AantalStuks,
            BreedteCm = r.BreedteCm, HoogteCm = r.HoogteCm,
            InlegBreedteCm = r.InlegBreedteCm, InlegHoogteCm = r.InlegHoogteCm,
            Titel = r.Titel, Opmerking = r.Opmerking,
            TypeLijstId = r.TypeLijst?.Id ?? r.TypeLijstId,
            GlasId = r.Glas?.Id ?? r.GlasId,
            PassePartout1Id = r.PassePartout1?.Id ?? r.PassePartout1Id,
            PassePartout2Id = r.PassePartout2?.Id ?? r.PassePartout2Id,
            DiepteKernId = r.DiepteKern?.Id ?? r.DiepteKernId,
            OpklevenId = r.Opkleven?.Id ?? r.OpklevenId,
            RugId = r.Rug?.Id ?? r.RugId,
            AfgesprokenPrijsExcl = r.AfgesprokenPrijsExcl, ExtraWerkMinuten = r.ExtraWerkMinuten,
            ExtraPrijs = r.ExtraPrijs, Korting = r.Korting, LegacyCode = r.LegacyCode,
            TotaalExcl = r.TotaalExcl, SubtotaalExBtw = r.SubtotaalExBtw,
            BtwBedrag = r.BtwBedrag, TotaalInclBtw = r.TotaalInclBtw,
            TypeLijst  = includeNavigations ? r.TypeLijst  : null,
            Glas       = includeNavigations ? r.Glas       : null,
            PassePartout1 = includeNavigations ? r.PassePartout1 : null,
            PassePartout2 = includeNavigations ? r.PassePartout2 : null,
            DiepteKern = includeNavigations ? r.DiepteKern : null,
            Opkleven   = includeNavigations ? r.Opkleven   : null,
            Rug        = includeNavigations ? r.Rug        : null
        };
    }

    private void ApplyPricingSnapshot(Offerte snapshot)
    {
        if (Offerte is null) return;

        _suppressRecalc = true;
        try
        {
            Offerte.SubtotaalExBtw  = snapshot.SubtotaalExBtw;
            Offerte.BtwBedrag       = snapshot.BtwBedrag;
            Offerte.TotaalInclBtw   = snapshot.TotaalInclBtw;
            Offerte.VoorschotBedrag = snapshot.VoorschotBedrag;

            // Patch ONLY price fields in-place on the existing OfferteRegel instances.
            // Never replace the instances themselves: swapping an instance out of the
            // ObservableCollection causes the ListBox to null the TwoWay SelectedRegel
            // binding, which momentarily clears all ComboBox selections.
            var srcRegels = snapshot.Regels.ToList();   // ICollection → indexed list
            var count = Math.Min(Regelbeheer.Regels.Count, srcRegels.Count);
            for (var i = 0; i < count; i++)
            {
                var dst = Regelbeheer.Regels[i];
                var src = srcRegels[i];
                dst.TotaalExcl     = src.TotaalExcl;
                dst.SubtotaalExBtw = src.SubtotaalExBtw;
                dst.BtwBedrag      = src.BtwBedrag;
                dst.TotaalInclBtw  = src.TotaalInclBtw;
            }
            // Remove any excess regels (defensive; should not happen in practice).
            while (Regelbeheer.Regels.Count > srcRegels.Count)
                Regelbeheer.Regels.RemoveAt(Regelbeheer.Regels.Count - 1);

            // RefreshLijstPrijzenAsync replaces regel navigation properties with fresh
            // DB instances for accurate pricing.  Re-link them back to the catalog
            // instances so ComboBox reference-equality matching still works.
            RelinkSelectionsAfterCatalog();

            // Force the ComboBox SelectedItem bindings to re-read from the re-linked regel.
            OnPropertyChanged(nameof(SelectedRegelTypeLijst));
            OnPropertyChanged(nameof(SelectedRegelGlas));
            OnPropertyChanged(nameof(SelectedRegelPasse1));
            OnPropertyChanged(nameof(SelectedRegelPasse2));
            OnPropertyChanged(nameof(SelectedRegelDiepte));
            OnPropertyChanged(nameof(SelectedRegelOpkleven));
            OnPropertyChanged(nameof(SelectedRegelRug));

            RefreshTotals();
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private async Task RefreshLijstPrijzenAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var lijstIds = Regelbeheer.Regels
            .Select(r => r.TypeLijst?.Id ?? r.TypeLijstId)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var optieIds = Regelbeheer.Regels
            .SelectMany(r => new[]
            {
                r.Glas?.Id ?? r.GlasId, r.PassePartout1?.Id ?? r.PassePartout1Id,
                r.PassePartout2?.Id ?? r.PassePartout2Id, r.DiepteKern?.Id ?? r.DiepteKernId,
                r.Opkleven?.Id ?? r.OpklevenId, r.Rug?.Id ?? r.RugId
            })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var freshLijsten = lijstIds.Count > 0
            ? await db.TypeLijsten.Where(l => lijstIds.Contains(l.Id)).ToListAsync()
            : new System.Collections.Generic.List<TypeLijst>();

        var freshOpties = optieIds.Count > 0
            ? await db.AfwerkingsOpties.Where(o => optieIds.Contains(o.Id)).ToListAsync()
            : new System.Collections.Generic.List<AfwerkingsOptie>();

        foreach (var regel in Regelbeheer.Regels)
        {
            if (regel.TypeLijst is not null)
            {
                var fresh = freshLijsten.FirstOrDefault(l => l.Id == regel.TypeLijst.Id);
                if (fresh is not null) regel.TypeLijst = fresh;
            }
            if (regel.Glas is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.Glas.Id);
                if (fresh is not null) regel.Glas = fresh;
            }
            if (regel.PassePartout1 is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.PassePartout1.Id);
                if (fresh is not null) regel.PassePartout1 = fresh;
            }
            if (regel.PassePartout2 is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.PassePartout2.Id);
                if (fresh is not null) regel.PassePartout2 = fresh;
            }
            if (regel.DiepteKern is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.DiepteKern.Id);
                if (fresh is not null) regel.DiepteKern = fresh;
            }
            if (regel.Opkleven is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.Opkleven.Id);
                if (fresh is not null) regel.Opkleven = fresh;
            }
            if (regel.Rug is not null)
            {
                var fresh = freshOpties.FirstOrDefault(o => o.Id == regel.Rug.Id);
                if (fresh is not null) regel.Rug = fresh;
            }
        }
    }

    // Wrapper voor gebruik in Workflow delegate (geen showFeedback nodig)
    private Task BerekenSilentAsync() => Prijzen.BerekenSilentAsync();

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(OfferteEx));
        OnPropertyChanged(nameof(OfferteBtw));
        OnPropertyChanged(nameof(OfferteIncl));
    }

    // ── Navigation ──
    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<OffertesLijstViewModel>();

    [RelayCommand]
    private async Task OpenLijstBeheerAsync()
    {
        if (App.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = new QuadroApp.Views.LijstenWindow();
        window.Closed += async (_, _) => await LoadCatalogAsync();

        if (desktop.MainWindow is { } owner)
            window.Show(owner);
        else
            window.Show();
    }

    // ── DB helpers ──
    private static OfferteRegel BuildDbRegel(OfferteRegel vmRule, int offerteId) => new()
    {
        OfferteId = offerteId, AantalStuks = vmRule.AantalStuks, BreedteCm = vmRule.BreedteCm,
        HoogteCm = vmRule.HoogteCm, InlegBreedteCm = vmRule.InlegBreedteCm,
        InlegHoogteCm = vmRule.InlegHoogteCm, Titel = vmRule.Titel, Opmerking = vmRule.Opmerking,
        TypeLijstId = vmRule.TypeLijst?.Id, GlasId = vmRule.Glas?.Id,
        PassePartout1Id = vmRule.PassePartout1?.Id, PassePartout2Id = vmRule.PassePartout2?.Id,
        DiepteKernId = vmRule.DiepteKern?.Id, OpklevenId = vmRule.Opkleven?.Id,
        RugId = vmRule.Rug?.Id, AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl,
        ExtraWerkMinuten = vmRule.ExtraWerkMinuten, ExtraPrijs = vmRule.ExtraPrijs,
        Korting = vmRule.Korting, LegacyCode = vmRule.LegacyCode, TotaalExcl = vmRule.TotaalExcl,
        SubtotaalExBtw = vmRule.SubtotaalExBtw, BtwBedrag = vmRule.BtwBedrag,
        TotaalInclBtw = vmRule.TotaalInclBtw
    };

    private static void ApplyToDbRegel(OfferteRegel dbRule, OfferteRegel vmRule)
    {
        dbRule.AantalStuks = vmRule.AantalStuks; dbRule.BreedteCm = vmRule.BreedteCm;
        dbRule.HoogteCm = vmRule.HoogteCm; dbRule.InlegBreedteCm = vmRule.InlegBreedteCm;
        dbRule.InlegHoogteCm = vmRule.InlegHoogteCm; dbRule.Titel = vmRule.Titel;
        dbRule.Opmerking = vmRule.Opmerking; dbRule.TypeLijstId = vmRule.TypeLijst?.Id;
        dbRule.GlasId = vmRule.Glas?.Id; dbRule.PassePartout1Id = vmRule.PassePartout1?.Id;
        dbRule.PassePartout2Id = vmRule.PassePartout2?.Id; dbRule.DiepteKernId = vmRule.DiepteKern?.Id;
        dbRule.OpklevenId = vmRule.Opkleven?.Id; dbRule.RugId = vmRule.Rug?.Id;
        dbRule.AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl;
        dbRule.ExtraWerkMinuten = vmRule.ExtraWerkMinuten; dbRule.ExtraPrijs = vmRule.ExtraPrijs;
        dbRule.Korting = vmRule.Korting; dbRule.LegacyCode = vmRule.LegacyCode;
        dbRule.TotaalExcl = vmRule.TotaalExcl; dbRule.SubtotaalExBtw = vmRule.SubtotaalExBtw;
        dbRule.BtwBedrag = vmRule.BtwBedrag; dbRule.TotaalInclBtw = vmRule.TotaalInclBtw;
    }
}
