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
    // Guard: voorkomt dat Avalonia's TwoWay binding _selectedXxxNaam wist
    // terwijl ApplyNaamFilter de gefilterde collectie opbouwt.
    private bool _applyingNaamFilter;

    // ── Sub-ViewModels ──
    public KlantSelectieViewModel KlantSelectie { get; }
    public OfferteRegelViewModel Regelbeheer { get; }
    public OffertePrijsViewModel Prijzen { get; }
    public OfferteWorkflowViewModel Workflow { get; }

    // ── Forwarding properties: AXAML hoeft NIET te wijzigen ──

    // Totalen (footer)
    public decimal OfferteEx        => Offerte?.SubtotaalExBtw   ?? 0m;
    public decimal OfferteBtw       => Offerte?.BtwBedrag        ?? 0m;
    public decimal OfferteIncl      => Offerte?.TotaalInclBtw    ?? 0m;
    public decimal OfferteVoorschot  => Offerte?.VoorschotBedrag  ?? 0m;
    public decimal OfferteRest       => Offerte?.RestTeBetalen    ?? 0m;
    public bool    HeeftVoorschot    => OfferteVoorschot > 0m;

    // Wrapper zodat het voorschot live zichtbaar is tijdens de berekening
    public decimal VoorschotBedragInput
    {
        get => Offerte?.VoorschotBedrag ?? 0m;
        set
        {
            if (Offerte is null) return;
            Offerte.VoorschotBedrag = value;
            RefreshTotals();
            OnPropertyChanged();
        }
    }

    // Afhaal datum — manueel in te vullen door de gebruiker
    public DateTimeOffset? AfhaalDatumInput
    {
        get => Offerte?.AfhaalDatum.HasValue == true
            ? new DateTimeOffset(Offerte.AfhaalDatum.Value, TimeSpan.Zero)
            : null;
        set
        {
            if (Offerte is null) return;
            Offerte.AfhaalDatum = value?.DateTime.Date;
            OnPropertyChanged();
        }
    }

    // Notify wrapper properties when a different offerte is loaded
    partial void OnOfferteChanged(Offerte? value)
    {
        OnPropertyChanged(nameof(VoorschotBedragInput));
        OnPropertyChanged(nameof(AfhaalDatumInput));
        RefreshTotals();
    }

    // Workflow
    public string FactuurButtonText       => Workflow.FactuurButtonText;
    public string FactuurStatusText       => Workflow.FactuurStatusText;
    public bool   IsBevestigenZichtbaar   => Workflow.IsBevestigenZichtbaar;
    public bool   IsPlanningZichtbaar     => Workflow.IsPlanningZichtbaar;

    // Commands (alle geforward vanuit sub-VMs)
    public IAsyncRelayCommand BerekenCommand      => Prijzen.BerekenCommand;
    public IAsyncRelayCommand BevestigenCommand   => Workflow.BevestigenCommand;
    public IAsyncRelayCommand OpenPlanningCommand => Workflow.OpenPlanningCommand;
    public IAsyncRelayCommand FactuurCommand      => Workflow.FactuurCommand;
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

    /// <summary>Afhaal datum voor de geselecteerde offerte-regel (als DateTimeOffset? voor Avalonia DatePicker).</summary>
    public DateTimeOffset? SelectedRegelAfhaalDatum
    {
        get => Regelbeheer.SelectedRegel?.AfhaalDatum.HasValue == true
            ? new DateTimeOffset(Regelbeheer.SelectedRegel.AfhaalDatum!.Value, TimeSpan.Zero)
            : null;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            Regelbeheer.SelectedRegel.AfhaalDatum = value?.DateTime.Date;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// US-22: afgesproken prijs (incl. BTW) voor de geselecteerde regel.
    /// Bindt via een VM-proxy zodat élke wijziging — invullen én leegmaken — een
    /// herberekening triggert. Daardoor VERVANGT de afgesproken prijs de berekende
    /// regelprijs (engine doet dit), en keert het totaal terug naar de berekening
    /// zodra het veld leeggemaakt wordt. Voorheen schreef de TextBox rechtstreeks naar
    /// het model zonder recalc → oude (opgetelde) totaal bleef staan.
    /// </summary>
    public decimal? SelectedRegelAfgesprokenPrijs
    {
        get => Regelbeheer.SelectedRegel?.AfgesprokenPrijsExcl;
        set
        {
            if (Regelbeheer.SelectedRegel is null) return;
            if (Regelbeheer.SelectedRegel.AfgesprokenPrijsExcl == value) return;
            Regelbeheer.SelectedRegel.AfgesprokenPrijsExcl = value;
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
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
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.Glas = value;
            OptieGewijzigd(value, GlasVarianten, v => Regelbeheer.SelectedRegel.GlasVariant = v, nameof(SelectedRegelGlasVariant));
            SyncNaamBackward(value?.Naam, ref _selectedGlasNaam, nameof(SelectedGlasNaam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelPasse1
    {
        get => Regelbeheer.SelectedRegel?.PassePartout1;
        set
        {
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.PassePartout1 = value;
            OptieGewijzigd(value, Passe1VariantLijst, v => Regelbeheer.SelectedRegel.PassePartout1Variant = v, nameof(SelectedRegelPasse1Variant));
            SyncNaamBackward(value?.Naam, ref _selectedPasse1Naam, nameof(SelectedPasse1Naam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelPasse2
    {
        get => Regelbeheer.SelectedRegel?.PassePartout2;
        set
        {
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.PassePartout2 = value;
            OptieGewijzigd(value, Passe2VariantLijst, v => Regelbeheer.SelectedRegel.PassePartout2Variant = v, nameof(SelectedRegelPasse2Variant));
            SyncNaamBackward(value?.Naam, ref _selectedPasse2Naam, nameof(SelectedPasse2Naam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelDiepte
    {
        get => Regelbeheer.SelectedRegel?.DiepteKern;
        set
        {
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.DiepteKern = value;
            OptieGewijzigd(value, DiepteVariantLijst, v => Regelbeheer.SelectedRegel.DiepteKernVariant = v, nameof(SelectedRegelDiepteVariant));
            SyncNaamBackward(value?.Naam, ref _selectedDiepteNaam, nameof(SelectedDiepteNaam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelOpkleven
    {
        get => Regelbeheer.SelectedRegel?.Opkleven;
        set
        {
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.Opkleven = value;
            OptieGewijzigd(value, OpklevenVariantLijst, v => Regelbeheer.SelectedRegel.OpklevenVariant = v, nameof(SelectedRegelOpklevenVariant));
            SyncNaamBackward(value?.Naam, ref _selectedOpklevenNaam, nameof(SelectedOpklevenNaam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }
    public AfwerkingsOptie? SelectedRegelRug
    {
        get => Regelbeheer.SelectedRegel?.Rug;
        set
        {
            if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return;
            Regelbeheer.SelectedRegel.Rug = value;
            OptieGewijzigd(value, RugVariantLijst, v => Regelbeheer.SelectedRegel.RugVariant = v, nameof(SelectedRegelRugVariant));
            SyncNaamBackward(value?.Naam, ref _selectedRugNaam, nameof(SelectedRugNaam));
            OnPropertyChanged();
            if (!_suppressRecalc) Prijzen.TriggerRecalc();
        }
    }

    // ── Niveau 3 — AfwerkingsVariant per slot (bv. "Brons"/"Zwart") ────────────
    // De keuzelijst toont de Varianten van de gekozen optie; de keuze wordt op de
    // OfferteRegel bewaard (GlasVariant enz.) en verschijnt op bestelbon + weeklijst.
    public ObservableCollection<AfwerkingsVariant> GlasVarianten      { get; } = new();
    public ObservableCollection<AfwerkingsVariant> Passe1VariantLijst { get; } = new();
    public ObservableCollection<AfwerkingsVariant> Passe2VariantLijst { get; } = new();
    public ObservableCollection<AfwerkingsVariant> DiepteVariantLijst { get; } = new();
    public ObservableCollection<AfwerkingsVariant> OpklevenVariantLijst { get; } = new();
    public ObservableCollection<AfwerkingsVariant> RugVariantLijst    { get; } = new();

    public AfwerkingsVariant? SelectedRegelGlasVariant
    {
        get => Regelbeheer.SelectedRegel?.GlasVariant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.GlasVariant = value; OnPropertyChanged(); }
    }
    public AfwerkingsVariant? SelectedRegelPasse1Variant
    {
        get => Regelbeheer.SelectedRegel?.PassePartout1Variant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.PassePartout1Variant = value; OnPropertyChanged(); }
    }
    public AfwerkingsVariant? SelectedRegelPasse2Variant
    {
        get => Regelbeheer.SelectedRegel?.PassePartout2Variant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.PassePartout2Variant = value; OnPropertyChanged(); }
    }
    public AfwerkingsVariant? SelectedRegelDiepteVariant
    {
        get => Regelbeheer.SelectedRegel?.DiepteKernVariant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.DiepteKernVariant = value; OnPropertyChanged(); }
    }
    public AfwerkingsVariant? SelectedRegelOpklevenVariant
    {
        get => Regelbeheer.SelectedRegel?.OpklevenVariant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.OpklevenVariant = value; OnPropertyChanged(); }
    }
    public AfwerkingsVariant? SelectedRegelRugVariant
    {
        get => Regelbeheer.SelectedRegel?.RugVariant;
        set { if (Regelbeheer.SelectedRegel is null || _applyingNaamFilter) return; Regelbeheer.SelectedRegel.RugVariant = value; OnPropertyChanged(); }
    }

    private static void FillVarianten(ObservableCollection<AfwerkingsVariant> tgt, AfwerkingsOptie? optie)
    {
        var desired = optie is null
            ? new System.Collections.Generic.List<AfwerkingsVariant>()
            : optie.Varianten
                   .Where(v => v.IsActief && !v.IsGearchiveerd)
                   .OrderByDescending(v => v.IsStandaard)
                   .ThenBy(v => v.Beschrijving)
                   .ToList();

        // Niet onnodig herbouwen: als de inhoud al identiek is (zelfde instanties,
        // zelfde volgorde) laten we de keuzelijst met rust, zodat de ComboBox zijn
        // selectie behoudt na opslaan/herladen.
        if (tgt.Count == desired.Count && tgt.SequenceEqual(desired)) return;

        tgt.Clear();
        foreach (var v in desired) tgt.Add(v);
    }

    /// <summary>Herbouwt alle variant-keuzelijsten vanuit de opties van de geselecteerde regel
    /// (behoudt de reeds gekozen varianten). Aangeroepen bij regelselectie/herladen.</summary>
    private void RefreshAlleVarianten()
    {
        var r = Regelbeheer.SelectedRegel;
        // Guard aan tijdens het herbouwen van de ItemsSource: zo negeren de
        // variant-setters de null-writeback die Avalonia doet wanneer Clear()
        // de keuzelijst even leegt (anders wordt de gekozen variant gewist).
        _applyingNaamFilter = true;
        try
        {
            FillVarianten(GlasVarianten, r?.Glas);
            FillVarianten(Passe1VariantLijst, r?.PassePartout1);
            FillVarianten(Passe2VariantLijst, r?.PassePartout2);
            FillVarianten(DiepteVariantLijst, r?.DiepteKern);
            FillVarianten(OpklevenVariantLijst, r?.Opkleven);
            FillVarianten(RugVariantLijst, r?.Rug);
        }
        finally
        {
            _applyingNaamFilter = false;
        }
        OnPropertyChanged(nameof(SelectedRegelGlasVariant));
        OnPropertyChanged(nameof(SelectedRegelPasse1Variant));
        OnPropertyChanged(nameof(SelectedRegelPasse2Variant));
        OnPropertyChanged(nameof(SelectedRegelDiepteVariant));
        OnPropertyChanged(nameof(SelectedRegelOpklevenVariant));
        OnPropertyChanged(nameof(SelectedRegelRugVariant));
    }

    /// <summary>Na het wijzigen van een afwerkingsoptie: vul de variantlijst en kies de
    /// standaardvariant (of niets). Aangeroepen vanuit de optie-setters.</summary>
    private void OptieGewijzigd(AfwerkingsOptie? optie, ObservableCollection<AfwerkingsVariant> tgt,
                               System.Action<AfwerkingsVariant?> setVariant, string variantProxy)
    {
        // Guard aan tijdens herbouw zodat de binding-writeback de selectie niet wist;
        // save/restore omdat dit ook vanuit ApplyNaamFilter (guard al aan) kan komen.
        var prev = _applyingNaamFilter;
        _applyingNaamFilter = true;
        try { FillVarianten(tgt, optie); }
        finally { _applyingNaamFilter = prev; }

        setVariant(tgt.FirstOrDefault(v => v.IsStandaard) ?? tgt.FirstOrDefault());
        OnPropertyChanged(variantProxy);
    }

    // ── Twee-niveau afwerking selectie ────────────────────────────────────────
    // Niveau 1 — unieke namen per groep (gebouwd na catalog load)
    public System.Collections.Generic.List<string> GlasNamen    { get; private set; } = new();
    public System.Collections.Generic.List<string> Passe1Namen  { get; private set; } = new();
    public System.Collections.Generic.List<string> Passe2Namen  { get; private set; } = new();
    public System.Collections.Generic.List<string> DiepteNamen  { get; private set; } = new();
    public System.Collections.Generic.List<string> OpklevenNamen { get; private set; } = new();
    public System.Collections.Generic.List<string> RugNamen     { get; private set; } = new();

    // Niveau 2 — gefilterde opties op basis van geselecteerde naam
    public ObservableCollection<AfwerkingsOptie> GefilterdeGlasVarianten     { get; } = new();
    public ObservableCollection<AfwerkingsOptie> GefilterdePasse1Varianten   { get; } = new();
    public ObservableCollection<AfwerkingsOptie> GefilterdePasse2Varianten   { get; } = new();
    public ObservableCollection<AfwerkingsOptie> GefilterdeDiepteVarianten   { get; } = new();
    public ObservableCollection<AfwerkingsOptie> GefilterdeOpklevenVarianten { get; } = new();
    public ObservableCollection<AfwerkingsOptie> GefilterdeRugVarianten      { get; } = new();

    // Backing fields voor geselecteerde naam (level 1)
    private string? _selectedGlasNaam;
    private string? _selectedPasse1Naam;
    private string? _selectedPasse2Naam;
    private string? _selectedDiepteNaam;
    private string? _selectedOpklevenNaam;
    private string? _selectedRugNaam;

    public string? SelectedGlasNaam
    {
        get => _selectedGlasNaam;
        set
        {
            if (_selectedGlasNaam == value) return;
            _selectedGlasNaam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.GlasOpties, GefilterdeGlasVarianten, value,
                () => SelectedRegelGlas,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.Glas = v; OptieGewijzigd(v, GlasVarianten, vr => Regelbeheer.SelectedRegel.GlasVariant = vr, nameof(SelectedRegelGlasVariant)); OnPropertyChanged(nameof(SelectedRegelGlas)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }
    public string? SelectedPasse1Naam
    {
        get => _selectedPasse1Naam;
        set
        {
            if (_selectedPasse1Naam == value) return;
            _selectedPasse1Naam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.Passe1Opties, GefilterdePasse1Varianten, value,
                () => SelectedRegelPasse1,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.PassePartout1 = v; OptieGewijzigd(v, Passe1VariantLijst, vr => Regelbeheer.SelectedRegel.PassePartout1Variant = vr, nameof(SelectedRegelPasse1Variant)); OnPropertyChanged(nameof(SelectedRegelPasse1)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }
    public string? SelectedPasse2Naam
    {
        get => _selectedPasse2Naam;
        set
        {
            if (_selectedPasse2Naam == value) return;
            _selectedPasse2Naam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.Passe2Opties, GefilterdePasse2Varianten, value,
                () => SelectedRegelPasse2,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.PassePartout2 = v; OptieGewijzigd(v, Passe2VariantLijst, vr => Regelbeheer.SelectedRegel.PassePartout2Variant = vr, nameof(SelectedRegelPasse2Variant)); OnPropertyChanged(nameof(SelectedRegelPasse2)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }
    public string? SelectedDiepteNaam
    {
        get => _selectedDiepteNaam;
        set
        {
            if (_selectedDiepteNaam == value) return;
            _selectedDiepteNaam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.DiepteOpties, GefilterdeDiepteVarianten, value,
                () => SelectedRegelDiepte,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.DiepteKern = v; OptieGewijzigd(v, DiepteVariantLijst, vr => Regelbeheer.SelectedRegel.DiepteKernVariant = vr, nameof(SelectedRegelDiepteVariant)); OnPropertyChanged(nameof(SelectedRegelDiepte)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }
    public string? SelectedOpklevenNaam
    {
        get => _selectedOpklevenNaam;
        set
        {
            if (_selectedOpklevenNaam == value) return;
            _selectedOpklevenNaam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.OpkleefOpties, GefilterdeOpklevenVarianten, value,
                () => SelectedRegelOpkleven,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.Opkleven = v; OptieGewijzigd(v, OpklevenVariantLijst, vr => Regelbeheer.SelectedRegel.OpklevenVariant = vr, nameof(SelectedRegelOpklevenVariant)); OnPropertyChanged(nameof(SelectedRegelOpkleven)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }
    public string? SelectedRugNaam
    {
        get => _selectedRugNaam;
        set
        {
            if (_selectedRugNaam == value) return;
            _selectedRugNaam = value;
            OnPropertyChanged();
            ApplyNaamFilter(Regelbeheer.RugOpties, GefilterdeRugVarianten, value,
                () => SelectedRegelRug,
                v => { if (Regelbeheer.SelectedRegel is not null) { Regelbeheer.SelectedRegel.Rug = v; OptieGewijzigd(v, RugVariantLijst, vr => Regelbeheer.SelectedRegel.RugVariant = vr, nameof(SelectedRegelRugVariant)); OnPropertyChanged(nameof(SelectedRegelRug)); if (!_suppressRecalc) Prijzen.TriggerRecalc(); } });
        }
    }

    /// <summary>
    /// Vult een gefilterde level-2 collectie op basis van de gekozen naam.
    /// Selecteert automatisch als er maar 1 variant is; wist de selectie als de
    /// huidige selectie niet langer in de gefilterde lijst staat.
    /// </summary>
    private void ApplyNaamFilter(
        ObservableCollection<AfwerkingsOptie> source,
        ObservableCollection<AfwerkingsOptie> target,
        string? naam,
        Func<AfwerkingsOptie?> getSelected,
        Action<AfwerkingsOptie?> setSelected)
    {
        // Zet de guard vóór Clear(): als Avalonia's TwoWay binding daarna
        // SelectedRegelXxx = null terugschrijft, slaat SyncNaamBackward dit over
        // zodat _selectedXxxNaam intact blijft tijdens het herbouwen.
        _applyingNaamFilter = true;
        try
        {
            target.Clear();
            if (naam is not null)
                foreach (var o in source.Where(x => x.Naam == naam))
                    target.Add(o);

            if (Regelbeheer.SelectedRegel is null) return;

            var current = getSelected();
            if (target.Count == 1)
                setSelected(target[0]);
            else if (current is not null && !target.Contains(current))
                setSelected(null);
        }
        finally
        {
            _applyingNaamFilter = false;
        }
    }

    /// <summary>
    /// Synct _selectedXxxNaam terug vanuit een SelectedRegelXxx setter
    /// (bijv. na ApplyLegacyCode) zonder de ApplyNaamFilter te triggeren.
    /// </summary>
    private void SyncNaamBackward(string? naam, ref string? field, string propName)
    {
        if (field == naam) return;
        field = naam;
        OnPropertyChanged(propName);
    }

    /// <summary>
    /// Herbouwt de naam-lijsten (level 1) nadat de catalog geladen is.
    /// </summary>
    private void RebuildNaamLijsten()
    {
        static System.Collections.Generic.List<string> Distinct(ObservableCollection<AfwerkingsOptie> src)
            => src.Select(o => o.Naam ?? "").Where(n => n.Length > 0).Distinct().OrderBy(n => n).ToList();

        GlasNamen    = Distinct(Regelbeheer.GlasOpties);
        Passe1Namen  = Distinct(Regelbeheer.Passe1Opties);
        Passe2Namen  = Distinct(Regelbeheer.Passe2Opties);
        DiepteNamen  = Distinct(Regelbeheer.DiepteOpties);
        OpklevenNamen = Distinct(Regelbeheer.OpkleefOpties);
        RugNamen     = Distinct(Regelbeheer.RugOpties);

        OnPropertyChanged(nameof(GlasNamen));
        OnPropertyChanged(nameof(Passe1Namen));
        OnPropertyChanged(nameof(Passe2Namen));
        OnPropertyChanged(nameof(DiepteNamen));
        OnPropertyChanged(nameof(OpklevenNamen));
        OnPropertyChanged(nameof(RugNamen));
    }

    /// <summary>
    /// Synct de geselecteerde naam (level 1) en de gefilterde varianten (level 2)
    /// vanuit de huidig geselecteerde OfferteRegel — zonder auto-select of wissen.
    /// </summary>
    private void SyncNaamSelectiesFromRegel()
    {
        // Guard prevents Avalonia's TwoWay binding from writing null back to
        // SelectedRegelGlas etc. when Clear() momentarily empties ItemsSource.
        _applyingNaamFilter = true;
        try
        {
            var r = Regelbeheer.SelectedRegel;

            _selectedGlasNaam    = r?.Glas?.Naam;
            _selectedPasse1Naam  = r?.PassePartout1?.Naam;
            _selectedPasse2Naam  = r?.PassePartout2?.Naam;
            _selectedDiepteNaam  = r?.DiepteKern?.Naam;
            _selectedOpklevenNaam = r?.Opkleven?.Naam;
            _selectedRugNaam     = r?.Rug?.Naam;

            void Fill(ObservableCollection<AfwerkingsOptie> src, string? naam, ObservableCollection<AfwerkingsOptie> tgt)
            {
                tgt.Clear();
                if (naam is not null)
                    foreach (var o in src.Where(x => x.Naam == naam)) tgt.Add(o);
            }

            Fill(Regelbeheer.GlasOpties,    _selectedGlasNaam,     GefilterdeGlasVarianten);
            Fill(Regelbeheer.Passe1Opties,  _selectedPasse1Naam,   GefilterdePasse1Varianten);
            Fill(Regelbeheer.Passe2Opties,  _selectedPasse2Naam,   GefilterdePasse2Varianten);
            Fill(Regelbeheer.DiepteOpties,  _selectedDiepteNaam,   GefilterdeDiepteVarianten);
            Fill(Regelbeheer.OpkleefOpties, _selectedOpklevenNaam, GefilterdeOpklevenVarianten);
            Fill(Regelbeheer.RugOpties,     _selectedRugNaam,      GefilterdeRugVarianten);

            OnPropertyChanged(nameof(SelectedGlasNaam));
            OnPropertyChanged(nameof(SelectedPasse1Naam));
            OnPropertyChanged(nameof(SelectedPasse2Naam));
            OnPropertyChanged(nameof(SelectedDiepteNaam));
            OnPropertyChanged(nameof(SelectedOpklevenNaam));
            OnPropertyChanged(nameof(SelectedRugNaam));
        }
        finally
        {
            _applyingNaamFilter = false;
            // Fire level-2 notifications AFTER guard is off so ComboBoxes
            // re-read SelectedRegelXxx against the newly filled ItemsSource.
            OnPropertyChanged(nameof(SelectedRegelGlas));
            OnPropertyChanged(nameof(SelectedRegelPasse1));
            OnPropertyChanged(nameof(SelectedRegelPasse2));
            OnPropertyChanged(nameof(SelectedRegelDiepte));
            OnPropertyChanged(nameof(SelectedRegelOpkleven));
            OnPropertyChanged(nameof(SelectedRegelRug));
            OnPropertyChanged(nameof(SelectedRegelAfhaalDatum));
            OnPropertyChanged(nameof(SelectedRegelAfgesprokenPrijs));   // US-22
            RefreshAlleVarianten();   // variant-keuzelijsten + selecties bijwerken
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
                o => validator.ValidateForPricingAsync(o), showFeedback),
            // Alleen herladen (niet opslaan) — bv. na het sluiten van de planningskalender,
            // zodat de via planning gezette afhaaldatum zichtbaar wordt zonder de
            // in-memory waarden terug te schrijven.
            reloadOnly: async () =>
            {
                if (Offerte is { Id: > 0 } o)
                {
                    var selectedId = Regelbeheer.SelectedRegel?.Id;
                    await LoadAsync(o.Id, reloadCatalog: false);

                    // Herstel de selectie zodat de (nu read-only) afhaaldatum van de
                    // geselecteerde regel meteen zichtbaar is.
                    if (selectedId is int id && id > 0)
                    {
                        Regelbeheer.SelectedRegel = Regelbeheer.Regels.FirstOrDefault(r => r.Id == id);
                        OnPropertyChanged(nameof(SelectedRegel));
                    }
                }
            });

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
                    // SelectedRegelXxx notifications are fired by SyncNaamSelectiesFromRegel()
                    // in its finally block, AFTER GefilterdeXxxVarianten are rebuilt, so the
                    // ComboBoxes find their items and never write null back via TwoWay binding.
                    SyncNaamSelectiesFromRegel();
                    break;
                case nameof(Regelbeheer.SelectedTypeLijst):   OnPropertyChanged(nameof(SelectedRegelTypeLijst)); break;
                case nameof(Regelbeheer.Regels):              OnPropertyChanged(nameof(Regels)); break;
                case nameof(Regelbeheer.TypeLijstZoekterm):   OnPropertyChanged(nameof(TypeLijstZoekterm)); break;
                case nameof(Regelbeheer.GefilterdeTypeLijsten): OnPropertyChanged(nameof(GefilterdeTypeLijsten)); break;
                case nameof(Regelbeheer.GlasOpties):
                    OnPropertyChanged(nameof(GlasOpties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.Passe1Opties):
                    OnPropertyChanged(nameof(Passe1Opties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.Passe2Opties):
                    OnPropertyChanged(nameof(Passe2Opties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.DiepteOpties):
                    OnPropertyChanged(nameof(DiepteOpties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.OpkleefOpties):
                    OnPropertyChanged(nameof(OpkleefOpties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.RugOpties):
                    OnPropertyChanged(nameof(RugOpties));
                    RebuildNaamLijsten();
                    break;
                case nameof(Regelbeheer.LegacyCode):          OnPropertyChanged(nameof(LegacyCode)); break;
            }
        };

        Workflow.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Workflow.FactuurButtonText)
                               or nameof(Workflow.FactuurStatusText)
                               or nameof(Workflow.IsBevestigenZichtbaar)
                               or nameof(Workflow.IsPlanningZichtbaar))
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

        RebuildNaamLijsten();
        RelinkSelectionsAfterCatalog();
    }

    private static async Task<ObservableCollection<AfwerkingsOptie>> LoadOptiesAsync(AppDbContext db, char code)
    {
        var groepId = await db.AfwerkingsGroepen
            .Where(g => g.Code == code)
            .Select(g => g.Id)
            .FirstAsync();

        var list = await db.AfwerkingsOpties.AsNoTracking()
            .Include(a => a.Varianten)
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

            // ── Laad data op background thread om UI responsive te houden ──
            var (offerte, regels, klantId) = await Task.Run(async () =>
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                var o = await db.Offertes
                    .Include(x => x.Regels)
                    .AsNoTracking()
                    .FirstAsync(x => x.Id == offerteId);

                // Bouw lookup dictionaries voor snelle catalog matching (O(1) ipv O(n))
                var typeLijstDict = Regelbeheer.TypeLijsten.ToDictionary(t => t.Id);
                var glasDict = Regelbeheer.GlasOpties.ToDictionary(g => g.Id);
                var passe1Dict = Regelbeheer.Passe1Opties.ToDictionary(p => p.Id);
                var passe2Dict = Regelbeheer.Passe2Opties.ToDictionary(p => p.Id);
                var diepteDict = Regelbeheer.DiepteOpties.ToDictionary(d => d.Id);
                var opkleefDict = Regelbeheer.OpkleefOpties.ToDictionary(o => o.Id);
                var rugDict = Regelbeheer.RugOpties.ToDictionary(r => r.Id);

                var regelsList = new System.Collections.Generic.List<OfferteRegel>();
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
                        GlasVariantId = dbRule.GlasVariantId, PassePartout1VariantId = dbRule.PassePartout1VariantId,
                        PassePartout2VariantId = dbRule.PassePartout2VariantId, DiepteKernVariantId = dbRule.DiepteKernVariantId,
                        OpklevenVariantId = dbRule.OpklevenVariantId, RugVariantId = dbRule.RugVariantId,
                        ExtraWerkMinuten = dbRule.ExtraWerkMinuten, ExtraPrijs = dbRule.ExtraPrijs,
                        Korting = dbRule.Korting, LegacyCode = dbRule.LegacyCode,
                        AfgesprokenPrijsExcl = dbRule.AfgesprokenPrijsExcl,
                        TotaalExcl = dbRule.TotaalExcl, SubtotaalExBtw = dbRule.SubtotaalExBtw,
                        BtwBedrag = dbRule.BtwBedrag, TotaalInclBtw = dbRule.TotaalInclBtw,
                        AfhaalDatum = dbRule.AfhaalDatum
                    };

                    // Gebruik dictionary lookups ipv LINQ FirstOrDefault (veel sneller)
                    if (rule.TypeLijstId.HasValue && typeLijstDict.TryGetValue(rule.TypeLijstId.Value, out var typeLijst))
                        rule.TypeLijst = typeLijst;
                    // Lokale helper: relink de gekozen variant uit de Varianten van de optie.
                    static AfwerkingsVariant? Variant(AfwerkingsOptie? optie, int? variantId) =>
                        optie is null || !variantId.HasValue
                            ? null
                            : optie.Varianten.FirstOrDefault(v => v.Id == variantId.Value);

                    if (rule.GlasId.HasValue && glasDict.TryGetValue(rule.GlasId.Value, out var glas))
                    {
                        rule.Glas = glas;
                        rule.GlasVariant = Variant(glas, rule.GlasVariantId);
                    }
                    if (rule.PassePartout1Id.HasValue && passe1Dict.TryGetValue(rule.PassePartout1Id.Value, out var passe1))
                    {
                        rule.PassePartout1 = passe1;
                        rule.PassePartout1Variant = Variant(passe1, rule.PassePartout1VariantId);
                    }
                    if (rule.PassePartout2Id.HasValue && passe2Dict.TryGetValue(rule.PassePartout2Id.Value, out var passe2))
                    {
                        rule.PassePartout2 = passe2;
                        rule.PassePartout2Variant = Variant(passe2, rule.PassePartout2VariantId);
                    }
                    if (rule.DiepteKernId.HasValue && diepteDict.TryGetValue(rule.DiepteKernId.Value, out var diepte))
                    {
                        rule.DiepteKern = diepte;
                        rule.DiepteKernVariant = Variant(diepte, rule.DiepteKernVariantId);
                    }
                    if (rule.OpklevenId.HasValue && opkleefDict.TryGetValue(rule.OpklevenId.Value, out var opkleven))
                    {
                        rule.Opkleven = opkleven;
                        rule.OpklevenVariant = Variant(opkleven, rule.OpklevenVariantId);
                    }
                    if (rule.RugId.HasValue && rugDict.TryGetValue(rule.RugId.Value, out var rug))
                    {
                        rule.Rug = rug;
                        rule.RugVariant = Variant(rug, rule.RugVariantId);
                    }

                    regelsList.Add(rule);
                }

                return (o, regelsList, o.KlantId);
            });

            // ── Update UI op main thread ──
            Offerte = offerte;
            await Workflow.LoadFactuurContextAsync(await _dbFactory.CreateDbContextAsync(), offerteId);

            // Selecteer de juiste klant uit de al-geladen catalogus.
            // Niet SetKlanten aanroepen met de eigen collectie — dat wist hem eerst (self-clear bug).
            KlantSelectie.SelectedKlant = KlantSelectie.Klanten.FirstOrDefault(k => k.Id == klantId);

            Regelbeheer.Regels = new ObservableCollection<OfferteRegel>(regels);
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
                    AfhaalDatum = Offerte.AfhaalDatum,
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
            SyncNaamSelectiesFromRegel();

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
        OnPropertyChanged(nameof(OfferteVoorschot));
        OnPropertyChanged(nameof(OfferteRest));
        OnPropertyChanged(nameof(HeeftVoorschot));
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
        GlasVariantId = vmRule.GlasVariant?.Id, PassePartout1VariantId = vmRule.PassePartout1Variant?.Id,
        PassePartout2VariantId = vmRule.PassePartout2Variant?.Id, DiepteKernVariantId = vmRule.DiepteKernVariant?.Id,
        OpklevenVariantId = vmRule.OpklevenVariant?.Id, RugVariantId = vmRule.RugVariant?.Id,
        ExtraWerkMinuten = vmRule.ExtraWerkMinuten, ExtraPrijs = vmRule.ExtraPrijs,
        Korting = vmRule.Korting, LegacyCode = vmRule.LegacyCode, TotaalExcl = vmRule.TotaalExcl,
        SubtotaalExBtw = vmRule.SubtotaalExBtw, BtwBedrag = vmRule.BtwBedrag,
        TotaalInclBtw = vmRule.TotaalInclBtw, AfhaalDatum = vmRule.AfhaalDatum
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
        dbRule.GlasVariantId = vmRule.GlasVariant?.Id;
        dbRule.PassePartout1VariantId = vmRule.PassePartout1Variant?.Id;
        dbRule.PassePartout2VariantId = vmRule.PassePartout2Variant?.Id;
        dbRule.DiepteKernVariantId = vmRule.DiepteKernVariant?.Id;
        dbRule.OpklevenVariantId = vmRule.OpklevenVariant?.Id;
        dbRule.RugVariantId = vmRule.RugVariant?.Id;
        dbRule.AfgesprokenPrijsExcl = vmRule.AfgesprokenPrijsExcl;
        dbRule.ExtraWerkMinuten = vmRule.ExtraWerkMinuten; dbRule.ExtraPrijs = vmRule.ExtraPrijs;
        dbRule.Korting = vmRule.Korting; dbRule.LegacyCode = vmRule.LegacyCode;
        dbRule.TotaalExcl = vmRule.TotaalExcl; dbRule.SubtotaalExBtw = vmRule.SubtotaalExBtw;
        dbRule.BtwBedrag = vmRule.BtwBedrag; dbRule.TotaalInclBtw = vmRule.TotaalInclBtw;
        dbRule.AfhaalDatum = vmRule.AfhaalDatum;
    }
}
