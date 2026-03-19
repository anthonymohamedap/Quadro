using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service; // LegacyAfwerkingCode
using QuadroApp.Validation;
using QuadroApp.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OfferteViewModel : ObservableObject, IAsyncInitializable
{
    // ==============================
    // KLANT ZOEKEN (zoals TypeLijst)
    // ==============================

    [ObservableProperty] private string? klantZoekterm;
    [ObservableProperty] private ObservableCollection<Klant> gefilterdeKlanten = new();
    private bool _suppressRecalc;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IPricingService _pricing;
    private readonly IOfferteWorkflowService _workflow;
    private readonly IWerkBonWorkflowService _werkBonWorkflow;
    private readonly IWorkflowService _statusWorkflow;
    private readonly ICrudValidator<Klant> _klantValidator;
    private readonly IKlantDialogService _klantDialog;
    // ====== state ======
    [ObservableProperty] private Offerte? offerte;
    [ObservableProperty] private ObservableCollection<OfferteRegel> regels = new();

    [ObservableProperty] private ObservableCollection<TypeLijst> typeLijsten = new();
    [ObservableProperty] private ObservableCollection<Klant> klanten = new();
    [ObservableProperty] private Klant? selectedKlant;

    // afwerking dropdowns
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> glasOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> passe1Opties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> passe2Opties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> diepteOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> opkleefOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> rugOpties = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    // ====== SelectedRegel expliciet (compiled bindings + auto recalc) ======
    private OfferteRegel? _selectedRegel;
    public OfferteRegel? SelectedRegel
    {
        get => _selectedRegel;
        set
        {
            if (SetProperty(ref _selectedRegel, value))
            {
                RegelDuplicerenCommand.NotifyCanExecuteChanged();
                ApplyLegacyCodeCommand.NotifyCanExecuteChanged();
                GenerateLegacyCodeCommand.NotifyCanExecuteChanged();

                OnPropertyChanged(nameof(LegacyCode));

                if (!_suppressRecalc)
                    _ = QueueRecalcAsync();
            }
        }
    }

    // ====== LegacyCode proxy (XAML: TextBox Text="{Binding LegacyCode, TwoWay}") ======
    public string? LegacyCode
    {
        get => SelectedRegel?.LegacyCode;
        set
        {
            if (SelectedRegel is null) return;
            if (SelectedRegel.LegacyCode != value)
            {
                SelectedRegel.LegacyCode = value;
                OnPropertyChanged();
            }
        }
    }

    // ====== Totals ======
    public decimal OfferteEx => Offerte?.SubtotaalExBtw ?? 0m;
    public decimal OfferteBtw => Offerte?.BtwBedrag ?? 0m;
    public decimal OfferteIncl => Offerte?.TotaalInclBtw ?? 0m;

    // ====== EXPLICIETE commands (zodat compiled bindings nooit falen) ======
    public IAsyncRelayCommand NieuweKlantCommand { get; }
    public IRelayCommand RegelDuplicerenCommand { get; }
    public IAsyncRelayCommand ApplyLegacyCodeCommand { get; }
    public IRelayCommand GenerateLegacyCodeCommand { get; }
    public IAsyncRelayCommand BevestigenCommand { get; }

    // BerekenCommand (als je XAML BerekenCommand gebruikt)
    public IRelayCommand BerekenCommand { get; }

    // ====== debounce voor auto pricing ======
    private CancellationTokenSource? _recalcCts;

    private readonly IOfferteValidator _validator;
    private readonly IToastService _toast;

    // ctor parameterlijst uitbreiden
    public OfferteViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        IPricingService pricing,
        IOfferteWorkflowService workflow,
        IWerkBonWorkflowService werkBonWorkflow,
        IWorkflowService statusWorkflow,
        IOfferteValidator validator,
        IToastService toast, ICrudValidator<Klant> crudValidator, IKlantDialogService klantDialog)
    {
        _dbFactory = dbFactory;
        _nav = nav;
        _dialogs = dialogs;
        _pricing = pricing;
        _workflow = workflow;
        _werkBonWorkflow = werkBonWorkflow;
        _statusWorkflow = statusWorkflow;
        _validator = validator;
        _klantValidator = crudValidator;
        _klantDialog = klantDialog;
        _toast = toast;

        BerekenCommand = new AsyncRelayCommand(() => BerekenAsync(showFeedback: true));
        NieuweKlantCommand = new AsyncRelayCommand(NieuweKlantAsync);
        RegelDuplicerenCommand = new RelayCommand(RegelDupliceren, () => SelectedRegel is not null);
        ApplyLegacyCodeCommand = new AsyncRelayCommand(ApplyLegacyCodeAsync, () => SelectedRegel is not null);
        GenerateLegacyCodeCommand = new RelayCommand(GenerateLegacyCode, () => SelectedRegel is not null);
        BevestigenCommand = new AsyncRelayCommand(BevestigenAsync);
    }

    public async Task InitializeAsync() => await LoadCatalogAsync();
    [ObservableProperty]
    private string? typeLijstZoekterm;

    [ObservableProperty]
    private ObservableCollection<TypeLijst> gefilterdeTypeLijsten = new();

    partial void OnTypeLijstZoektermChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            GefilterdeTypeLijsten = new ObservableCollection<TypeLijst>(TypeLijsten);
            return;
        }

        var t = value.Trim().ToLowerInvariant();

        GefilterdeTypeLijsten = new ObservableCollection<TypeLijst>(
            TypeLijsten.Where(x =>
                x.Artikelnummer != null &&
                x.Artikelnummer.ToLowerInvariant().Contains(t))
        );
    }
    private Offerte BuildSnapshotForValidation()
    {
        var source = Offerte ?? new Offerte();
        var o = new Offerte
        {
            Id = source.Id,
            Datum = source.Datum,
            Opmerking = source.Opmerking,
            GeplandeDatum = source.GeplandeDatum,
            DeadlineDatum = source.DeadlineDatum,
            GeschatteMinuten = source.GeschatteMinuten,
            Status = source.Status,
            KortingPct = source.KortingPct,
            MeerPrijsIncl = source.MeerPrijsIncl,
            IsVoorschotBetaald = source.IsVoorschotBetaald,
            VoorschotBedrag = source.VoorschotBedrag,
            SubtotaalExBtw = source.SubtotaalExBtw,
            BtwBedrag = source.BtwBedrag,
            TotaalInclBtw = source.TotaalInclBtw
        };

        // datum fix je hier al (warning komt ook via validator)
        if (o.Datum == default)
            o.Datum = DateTime.Today;

        o.KlantId = SelectedKlant?.Id;

        // zet regels als "disconnected"
        o.Regels = Regels.Select(r => CloneRegelForSnapshot(r)).ToList();

        return o;
    }

    private Offerte BuildSnapshotForPricing()
    {
        var snapshot = BuildSnapshotForValidation();
        snapshot.Regels = Regels.Select(r => CloneRegelForSnapshot(r, includeNavigations: true)).ToList();
        return snapshot;
    }

    private static OfferteRegel CloneRegelForSnapshot(OfferteRegel r, bool includeNavigations = false)
    {
        return new OfferteRegel
        {
            Id = r.Id,
            OfferteId = r.OfferteId,
            AantalStuks = r.AantalStuks,
            BreedteCm = r.BreedteCm,
            HoogteCm = r.HoogteCm,
            InlegBreedteCm = r.InlegBreedteCm,
            InlegHoogteCm = r.InlegHoogteCm,
            Opmerking = r.Opmerking,
            TypeLijstId = r.TypeLijst?.Id ?? r.TypeLijstId,
            GlasId = r.Glas?.Id ?? r.GlasId,
            PassePartout1Id = r.PassePartout1?.Id ?? r.PassePartout1Id,
            PassePartout2Id = r.PassePartout2?.Id ?? r.PassePartout2Id,
            DiepteKernId = r.DiepteKern?.Id ?? r.DiepteKernId,
            OpklevenId = r.Opkleven?.Id ?? r.OpklevenId,
            RugId = r.Rug?.Id ?? r.RugId,
            AfgesprokenPrijsExcl = r.AfgesprokenPrijsExcl,
            ExtraWerkMinuten = r.ExtraWerkMinuten,
            ExtraPrijs = r.ExtraPrijs,
            Korting = r.Korting,
            LegacyCode = r.LegacyCode,
            TotaalExcl = r.TotaalExcl,
            SubtotaalExBtw = r.SubtotaalExBtw,
            BtwBedrag = r.BtwBedrag,
            TotaalInclBtw = r.TotaalInclBtw,
            TypeLijst = includeNavigations ? r.TypeLijst : null,
            Glas = includeNavigations ? r.Glas : null,
            PassePartout1 = includeNavigations ? r.PassePartout1 : null,
            PassePartout2 = includeNavigations ? r.PassePartout2 : null,
            DiepteKern = includeNavigations ? r.DiepteKern : null,
            Opkleven = includeNavigations ? r.Opkleven : null,
            Rug = includeNavigations ? r.Rug : null
        };
    }

    private void ApplyPricingSnapshot(Offerte snapshot)
    {
        if (Offerte is null)
            return;

        _suppressRecalc = true;
        try
        {
            Offerte.SubtotaalExBtw = snapshot.SubtotaalExBtw;
            Offerte.BtwBedrag = snapshot.BtwBedrag;
            Offerte.TotaalInclBtw = snapshot.TotaalInclBtw;
            Offerte.VoorschotBedrag = snapshot.VoorschotBedrag;

            var selectedIndex = SelectedRegel is null ? -1 : Regels.IndexOf(SelectedRegel);
            var selectedRuleId = SelectedRegel?.Id;
            var updatedRegels = snapshot.Regels.Select(r => CloneRegelForSnapshot(r, includeNavigations: true)).ToList();

            while (Regels.Count > updatedRegels.Count)
                Regels.RemoveAt(Regels.Count - 1);

            for (var i = 0; i < updatedRegels.Count; i++)
            {
                if (i < Regels.Count)
                    Regels[i] = updatedRegels[i];
                else
                    Regels.Add(updatedRegels[i]);
            }

            SelectedRegel =
                (selectedRuleId.HasValue ? Regels.FirstOrDefault(r => r.Id == selectedRuleId.Value) : null)
                ?? (selectedIndex >= 0 && selectedIndex < Regels.Count ? Regels[selectedIndex] : null)
                ?? Regels.FirstOrDefault();

            RefreshTotals();
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private async Task<bool> RunValidationOrToastAsync(Func<Offerte, Task<ValidationResult>> validate, bool showFeedback)
    {
        var snapshot = BuildSnapshotForValidation();
        var vr = await validate(snapshot);

        var warn = vr.WarningText();
        if (showFeedback && !string.IsNullOrWhiteSpace(warn))
            _toast.Warning(warn);

        if (!vr.IsValid)
        {
            if (showFeedback)
                _toast.Error(vr.ErrorText());
            return false;
        }

        // als valid: commit snapshot datum/klantId ook terug naar echte Offerte
        if (Offerte is not null)
        {
            Offerte.Datum = snapshot.Datum;
            Offerte.KlantId = snapshot.KlantId;
        }

        return true;
    }
    // ====== Catalog load ======
    public async Task LoadCatalogAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var lijsten = await db.TypeLijsten.AsNoTracking()
    .OrderBy(t => t.Artikelnummer)
    .ToListAsync();

        TypeLijsten.Clear();
        foreach (var l in lijsten)
            TypeLijsten.Add(l);


        var klanten = await db.Klanten.AsNoTracking()
            .OrderBy(k => k.Achternaam)
            .ThenBy(k => k.Voornaam)
            .ToListAsync();

        Klanten.Clear();
        foreach (var k in klanten)
            Klanten.Add(k);

        // 👇 voeg dit toe
        FilterKlanten();

        GlasOpties = await LoadOptiesAsync(db, 'G');
        Passe1Opties = await LoadOptiesAsync(db, 'P');
        Passe2Opties = await LoadOptiesAsync(db, 'P');
        DiepteOpties = await LoadOptiesAsync(db, 'D');
        OpkleefOpties = await LoadOptiesAsync(db, 'O');
        RugOpties = await LoadOptiesAsync(db, 'R');

        FilterKlanten();
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
            .OrderBy(a => a.Volgnummer)
            .ThenBy(a => a.Naam)
            .ToListAsync();

        return new ObservableCollection<AfwerkingsOptie>(list);
    }

    private void RelinkSelectionsAfterCatalog()
    {
        // Klant
        if (Offerte?.KlantId is int kid)
            SelectedKlant = Klanten.FirstOrDefault(k => k.Id == kid);

        // Relink all afwerking navigations for every regel so the ComboBox
        // SelectedItem matches an instance actually present in the catalog collections.
        foreach (var regel in Regels)
        {
            if (regel.TypeLijstId is int tid)
                regel.TypeLijst = TypeLijsten.FirstOrDefault(t => t.Id == tid);

            if (regel.GlasId is int gid)
                regel.Glas = GlasOpties.FirstOrDefault(g => g.Id == gid);

            if (regel.PassePartout1Id is int p1id)
                regel.PassePartout1 = Passe1Opties.FirstOrDefault(p => p.Id == p1id);

            if (regel.PassePartout2Id is int p2id)
                regel.PassePartout2 = Passe2Opties.FirstOrDefault(p => p.Id == p2id);

            if (regel.DiepteKernId is int did)
                regel.DiepteKern = DiepteOpties.FirstOrDefault(d => d.Id == did);

            if (regel.OpklevenId is int oid)
                regel.Opkleven = OpkleefOpties.FirstOrDefault(o => o.Id == oid);

            if (regel.RugId is int rid)
                regel.Rug = RugOpties.FirstOrDefault(r => r.Id == rid);
        }
    }


    private void FilterKlanten()
    {
        var term = KlantZoekterm?.Trim();

        var lijst = string.IsNullOrWhiteSpace(term)
            ? Klanten.ToList()
            : Klanten.Where(k =>
                (k.Voornaam ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (k.Achternaam ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (k.Email ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();

        // BELANGRIJK: niet vervangen, alleen inhoud updaten
        GefilterdeKlanten.Clear();
        foreach (var k in lijst)
            GefilterdeKlanten.Add(k);
    }
    // ====== Load offerte ======
    public async Task LoadAsync(int offerteId) => await LoadAsync(offerteId, reloadCatalog: true);

    private async Task LoadAsync(int offerteId, bool reloadCatalog)
    {
        _suppressRecalc = true;

        try
        {
            if (reloadCatalog)
                await LoadCatalogAsync(); // altijd eerst catalog laden

            await using var db = await _dbFactory.CreateDbContextAsync();

            var o = await db.Offertes
                .Include(x => x.Regels)
                .AsNoTracking()
                .FirstAsync(x => x.Id == offerteId);
            Offerte = o;

            SelectedKlant = GefilterdeKlanten.FirstOrDefault(k => k.Id == o.KlantId);
            Regels = new ObservableCollection<OfferteRegel>();

            foreach (var dbRule in o.Regels)
            {
                var rule = new OfferteRegel
                {
                    Id = dbRule.Id,
                    OfferteId = dbRule.OfferteId,
                    AantalStuks = dbRule.AantalStuks,
                    BreedteCm = dbRule.BreedteCm,
                    HoogteCm = dbRule.HoogteCm,
                    InlegBreedteCm = dbRule.InlegBreedteCm,
                    InlegHoogteCm = dbRule.InlegHoogteCm,
                    Opmerking = dbRule.Opmerking,
                    TypeLijstId = dbRule.TypeLijstId,
                    GlasId = dbRule.GlasId,
                    PassePartout1Id = dbRule.PassePartout1Id,
                    PassePartout2Id = dbRule.PassePartout2Id,
                    DiepteKernId = dbRule.DiepteKernId,
                    OpklevenId = dbRule.OpklevenId,
                    RugId = dbRule.RugId,
                    ExtraWerkMinuten = dbRule.ExtraWerkMinuten,
                    ExtraPrijs = dbRule.ExtraPrijs,
                    Korting = dbRule.Korting,
                    LegacyCode = dbRule.LegacyCode,
                    AfgesprokenPrijsExcl = dbRule.AfgesprokenPrijsExcl,
                    TotaalExcl = dbRule.TotaalExcl,
                    SubtotaalExBtw = dbRule.SubtotaalExBtw,
                    BtwBedrag = dbRule.BtwBedrag,
                    TotaalInclBtw = dbRule.TotaalInclBtw
                };

                // NU pas navigaties zetten met catalog instances
                rule.TypeLijst = TypeLijsten.FirstOrDefault(t => t.Id == rule.TypeLijstId);
                rule.Glas = GlasOpties.FirstOrDefault(g => g.Id == rule.GlasId);
                rule.PassePartout1 = Passe1Opties.FirstOrDefault(p => p.Id == rule.PassePartout1Id);
                rule.PassePartout2 = Passe2Opties.FirstOrDefault(p => p.Id == rule.PassePartout2Id);
                rule.DiepteKern = DiepteOpties.FirstOrDefault(x => x.Id == rule.DiepteKernId);
                rule.Opkleven = OpkleefOpties.FirstOrDefault(x => x.Id == rule.OpklevenId);
                rule.Rug = RugOpties.FirstOrDefault(x => x.Id == rule.RugId);

                Regels.Add(rule);
            }

            SelectedRegel = null;
            SelectedRegel = Regels.FirstOrDefault();

            RefreshTotals();
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    partial void OnKlantZoektermChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            GefilterdeKlanten = new ObservableCollection<Klant>(Klanten);
            return;
        }

        var t = value.Trim().ToLowerInvariant();

        GefilterdeKlanten = new ObservableCollection<Klant>(
            Klanten.Where(k =>
                (k.Voornaam != null && k.Voornaam.ToLowerInvariant().Contains(t)) ||
                (k.Achternaam != null && k.Achternaam.ToLowerInvariant().Contains(t)) ||
                (k.Email != null && k.Email.ToLowerInvariant().Contains(t))
            )
        );
    }
    // ====== New offerte ======
    [RelayCommand]
    public Task NewAsync()
    {
        Offerte = new Offerte();
        Regels = new ObservableCollection<OfferteRegel>();

        SelectedRegel = null;

        RefreshTotals();
        return Task.CompletedTask;
    }
    // ====== Save ======
    [RelayCommand]
    private async Task SaveAsync() => await SaveCoreAsync(reloadAfterSave: true);

    private async Task SaveCoreAsync(bool reloadAfterSave)
    {
        if (Offerte is null) return;
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await using var db = await _dbFactory.CreateDbContextAsync();

            Offerte.KlantId = SelectedKlant?.Id;

            if (Offerte.KlantId is null)
            {
                _toast.Error("Selecteer een klant.");
                return;
            }

            // 🔥 voorkom EF insert klant
            Offerte.Klant = null;
            if (Offerte.Datum == default) Offerte.Datum = DateTime.Today;

            // rebuild regels disconnected


            // NEW: eerst offerte opslaan zodat Id bestaat
            var shouldSaveAtEnd = true;

            if (Offerte.Id == 0)
            {
                db.Offertes.Add(Offerte);
                await db.SaveChangesAsync();

                // 🔥 voeg dit toe:
                foreach (var vmRule in Regels)
                {
                    var newRule = new OfferteRegel
                    {
                        OfferteId = Offerte.Id,
                        AantalStuks = vmRule.AantalStuks,
                        BreedteCm = vmRule.BreedteCm,
                        HoogteCm = vmRule.HoogteCm,
                        InlegBreedteCm = vmRule.InlegBreedteCm,
                        InlegHoogteCm = vmRule.InlegHoogteCm,
                        Opmerking = vmRule.Opmerking,
                        TypeLijstId = vmRule.TypeLijst?.Id,
                        GlasId = vmRule.Glas?.Id,
                        PassePartout1Id = vmRule.PassePartout1?.Id,
                        PassePartout2Id = vmRule.PassePartout2?.Id,
                        DiepteKernId = vmRule.DiepteKern?.Id,
                        OpklevenId = vmRule.Opkleven?.Id,
                        RugId = vmRule.Rug?.Id,
                        AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl,
                        ExtraWerkMinuten = vmRule.ExtraWerkMinuten,
                        ExtraPrijs = vmRule.ExtraPrijs,
                        Korting = vmRule.Korting,
                        LegacyCode = vmRule.LegacyCode,
                        TotaalExcl = vmRule.TotaalExcl,
                        SubtotaalExBtw = vmRule.SubtotaalExBtw,
                        BtwBedrag = vmRule.BtwBedrag,
                        TotaalInclBtw = vmRule.TotaalInclBtw
                    };

                    db.OfferteRegels.Add(newRule);
                }

                await db.SaveChangesAsync();
                shouldSaveAtEnd = false;
            }
            else
            {
                // ✅ Update enkel scalars via stub (geen navigation attach)
                var offerteStub = new Offerte
                {
                    Id = Offerte.Id,
                    KlantId = Offerte.KlantId,
                    Datum = Offerte.Datum,
                    Opmerking = Offerte.Opmerking,
                    GeplandeDatum = Offerte.GeplandeDatum,
                    DeadlineDatum = Offerte.DeadlineDatum,
                    GeschatteMinuten = Offerte.GeschatteMinuten,
                    Status = Offerte.Status,

                    // nieuwe velden
                    KortingPct = Offerte.KortingPct,
                    MeerPrijsIncl = Offerte.MeerPrijsIncl,
                    IsVoorschotBetaald = Offerte.IsVoorschotBetaald,
                    VoorschotBedrag = Offerte.VoorschotBedrag,
                    SubtotaalExBtw = Offerte.SubtotaalExBtw,
                    BtwBedrag = Offerte.BtwBedrag,
                    TotaalInclBtw = Offerte.TotaalInclBtw
                };

                db.Offertes.Attach(offerteStub);
                db.Entry(offerteStub).State = EntityState.Modified;
                // bestaande regels ophalen uit DB
                var existingRules = await db.OfferteRegels
                    .Where(x => x.OfferteId == Offerte.Id)
                    .ToListAsync();

                // verwijderen wat niet meer bestaat
                var currentIds = Regels.Where(r => r.Id > 0).Select(r => r.Id).ToHashSet();

                var toDelete = existingRules
                    .Where(x => !currentIds.Contains(x.Id))
                    .ToList();

                if (toDelete.Count > 0)
                    db.OfferteRegels.RemoveRange(toDelete);


                // update of insert
                foreach (var vmRule in Regels)
                {
                    if (vmRule.Id == 0)
                    {
                        // NIEUWE REGEL
                        var newRule = new OfferteRegel
                        {
                            OfferteId = Offerte.Id,
                            AantalStuks = vmRule.AantalStuks,
                            BreedteCm = vmRule.BreedteCm,
                            HoogteCm = vmRule.HoogteCm,
                            InlegBreedteCm = vmRule.InlegBreedteCm,
                            InlegHoogteCm = vmRule.InlegHoogteCm,
                            Opmerking = vmRule.Opmerking,
                            TypeLijstId = vmRule.TypeLijst?.Id,
                            GlasId = vmRule.Glas?.Id,
                            PassePartout1Id = vmRule.PassePartout1?.Id,
                            PassePartout2Id = vmRule.PassePartout2?.Id,
                            DiepteKernId = vmRule.DiepteKern?.Id,
                            OpklevenId = vmRule.Opkleven?.Id,
                            RugId = vmRule.Rug?.Id,
                            AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl,
                            ExtraWerkMinuten = vmRule.ExtraWerkMinuten,
                            ExtraPrijs = vmRule.ExtraPrijs,
                            Korting = vmRule.Korting,
                            LegacyCode = vmRule.LegacyCode,
                            TotaalExcl = vmRule.TotaalExcl,
                            SubtotaalExBtw = vmRule.SubtotaalExBtw,
                            BtwBedrag = vmRule.BtwBedrag,
                            TotaalInclBtw = vmRule.TotaalInclBtw
                        };

                        db.OfferteRegels.Add(newRule);
                    }
                    else
                    {
                        // BESTAANDE REGEL
                        var dbRule = existingRules.First(x => x.Id == vmRule.Id);

                        dbRule.AantalStuks = vmRule.AantalStuks;
                        dbRule.BreedteCm = vmRule.BreedteCm;
                        dbRule.HoogteCm = vmRule.HoogteCm;
                        dbRule.InlegBreedteCm = vmRule.InlegBreedteCm;
                        dbRule.InlegHoogteCm = vmRule.InlegHoogteCm;
                        dbRule.Opmerking = vmRule.Opmerking;
                        dbRule.TypeLijstId = vmRule.TypeLijst?.Id;
                        dbRule.GlasId = vmRule.Glas?.Id;
                        dbRule.PassePartout1Id = vmRule.PassePartout1?.Id;
                        dbRule.PassePartout2Id = vmRule.PassePartout2?.Id;
                        dbRule.DiepteKernId = vmRule.DiepteKern?.Id;
                        dbRule.OpklevenId = vmRule.Opkleven?.Id;
                        dbRule.RugId = vmRule.Rug?.Id;
                        dbRule.AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl;
                        dbRule.ExtraWerkMinuten = vmRule.ExtraWerkMinuten;
                        dbRule.ExtraPrijs = vmRule.ExtraPrijs;
                        dbRule.Korting = vmRule.Korting;
                        dbRule.LegacyCode = vmRule.LegacyCode;
                        dbRule.TotaalExcl = vmRule.TotaalExcl;
                        dbRule.SubtotaalExBtw = vmRule.SubtotaalExBtw;
                        dbRule.BtwBedrag = vmRule.BtwBedrag;
                        dbRule.TotaalInclBtw = vmRule.TotaalInclBtw;
                    }
                }
            }

            // upsert regels


            if (shouldSaveAtEnd)
                await db.SaveChangesAsync();

            _toast.Success("Offerte opgeslagen");

            if (reloadAfterSave)
            {
                // ✅ reload met includes zodat UI zeker klopt
                await LoadAsync(Offerte.Id, reloadCatalog: false);
            }
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

    // ====== Regelbeheer ======
    [RelayCommand]
    private void RegelToevoegen()
    {
        var r = new OfferteRegel { AantalStuks = 1, BreedteCm = 30, HoogteCm = 40 };
        Regels.Add(r);
        SelectedRegel = r;
    }

    [RelayCommand]
    private void RegelVerwijderen(OfferteRegel? regel)
    {
        if (regel is null) return;

        var wasSelected = ReferenceEquals(SelectedRegel, regel);
        Regels.Remove(regel);

        if (wasSelected)
            SelectedRegel = Regels.FirstOrDefault();
    }

    private void RegelDupliceren()
    {
        if (SelectedRegel is null) return;
        var s = SelectedRegel;

        var r = new OfferteRegel
        {
            AantalStuks = s.AantalStuks,
            BreedteCm = s.BreedteCm,
            HoogteCm = s.HoogteCm,
            InlegBreedteCm = s.InlegBreedteCm,
            InlegHoogteCm = s.InlegHoogteCm,
            Opmerking = s.Opmerking,
            TypeLijstId = s.TypeLijst?.Id ?? s.TypeLijstId,
            GlasId = s.Glas?.Id ?? s.GlasId,
            PassePartout1Id = s.PassePartout1?.Id ?? s.PassePartout1Id,
            PassePartout2Id = s.PassePartout2?.Id ?? s.PassePartout2Id,
            DiepteKernId = s.DiepteKern?.Id ?? s.DiepteKernId,
            OpklevenId = s.Opkleven?.Id ?? s.OpklevenId,
            RugId = s.Rug?.Id ?? s.RugId,

            AfgesprokenPrijsExcl = s.AfgesprokenPrijsExcl,
            ExtraWerkMinuten = s.ExtraWerkMinuten,
            ExtraPrijs = s.ExtraPrijs,
            Korting = s.Korting,
            LegacyCode = s.LegacyCode
        };

        Regels.Add(r);
        SelectedRegel = r;
    }

    // ====== 1) Legacy-code toepassen ======
    private async Task ApplyLegacyCodeAsync()
    {
        if (SelectedRegel is null)
            return;

        var code = (LegacyCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            await _dialogs.ShowErrorAsync("Legacy-code", "Geef een code in (6 tekens: G P P D O R).");
            return;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Zet navigatieprops op basis van code
            await LegacyAfwerkingCode.ApplyAsync(db, SelectedRegel, code);

            // Zorg dat FK Ids ook juist staan (handig voor SaveAsync/disconnected)
            SelectedRegel.GlasId = SelectedRegel.Glas?.Id;
            SelectedRegel.PassePartout1Id = SelectedRegel.PassePartout1?.Id;
            SelectedRegel.PassePartout2Id = SelectedRegel.PassePartout2?.Id;
            SelectedRegel.DiepteKernId = SelectedRegel.DiepteKern?.Id;
            SelectedRegel.OpklevenId = SelectedRegel.Opkleven?.Id;
            SelectedRegel.RugId = SelectedRegel.Rug?.Id;

            // Notify UI
            OnPropertyChanged(nameof(SelectedRegel));
            OnPropertyChanged(nameof(LegacyCode));

            // Auto recalc
            _ = QueueRecalcAsync();
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            await _dialogs.ShowErrorAsync("Legacy-code toepassen mislukt", Foutmelding);
        }
    }

    // ====== Legacy-code genereren ======
    private void GenerateLegacyCode()
    {
        if (SelectedRegel is null) return;

        // gebruikt navigatieprops (Volgnummer) → werkt als je al opties geladen hebt
        var code = LegacyAfwerkingCode.Generate(SelectedRegel);
        LegacyCode = code;
    }

    // ====== 2) Validatie: minimale checks voor berekening ======
    private bool ValidateBeforePricing(out string message)
    {
        if (Offerte is null)
        {
            message = "Geen offerte geladen.";
            return false;
        }

        if (Regels.Count == 0)
        {
            message = "Voeg minstens één regel toe.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    // ====== 3) Automatisch herberekenen bij SelectedRegel change ======
    private async Task QueueRecalcAsync()
    {
        // geen spam: cancel vorige en debounce
        _recalcCts?.Cancel();
        _recalcCts = new CancellationTokenSource();
        var token = _recalcCts.Token;

        try
        {
            await Task.Delay(350, token); // debounce
            if (token.IsCancellationRequested) return;

            if (Offerte is null) return;
            if (!ValidateBeforePricing(out _)) return;
            await BerekenAsync(showFeedback: false);
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    // ====== Pricing ======
    private async Task BerekenAsync(bool showFeedback)
    {
        if (Offerte is null) return;
        if (IsBusy) return;

        // ✅ pricing-validatie (strenger)
        var ok = await RunValidationOrToastAsync(o => _validator.ValidateForPricingAsync(o), showFeedback);
        if (!ok) return;

        try
        {
            IsBusy = true;

            var snapshot = BuildSnapshotForPricing();
            await _pricing.BerekenAsync(snapshot);
            ApplyPricingSnapshot(snapshot);

            if (showFeedback)
                _toast.Success("Berekening uitgevoerd");
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            if (showFeedback)
                await _dialogs.ShowErrorAsync("Berekenen mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ====== Bevestigen ======
    private async Task BevestigenAsync()
    {
        if (Offerte is null) return;
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            // Zorg dat alles eerst opgeslagen is
            if (Offerte.Id == 0)
                await SaveCoreAsync(reloadAfterSave: false);

            if (Offerte.Id == 0)
            {
                _toast.Error("Offerte kon niet opgeslagen worden. Bevestigen is gestopt.");
                return;
            }

            // Bevestigen + werkbon aanmaken
            await _workflow.BevestigAsync(Offerte.Id);

            _toast.Success("Offerte bevestigd. Werkbon aangemaakt.");

            // 🔄 Herlaad offerte (incl WerkBon) zodat UI direct klopt
            await LoadAsync(Offerte.Id);

            // WerkBon id halen (nu moet het bestaan)
            await using var db = await _dbFactory.CreateDbContextAsync();
            var werkBonId = await db.WerkBonnen
                .Where(w => w.OfferteId == Offerte.Id)
                .Select(w => w.Id)
                .FirstOrDefaultAsync();

            if (werkBonId == 0)
            {
                _toast.Error("Bevestiging gelukt, maar werkbon werd niet gevonden. Check workflow.");
                return;
            }

            // planning window (zoals je al had)
            var vm = new PlanningCalendarViewModel(_dbFactory, _werkBonWorkflow, _toast, _statusWorkflow);
            await vm.InitializeAsync(werkBonId);

            var window = new PlanningCalendarWindow { DataContext = vm };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null)
                    return;

                await window.ShowDialog(owner);
            }
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            await _dialogs.ShowErrorAsync("Bevestigen mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ====== Klant ======

    private async Task NieuweKlantAsync()
    {


        try
        {
            IsBusy = true;
            Foutmelding = null;

            // 1) Start met een "lege" klant (geen hardcoded naam opslaan)
            var nieuw = new Klant
            {
                Voornaam = "",
                Achternaam = "",
                Email = null,
                Telefoon = null,
                Straat = null,
                Nummer = null,
                Postcode = null,
                Gemeente = null,
                BtwNummer = null,
                Opmerking = null
            };

            // 2) Laat gebruiker invullen via dialog
            var ingevuld = await _klantDialog.EditAsync(nieuw);
            if (ingevuld is null)
            {
                _toast?.Info("Aanmaken geannuleerd.");
                return;
            }

            // trim basic
            ingevuld.Voornaam = (ingevuld.Voornaam ?? "").Trim();
            ingevuld.Achternaam = (ingevuld.Achternaam ?? "").Trim();
            ingevuld.Email = string.IsNullOrWhiteSpace(ingevuld.Email) ? null : ingevuld.Email.Trim();
            ingevuld.Telefoon = string.IsNullOrWhiteSpace(ingevuld.Telefoon) ? null : ingevuld.Telefoon.Trim();
            ingevuld.Straat = string.IsNullOrWhiteSpace(ingevuld.Straat) ? null : ingevuld.Straat.Trim();
            ingevuld.Nummer = string.IsNullOrWhiteSpace(ingevuld.Nummer) ? null : ingevuld.Nummer.Trim();
            ingevuld.Postcode = string.IsNullOrWhiteSpace(ingevuld.Postcode) ? null : ingevuld.Postcode.Trim();
            ingevuld.Gemeente = string.IsNullOrWhiteSpace(ingevuld.Gemeente) ? null : ingevuld.Gemeente.Trim();
            ingevuld.BtwNummer = string.IsNullOrWhiteSpace(ingevuld.BtwNummer) ? null : ingevuld.BtwNummer.Trim();
            ingevuld.Opmerking = string.IsNullOrWhiteSpace(ingevuld.Opmerking) ? null : ingevuld.Opmerking.Trim();

            // 3) Valideer (business rules + eventueel uniek email)
            var vr = await _klantValidator.ValidateCreateAsync(ingevuld);

            // warnings tonen (niet blokkeren)
            var warn = vr.WarningText();
            if (!string.IsNullOrWhiteSpace(warn))
                _toast?.Warning(warn);

            // errors blokkeren
            if (!vr.IsValid)
            {
                var err = vr.ErrorText();
                _toast?.Error(string.IsNullOrWhiteSpace(err) ? vr.ToString() : err);
                return;
            }

            // 4) Save naar DB
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.Klanten.Add(ingevuld);
            await db.SaveChangesAsync();

            // 5) UI update (als je in OfferteVM ook Klanten-lijst bijhoudt)
            //    (optioneel: als je die niet hebt, skip dit)
            if (Klanten is not null)
            {
                Klanten.Add(ingevuld);
            }

            // 6) Selecteer klant onmiddellijk op offerte
            //    (pas property namen aan naar jouw OfferteVM)
            SelectedKlant = ingevuld;
            if (Offerte is not null)
            {
                Offerte.KlantId = ingevuld.Id;
                Offerte.Klant = ingevuld;
            }


            _toast?.Success("Klant aangemaakt en geselecteerd.");
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
            _toast?.Error($"Klant aanmaken mislukt: {Foutmelding}");
            await _dialogs.ShowErrorAsync("Klant aanmaken mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;

            // als jij CanExecute changes gebruikt:
            NieuweKlantCommand?.NotifyCanExecuteChanged();
            SaveCommand?.NotifyCanExecuteChanged();
            BerekenCommand?.NotifyCanExecuteChanged();
        }
    }

    // ====== Navigation ======
    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<OffertesLijstViewModel>();

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(OfferteEx));
        OnPropertyChanged(nameof(OfferteBtw));
        OnPropertyChanged(nameof(OfferteIncl));
    }
}
