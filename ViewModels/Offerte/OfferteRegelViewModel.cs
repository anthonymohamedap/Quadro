using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OfferteRegelViewModel : AsyncViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDialogService _dialogs;

    // ── Regels ──
    [ObservableProperty] private ObservableCollection<OfferteRegel> regels = new();

    // ── TypeLijst zoeken + selectie ──
    [ObservableProperty] private ObservableCollection<TypeLijst> typeLijsten = new();
    [ObservableProperty] private ObservableCollection<TypeLijst> gefilterdeTypeLijsten = new();
    [ObservableProperty] private string? typeLijstZoekterm;

    /// <summary>
    /// De geselecteerde TypeLijst voor de huidige regel — een echte [ObservableProperty]
    /// zodat Avalonia het betrouwbaar kan tracken, net zoals SelectedKlant in KlantSelectieViewModel.
    /// </summary>
    [ObservableProperty] private TypeLijst? selectedTypeLijst;

    // Guard: voorkomt circulaire schrijf-terug wanneer SelectedRegel verandert
    // en we SelectedTypeLijst syncen vanuit de regel.
    private bool _syncingTypeLijst;

    // ── US-53: Inleg-TypeLijst zoeken + selectie (zelfde patroon als TypeLijst) ──
    // Deelt dezelfde bron-catalogus (TypeLijsten) maar met een eigen filter + selectie,
    // zodat het inleg-nummer los van de kaderkeuze gekozen kan worden.
    [ObservableProperty] private ObservableCollection<TypeLijst> gefilterdeInlegTypeLijsten = new();
    [ObservableProperty] private string? inlegTypeLijstZoekterm;
    [ObservableProperty] private TypeLijst? selectedInlegTypeLijst;
    private bool _syncingInlegTypeLijst;

    // ── US-58: kant-en-klaar kader zoeken + selectie (zelfde patroon als TypeLijst, maar
    // zonder zoekterm/filter — de lijst is klein). Alternatief voor TypeLijst: bij keuze worden
    // BreedteCm/HoogteCm van de regel automatisch overgenomen (de maat ligt vast). ──
    [ObservableProperty] private ObservableCollection<KantKlaarKader> kantKlaarKaders = new();
    [ObservableProperty] private KantKlaarKader? selectedKantKlaarKader;
    private bool _syncingKantKlaarKader;

    /// <summary>US-58 — true zodra de huidige regel een kant-en-klaar kader gebruikt: de UI zet
    /// Breedte/Hoogte dan read-only, want de maat ligt vast.</summary>
    public bool HasKantKlaarKader => SelectedRegel?.KantKlaarKaderId is not null;

    // ── Afwerking dropdowns ──
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> glasOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> passe1Opties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> passe2Opties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> diepteOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> opkleefOpties = new();
    [ObservableProperty] private ObservableCollection<AfwerkingsOptie> rugOpties = new();

    // ── SelectedRegel: manual property voor CanExecute + recalc trigger ──
    private OfferteRegel? _selectedRegel;
    public OfferteRegel? SelectedRegel
    {
        get => _selectedRegel;
        set
        {
            if (SetProperty(ref _selectedRegel, value))
            {
                // Sync SelectedTypeLijst from the new regel (like SetKlanten syncs SelectedKlant).
                // Use the guard so OnSelectedTypeLijstChanged doesn't write back into the regel.
                _syncingTypeLijst = true;
                try { SelectedTypeLijst = value?.TypeLijst; }
                finally { _syncingTypeLijst = false; }

                // US-53: idem voor het inleg-nummer.
                _syncingInlegTypeLijst = true;
                try { SelectedInlegTypeLijst = value?.InlegTypeLijst; }
                finally { _syncingInlegTypeLijst = false; }

                // US-58: idem voor het kant-en-klaar kader.
                _syncingKantKlaarKader = true;
                try { SelectedKantKlaarKader = value?.KantKlaarKader; }
                finally { _syncingKantKlaarKader = false; }
                OnPropertyChanged(nameof(HasKantKlaarKader));

                RegelDuplicerenCommand.NotifyCanExecuteChanged();
                ApplyLegacyCodeCommand.NotifyCanExecuteChanged();
                GenerateLegacyCodeCommand.NotifyCanExecuteChanged();
                OpenTypeLijstCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(LegacyCode));
                RegelChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Wanneer de gebruiker een TypeLijst kiest in de ComboBox, schrijf het terug
    /// naar de huidige regel — zelfde patroon als OnSelectedKlantChanged in KlantSelectieViewModel.
    /// </summary>
    partial void OnSelectedTypeLijstChanged(TypeLijst? value)
    {
        OpenTypeLijstCommand.NotifyCanExecuteChanged();
        if (_syncingTypeLijst || SelectedRegel is null) return;
        SelectedRegel.TypeLijst = value;

        // Een regel heeft óf een op-maat lijst, óf een kant-en-klaar kader, nooit beide.
        if (value is not null && SelectedRegel.KantKlaarKaderId is not null)
        {
            SelectedRegel.KantKlaarKader = null;
            _syncingKantKlaarKader = true;
            try { SelectedKantKlaarKader = null; }
            finally { _syncingKantKlaarKader = false; }
            OnPropertyChanged(nameof(HasKantKlaarKader));
        }

        RegelChanged?.Invoke();
    }

    /// <summary>
    /// Synct SelectedTypeLijst vanuit de huidige SelectedRegel na catalog-relink.
    /// Gebruikt de guard zodat OnSelectedTypeLijstChanged niet terugschrijft naar de regel.
    /// </summary>
    public void SyncTypeLijstFromSelectedRegel()
    {
        _syncingTypeLijst = true;
        try { SelectedTypeLijst = SelectedRegel?.TypeLijst; }
        finally { _syncingTypeLijst = false; }
    }

    /// <summary>US-53 — schrijf de gekozen inleg-TypeLijst terug naar de huidige regel.</summary>
    partial void OnSelectedInlegTypeLijstChanged(TypeLijst? value)
    {
        if (_syncingInlegTypeLijst || SelectedRegel is null) return;
        SelectedRegel.InlegTypeLijst = value;
        RegelChanged?.Invoke();
    }

    /// <summary>US-53 — synct SelectedInlegTypeLijst vanuit de huidige SelectedRegel.</summary>
    public void SyncInlegTypeLijstFromSelectedRegel()
    {
        _syncingInlegTypeLijst = true;
        try { SelectedInlegTypeLijst = SelectedRegel?.InlegTypeLijst; }
        finally { _syncingInlegTypeLijst = false; }
    }

    /// <summary>US-58 — schrijf het gekozen kant-en-klaar kader terug naar de huidige regel en
    /// neem meteen de vaste maat van het kader over (Breedte/Hoogte worden in de UI read-only
    /// zodra <see cref="HasKantKlaarKader"/> true is).</summary>
    partial void OnSelectedKantKlaarKaderChanged(KantKlaarKader? value)
    {
        if (_syncingKantKlaarKader || SelectedRegel is null) return;

        SelectedRegel.KantKlaarKader = value;
        if (value is not null)
        {
            // Het kader is al volledig afgewerkt — een eventueel eerder gekozen afwerking
            // (glas/passe-partout/...) mag niet blijven hangen, ook al toont de UI die niet meer.
            SelectedRegel.Glas = null;
            SelectedRegel.PassePartout1 = null;
            SelectedRegel.PassePartout2 = null;
            SelectedRegel.DiepteKern = null;
            SelectedRegel.Opkleven = null;
            SelectedRegel.Rug = null;
        }
        OnPropertyChanged(nameof(HasKantKlaarKader));
        RegelChanged?.Invoke();
    }

    /// <summary>US-58 — synct SelectedKantKlaarKader vanuit de huidige SelectedRegel.</summary>
    public void SyncKantKlaarKaderFromSelectedRegel()
    {
        _syncingKantKlaarKader = true;
        try { SelectedKantKlaarKader = SelectedRegel?.KantKlaarKader; }
        finally { _syncingKantKlaarKader = false; }
        OnPropertyChanged(nameof(HasKantKlaarKader));
    }

    // ── LegacyCode proxy ──
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

    /// <summary>
    /// Vuurt wanneer SelectedRegel wisselt of een regel gewijzigd wordt.
    /// OfferteViewModel/OffertePrijsViewModel abonneert om pricing te debounce-triggeren.
    /// </summary>
    public event Action? RegelChanged;

    // ── Commands ──
    public IRelayCommand RegelDuplicerenCommand { get; }
    public IAsyncRelayCommand ApplyLegacyCodeCommand { get; }
    public IRelayCommand GenerateLegacyCodeCommand { get; }
    public IAsyncRelayCommand OpenTypeLijstCommand { get; }

    public OfferteRegelViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IDialogService dialogs,
        IToastService toast)
        : base(toast)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RegelDuplicerenCommand = new RelayCommand(RegelDupliceren, () => SelectedRegel is not null);
        ApplyLegacyCodeCommand = new AsyncRelayCommand(ApplyLegacyCodeAsync, () => SelectedRegel is not null);
        GenerateLegacyCodeCommand = new RelayCommand(GenerateLegacyCode, () => SelectedRegel is not null);
        OpenTypeLijstCommand = new AsyncRelayCommand(OpenTypeLijstAsync, () => SelectedRegel?.TypeLijst is not null);
    }

    partial void OnTypeLijstZoektermChanged(string? value)
    {
        // CRITICAL: Never use Clear() on GefilterdeTypeLijsten.
        // Clear() momentarily empties the collection → ComboBox can't find SelectedItem
        // → writes null back via TwoWay binding → OnSelectedTypeLijstChanged(null)
        // → SelectedRegel.TypeLijst = null → TypeLijstId = null → first regel lost.
        //
        // Instead, diff the target set against the current collection:
        // remove items that shouldn't be there, add items that are missing.
        // The selected item is never momentarily absent → ComboBox keeps its selection.
        ApplyTypeLijstFilter(value);
    }

    partial void OnInlegTypeLijstZoektermChanged(string? value) => ApplyInlegTypeLijstFilter(value);

    /// <summary>US-53 — filtert GefilterdeInlegTypeLijsten via diff (nooit Clear), zelfde
    /// aanpak als ApplyTypeLijstFilter zodat de ComboBox zijn SelectedItem niet verliest.</summary>
    public void ApplyInlegTypeLijstFilter(string? zoekterm)
    {
        System.Collections.Generic.List<TypeLijst> target;
        if (string.IsNullOrWhiteSpace(zoekterm))
        {
            target = new System.Collections.Generic.List<TypeLijst>(TypeLijsten);
        }
        else
        {
            var z = zoekterm.Trim().ToLowerInvariant();
            target = TypeLijsten
                .Where(x => x.Artikelnummer != null &&
                            x.Artikelnummer.ToLowerInvariant().Contains(z))
                .ToList();
        }

        var targetSet = new System.Collections.Generic.HashSet<TypeLijst>(target);

        for (int i = GefilterdeInlegTypeLijsten.Count - 1; i >= 0; i--)
        {
            if (!targetSet.Contains(GefilterdeInlegTypeLijsten[i]))
                GefilterdeInlegTypeLijsten.RemoveAt(i);
        }

        var currentSet = new System.Collections.Generic.HashSet<TypeLijst>(GefilterdeInlegTypeLijsten);
        int insertAt = 0;
        foreach (var item in target)
        {
            if (!currentSet.Contains(item))
            {
                GefilterdeInlegTypeLijsten.Insert(insertAt, item);
                currentSet.Add(item);
            }
            insertAt++;
        }
    }

    /// <summary>
    /// Past GefilterdeTypeLijsten aan via diff (nooit Clear) zodat de ComboBox
    /// zijn SelectedItem niet verliest tijdens een filter-wissel.
    /// </summary>
    public void ApplyTypeLijstFilter(string? zoekterm)
    {
        // Materialiseer de gewenste lijst één keer.
        System.Collections.Generic.List<TypeLijst> target;
        if (string.IsNullOrWhiteSpace(zoekterm))
        {
            target = new System.Collections.Generic.List<TypeLijst>(TypeLijsten);
        }
        else
        {
            var z = zoekterm.Trim().ToLowerInvariant();
            target = TypeLijsten
                .Where(x => x.Artikelnummer != null &&
                            x.Artikelnummer.ToLowerInvariant().Contains(z))
                .ToList();
        }

        var targetSet = new System.Collections.Generic.HashSet<TypeLijst>(target);

        // 1. Verwijder items die niet langer in de filter zitten (van achter naar voor
        //    zodat index-verschuivingen niet storen).
        for (int i = GefilterdeTypeLijsten.Count - 1; i >= 0; i--)
        {
            if (!targetSet.Contains(GefilterdeTypeLijsten[i]))
                GefilterdeTypeLijsten.RemoveAt(i);
        }

        // 2. Voeg ontbrekende items toe op de juiste positie (zelfde volgorde als TypeLijsten).
        //    Het geselecteerde item wordt nooit verwijderd → ComboBox behoudt SelectedItem.
        var currentSet = new System.Collections.Generic.HashSet<TypeLijst>(GefilterdeTypeLijsten);
        int insertAt = 0;
        foreach (var item in target)
        {
            if (!currentSet.Contains(item))
            {
                GefilterdeTypeLijsten.Insert(insertAt, item);
                currentSet.Add(item);
            }
            insertAt++;
        }
    }

    // ── Regel CRUD (via RelayCommand attributes in OfferteViewModel doorgestuurd) ──
    public void RegelToevoegen()
    {
        var r = new OfferteRegel { AantalStuks = 1, BreedteCm = 30, HoogteCm = 40 };
        Regels.Add(r);
        SelectedRegel = r;
    }

    public void RegelVerwijderen(OfferteRegel? regel)
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
            AantalStuks = s.AantalStuks, BreedteCm = s.BreedteCm, HoogteCm = s.HoogteCm,
            InlegBreedteCm = s.InlegBreedteCm, InlegHoogteCm = s.InlegHoogteCm,
            InlegTypeLijstId = s.InlegTypeLijst?.Id ?? s.InlegTypeLijstId, InlegTypeLijst = s.InlegTypeLijst,
            Titel = s.Titel, Opmerking = s.Opmerking,
            TypeLijstId = s.TypeLijst?.Id ?? s.TypeLijstId, TypeLijst = s.TypeLijst,
            GlasId = s.Glas?.Id ?? s.GlasId, Glas = s.Glas,
            PassePartout1Id = s.PassePartout1?.Id ?? s.PassePartout1Id, PassePartout1 = s.PassePartout1,
            PassePartout2Id = s.PassePartout2?.Id ?? s.PassePartout2Id, PassePartout2 = s.PassePartout2,
            DiepteKernId = s.DiepteKern?.Id ?? s.DiepteKernId, DiepteKern = s.DiepteKern,
            OpklevenId = s.Opkleven?.Id ?? s.OpklevenId, Opkleven = s.Opkleven,
            RugId = s.Rug?.Id ?? s.RugId, Rug = s.Rug,
            AfgesprokenPrijsExcl = s.AfgesprokenPrijsExcl, ExtraWerkMinuten = s.ExtraWerkMinuten,
            ExtraPrijs = s.ExtraPrijs, Korting = s.Korting, LegacyCode = s.LegacyCode,
            TotaalExcl = s.TotaalExcl, SubtotaalExBtw = s.SubtotaalExBtw,
            BtwBedrag = s.BtwBedrag, TotaalInclBtw = s.TotaalInclBtw
        };
        Regels.Add(r);
        SelectedRegel = r;
    }

    private async Task ApplyLegacyCodeAsync()
    {
        if (SelectedRegel is null) return;

        var code = (LegacyCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            await _dialogs.ShowErrorAsync("Legacy-code", "Geef een code in (6 tekens: G P P D O R).");
            return;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            await LegacyAfwerkingCode.ApplyAsync(db, SelectedRegel, code);

            SelectedRegel.GlasId           = SelectedRegel.Glas?.Id;
            SelectedRegel.PassePartout1Id  = SelectedRegel.PassePartout1?.Id;
            SelectedRegel.PassePartout2Id  = SelectedRegel.PassePartout2?.Id;
            SelectedRegel.DiepteKernId     = SelectedRegel.DiepteKern?.Id;
            SelectedRegel.OpklevenId       = SelectedRegel.Opkleven?.Id;
            SelectedRegel.RugId            = SelectedRegel.Rug?.Id;

            // Relink nav-props to CATALOG instances so the level-2 ComboBoxes
            // can match by reference (ApplyAsync uses a separate DB context).
            if (SelectedRegel.GlasId is int gid)          SelectedRegel.Glas          = GlasOpties.FirstOrDefault(x => x.Id == gid);          else SelectedRegel.Glas          = null;
            if (SelectedRegel.PassePartout1Id is int p1id) SelectedRegel.PassePartout1 = Passe1Opties.FirstOrDefault(x => x.Id == p1id); else SelectedRegel.PassePartout1 = null;
            if (SelectedRegel.PassePartout2Id is int p2id) SelectedRegel.PassePartout2 = Passe2Opties.FirstOrDefault(x => x.Id == p2id); else SelectedRegel.PassePartout2 = null;
            if (SelectedRegel.DiepteKernId is int did)     SelectedRegel.DiepteKern    = DiepteOpties.FirstOrDefault(x => x.Id == did);    else SelectedRegel.DiepteKern    = null;
            if (SelectedRegel.OpklevenId is int oid)       SelectedRegel.Opkleven      = OpkleefOpties.FirstOrDefault(x => x.Id == oid);  else SelectedRegel.Opkleven      = null;
            if (SelectedRegel.RugId is int rid)            SelectedRegel.Rug           = RugOpties.FirstOrDefault(x => x.Id == rid);      else SelectedRegel.Rug           = null;

            OnPropertyChanged(nameof(SelectedRegel));
            OnPropertyChanged(nameof(LegacyCode));
            RegelChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Toast.Error(ex.GetBaseException().Message);
            await _dialogs.ShowErrorAsync("Legacy-code toepassen mislukt", ex.GetBaseException().Message);
        }
    }

    private void GenerateLegacyCode()
    {
        if (SelectedRegel is null) return;
        LegacyCode = LegacyAfwerkingCode.Generate(SelectedRegel);
    }

    private async Task OpenTypeLijstAsync()
    {
        if (SelectedRegel?.TypeLijst is null) return;

        var owner = GetOwnerWindow();
        if (owner is null) return;

        var dialog = new LijstDialog(SelectedRegel.TypeLijst);
        var result = await dialog.ShowDialog<TypeLijst?>(owner);
        if (result is null) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.TypeLijsten.Attach(result);
            db.Entry(result).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            await db.SaveChangesAsync();

            // Update de in-memory catalogus-entry
            var idx = TypeLijsten.IndexOf(TypeLijsten.FirstOrDefault(t => t.Id == result.Id)!);
            if (idx >= 0)
                TypeLijsten[idx] = result;

            // Update alle regels die naar deze lijst verwijzen
            foreach (var r in Regels.Where(r => r.TypeLijst?.Id == result.Id))
                r.TypeLijst = result;

            // Update de geselecteerde regel zodat de UI refresht
            if (SelectedRegel.TypeLijst?.Id == result.Id)
            {
                SelectedRegel.TypeLijst = result;
                OnPropertyChanged(nameof(SelectedRegel));
            }

            Toast.Success($"Lijst '{result.Artikelnummer}' opgeslagen.");
        }
        catch (Exception ex)
        {
            Toast.Error(ex.GetBaseException().Message);
        }
    }

    private static Avalonia.Controls.Window? GetOwnerWindow()
    {
        if (App.Current?.ApplicationLifetime is not
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        return desktop.MainWindow;
    }
}
