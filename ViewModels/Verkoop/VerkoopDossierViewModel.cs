using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Verkoop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>US-90 — één chip "In deze verkoop": gestreept zolang het document niet bestaat.</summary>
public sealed record DossierChip(string Tekst, bool Bestaat)
{
    public bool BestaatNiet => !Bestaat;
}

/// <summary>US-90 — één stap in de stappenbalk van het dossier (klikbaar om terug te kijken).</summary>
public sealed record DossierStap(VerkoopStap Stap, string Titel, StapToestand Toestand, string? Onderschrift, bool IsBekeken)
{
    public int Nummer => (int)Stap;
    public bool IsKlaar => Toestand == StapToestand.Klaar;
    public bool IsHuidig => Toestand == StapToestand.Huidig;
    public bool IsOvergeslagen => Toestand == StapToestand.Overgeslagen;
    public bool IsNogTeDoen => Toestand == StapToestand.NogTeDoen;

    /// <summary>Enkel stappen die al bereikt zijn kan je bekijken (geen status terugzetten).</summary>
    public bool KanBekijken => !IsNogTeDoen;

    public string Bolletje => IsKlaar ? "✓" : IsOvergeslagen ? "⤼" : Nummer.ToString();
}

/// <summary>US-90 — één gegeven in de stapinhoud ("Werkbon" — "423 · Gepland").</summary>
public sealed record DossierGegeven(string Label, string Waarde);

/// <summary>
/// US-90 — het verkoopdossier: kop, documentchips, stappenbalk, de inhoud van de stap en een voetbalk.
/// De stappen zelf gebruiken (voorlopig) de bestaande schermen: de offerte, het werkbonvenster, de
/// planning en de bestelbonnen. Er verandert niets aan wat die schermen doen (grondregel 02/11).
/// Terugkijken naar een vorige stap is enkel bekijken: er wordt geen status teruggezet.
/// </summary>
public partial class VerkoopDossierViewModel : ObservableObject, IParameterReceiver<int>, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IVerkoopOverzichtService _overzicht;
    private readonly IVerkoopDossierService _dossier;
    private readonly INavigationService _nav;
    private readonly IOfferteNavigationService _offerteNav;
    private readonly IWorkflowService _workflow;
    private readonly IWerkBonWorkflowService _werkBonWorkflow;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;
    private readonly IOntvangstService? _ontvangsten;

    public VerkoopDossierViewModel(
        IDbContextFactory<AppDbContext> factory,
        IVerkoopOverzichtService overzicht,
        IVerkoopDossierService dossier,
        INavigationService nav,
        IOfferteNavigationService offerteNav,
        IWorkflowService workflow,
        IWerkBonWorkflowService werkBonWorkflow,
        IDialogService dialogs,
        IToastService toast,
        IOntvangstService? ontvangsten = null)
    {
        _factory = factory;
        _overzicht = overzicht;
        _dossier = dossier;
        _nav = nav;
        _offerteNav = offerteNav;
        _workflow = workflow;
        _werkBonWorkflow = werkBonWorkflow;
        _dialogs = dialogs;
        _toast = toast;
        _ontvangsten = ontvangsten;
    }

    public int OfferteId { get; private set; }

    // ── Kop ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private string titel = "";
    [ObservableProperty] private string klantRegel = "";
    [ObservableProperty] private string statusTekst = "";
    [ObservableProperty] private string totaalTekst = "";
    [ObservableProperty] private string nogTeBetalenTekst = "";
    [ObservableProperty] private bool isAllesBetaald;
    [ObservableProperty] private bool kanAnnuleren;

    public ObservableCollection<DossierChip> Chips { get; } = new();
    public ObservableCollection<DossierStap> Stappen { get; } = new();

    // ── Stap ─────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerugGekeken), nameof(StapTeller), nameof(KanVorigeStap),
        nameof(BekekenTitel), nameof(BekekenUitleg), nameof(IsHoofdKnopZichtbaar))]
    private VerkoopStap bekekenStap = VerkoopStap.Offerte;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTerugGekeken), nameof(IsHoofdKnopZichtbaar))]
    private VerkoopStapInfo? info;

    public bool IsTerugGekeken => Info is not null && BekekenStap != Info.Huidig;
    public string StapTeller => $"Stap {(int)BekekenStap} van 5";
    public bool KanVorigeStap => BekekenStap > VerkoopStap.Offerte;
    public string BekekenTitel => VerkoopStapResolver.Titel(BekekenStap);
    public string BekekenUitleg => VerkoopStapResolver.Uitleg(BekekenStap);

    /// <summary>Gegevens van de bekeken stap (uit offerte, werkbon en bestelbon).</summary>
    public ObservableCollection<DossierGegeven> Gegevens { get; } = new();

    /// <summary>Wat er nog ontbreekt (bv. om de productie te starten).</summary>
    public ObservableCollection<string> Meldingen { get; } = new();

    [ObservableProperty] private bool toontOfferteKnop;
    [ObservableProperty] private bool toontWerkbonKnoppen;
    [ObservableProperty] private bool toontBestelbonKnop;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHoofdKnopZichtbaar))]
    private string? hoofdKnopTekst;

    public bool IsHoofdKnopZichtbaar => !IsTerugGekeken && !string.IsNullOrEmpty(HoofdKnopTekst);

    // ── Geladen gegevens ─────────────────────────────────────────────────────
    private Offerte? _offerte;
    private Bestelbon? _bestelbon;
    private VerkoopOverzichtRegel? _regel;
    private decimal _ontvangen;

    public Task ReceiveAsync(int parameter)
    {
        OfferteId = parameter;
        return Task.CompletedTask;
    }

    public async Task InitializeAsync() => await LaadAsync(naarHuidigeStap: true);

    public async Task LaadAsync(bool naarHuidigeStap = false)
    {
        try
        {
            await using (var db = await _factory.CreateDbContextAsync())
            {
                _offerte = await db.Offertes.AsNoTracking()
                    .Include(o => o.Klant)
                    .Include(o => o.Regels)
                    .Include(o => o.WerkBon).ThenInclude(w => w!.Taken)
                    .FirstOrDefaultAsync(o => o.Id == OfferteId);

                if (_offerte is null)
                {
                    _toast.Error("Verkoop niet gevonden.");
                    return;
                }

                var werkBonId = _offerte.WerkBon?.Id;
                _bestelbon = await db.Bestelbonnen.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.OfferteId == OfferteId || (werkBonId != null && b.WerkBonId == werkBonId));
            }

            _regel = await _overzicht.GetRegelAsync(OfferteId);
            _ontvangen = await _dossier.OntvangenBedragAsync(OfferteId);
            if (_regel is null) return;

            Info = _regel.Stap;
            if (naarHuidigeStap || BekekenStap > Info.Huidig)
                BekekenStap = Info.Huidig;

            VulKop();
            VulStappen();
            await VulStapInhoudAsync();
        }
        catch (Exception ex)
        {
            _toast.Error($"Laden van de verkoop mislukt: {ex.Message}");
        }
    }

    private void VulKop()
    {
        var o = _offerte!;
        var k = o.Klant;
        Titel = $"Verkoop {o.OfferteNummer} · {k?.VolledigeNaam ?? "zonder klant"}";
        KlantRegel = KlantRegelTekst(k, _regel!.AfhaalDatum);
        StatusTekst = _regel.FaseTekst;
        TotaalTekst = $"€ {o.TotaalInclBtw:#,##0.00}";
        IsAllesBetaald = _regel.NogTeBetalen <= 0m;
        NogTeBetalenTekst = IsAllesBetaald ? "betaald" : $"nog te betalen € {_regel.NogTeBetalen:#,##0.00}";
        KanAnnuleren = Info is { IsGeannuleerd: false, IsAfgerond: false, IsAfgehaaldNogTeBetalen: false };

        Chips.Clear();
        foreach (var c in BouwChips(o.OfferteNummer, o.WerkBon is not null, _bestelbon, o.VoorschotBedrag))
            Chips.Add(c);
    }

    /// <summary>Adres · telefoon · afhalen (pure functie, testbaar).</summary>
    public static string KlantRegelTekst(Klant? k, DateTime? afhaalDatum)
    {
        var delen = new List<string>();
        if (k is not null)
        {
            var straat = $"{k.Straat} {k.Nummer}".Trim();
            var gemeente = $"{k.Postcode} {k.Gemeente}".Trim();
            var adres = string.Join(", ", new[] { straat, gemeente }.Where(s => s.Length > 0));
            if (adres.Length > 0) delen.Add(adres);
            if (!string.IsNullOrWhiteSpace(k.Telefoon)) delen.Add(k.Telefoon!);
            if (!string.IsNullOrWhiteSpace(k.Email)) delen.Add(k.Email!);
        }
        delen.Add(afhaalDatum is { } d ? $"afhalen {d:dd/MM/yyyy}" : "nog geen afhaaldatum");
        return string.Join(" · ", delen);
    }

    /// <summary>US-90 AC 2 — Offerte, Werkbon, Bestelbon, Voorschot; gestreept zolang ze niet bestaan.</summary>
    public static IReadOnlyList<DossierChip> BouwChips(int nummer, bool heeftWerkBon, Bestelbon? bestelbon, decimal voorschot)
    {
        return new[]
        {
            new DossierChip($"Offerte {nummer}", true),
            new DossierChip($"Werkbon {nummer}", heeftWerkBon),
            bestelbon is null
                ? new DossierChip("Bestelbon", false)
                : new DossierChip(bestelbon.VolgNr is null ? "Bestelbon voorlopig" : $"Bestelbon {bestelbon.BestelbonNummer}", true),
            voorschot > 0m
                ? new DossierChip($"Voorschot € {voorschot:#,##0.00}", true)
                : new DossierChip("Voorschot", false)
        };
    }

    private void VulStappen()
    {
        Stappen.Clear();
        foreach (var s in BouwStappen(Info!, BekekenStap, _offerte!.Datum, _offerte.WerkBon?.AangemaaktOp))
            Stappen.Add(s);
    }

    /// <summary>US-90 AC 3 — klaar (✓ + datum), huidig ("hier ben je"), overgeslagen, nog te doen.</summary>
    public static IReadOnlyList<DossierStap> BouwStappen(VerkoopStapInfo info, VerkoopStap bekeken, DateTime offerteDatum, DateTime? werkbonDatum)
    {
        return VerkoopStapResolver.AlleStappen.Select(s =>
        {
            var toestand = info.IsGeannuleerd ? StapToestand.NogTeDoen : info.ToestandVan(s);
            if (info.IsGeannuleerd && s <= info.Huidig) toestand = s == info.Huidig ? StapToestand.Huidig : StapToestand.Klaar;

            string? onder = toestand switch
            {
                StapToestand.Huidig when info.IsGeannuleerd => "geannuleerd",
                StapToestand.Huidig when s == bekeken => "hier ben je",
                StapToestand.Huidig => "huidige stap",
                StapToestand.Overgeslagen => "overgeslagen",
                StapToestand.Klaar when s == VerkoopStap.Offerte => offerteDatum.ToString("dd/MM"),
                StapToestand.Klaar when s == VerkoopStap.Werkbon && werkbonDatum is { } d => d.ToString("dd/MM"),
                _ => null
            };
            if (s == bekeken && toestand != StapToestand.Huidig) onder = "terug gekeken";

            return new DossierStap(s, VerkoopStapResolver.Titel(s), toestand, onder, s == bekeken);
        }).ToList();
    }

    private async Task VulStapInhoudAsync()
    {
        var o = _offerte!;
        var wb = o.WerkBon;
        Gegevens.Clear();
        Meldingen.Clear();
        ToontOfferteKnop = ToontWerkbonKnoppen = ToontBestelbonKnop = false;

        switch (BekekenStap)
        {
            case VerkoopStap.Offerte:
                Gegevens.Add(new("Offerte", $"{o.OfferteNummer} · {o.Status.Label()} · {o.Datum:dd/MM/yyyy}"));
                Gegevens.Add(new("Inlijstingen", o.Regels.Count == 1 ? "1 inlijsting" : $"{o.Regels.Count} inlijstingen"));
                Gegevens.Add(new("Totaal incl.", $"€ {o.TotaalInclBtw:#,##0.00}"));
                Gegevens.Add(new("Voorschot", o.VoorschotBedrag > 0m ? $"€ {o.VoorschotBedrag:#,##0.00}" : "geen"));
                if (_bestelbon is not null)
                    Gegevens.Add(new("Bestelbon", _bestelbon.VolgNr is null ? "voorlopig (nog zonder nummer)" : _bestelbon.BestelbonNummer));
                // Op stap 1 opent de hoofdknop de offerte al; bij terugkijken staat de knop hier.
                ToontOfferteKnop = IsTerugGekeken;
                break;

            case VerkoopStap.Werkbon:
            case VerkoopStap.InProductie:
                if (wb is null)
                {
                    Gegevens.Add(new("Werkbon", "nog niet aangemaakt"));
                    break;
                }
                Gegevens.Add(new("Werkbon", $"{o.OfferteNummer} · {wb.Status.Label()} · aangemaakt {wb.AangemaaktOp:dd/MM/yyyy}"));
                Gegevens.Add(new("Taken", PlanningTekst(wb)));
                if (wb.AantalTaken > 0)
                    Gegevens.Add(new("Bestellen", wb.BestelVoortgangLabel));
                Gegevens.Add(new("Afhalen", _regel!.AfhaalDatum is { } a ? a.ToString("dd/MM/yyyy") : "nog geen afhaaldatum"));
                if (BekekenStap == VerkoopStap.Werkbon && !IsTerugGekeken)
                    foreach (var m in await _dossier.WatOntbreektVoorProductieAsync(OfferteId))
                        Meldingen.Add(m);
                ToontWerkbonKnoppen = true;
                break;

            default:   // Klaar om af te halen, Afgehaald & betaald
                if (_bestelbon is null)
                    Gegevens.Add(new("Bestelbon", "nog niet aangemaakt"));
                else
                {
                    Gegevens.Add(new("Bestelbon", $"{_bestelbon.NummerLabel} · {_bestelbon.Status.Label()} · {_bestelbon.BestelbonDatum:dd/MM/yyyy}"));
                    Gegevens.Add(new("Totaal incl.", $"€ {_bestelbon.TotaalInclBtw:#,##0.00}"));
                }
                Gegevens.Add(new("Ontvangen", $"€ {_ontvangen:#,##0.00}"));
                Gegevens.Add(new("Nog te betalen", $"€ {Math.Max(0m, _regel!.NogTeBetalen):#,##0.00}"));
                if (wb is not null)
                    Gegevens.Add(new("Werkbon", $"{o.OfferteNummer} · {wb.Status.Label()}"));
                ToontBestelbonKnop = _bestelbon is not null;
                ToontWerkbonKnoppen = wb is not null;
                break;
        }

        if (Info!.IsGeannuleerd && !string.IsNullOrWhiteSpace(o.AnnuleerReden))
            Gegevens.Add(new("Geannuleerd", $"{o.GeannuleerdOp:dd/MM/yyyy} — {o.AnnuleerReden}"));
        if (o.OvergeslagenStappen != VerkoopStapFlags.Geen && !string.IsNullOrWhiteSpace(o.OverslaanReden))
            Gegevens.Add(new("Overgeslagen", o.OverslaanReden!));

        HoofdKnopTekst = HoofdKnopVoor(Info);
    }

    private static string PlanningTekst(WerkBon wb)
    {
        if (wb.AantalTaken == 0) return "nog niet gepland";
        var van = wb.Taken.Min(t => t.GeplandVan);
        var tot = wb.Taken.Max(t => t.GeplandTot);
        var min = wb.Taken.Sum(t => t.DuurMinuten);
        var taken = wb.AantalTaken == 1 ? "1 taak" : $"{wb.AantalTaken} taken";
        return van.Date == tot.Date
            ? $"{taken} · {van:dd/MM HH:mm} – {tot:HH:mm} · ± {min} min"
            : $"{taken} · {van:dd/MM} – {tot:dd/MM} · ± {min} min";
    }

    /// <summary>US-90 AC 5 — hoofdknop rechts in de voetbalk, per huidige stap.</summary>
    public static string? HoofdKnopVoor(VerkoopStapInfo info) => info switch
    {
        { IsGeannuleerd: true } or { IsAfgerond: true } => null,
        { IsAfgehaaldNogTeBetalen: true } => "Betaling registreren ▶",
        { Huidig: VerkoopStap.Offerte } => "Offerte openen ▶",
        { Huidig: VerkoopStap.Werkbon } => "Start productie ▶",
        { Huidig: VerkoopStap.InProductie } => "Klaar om af te halen ▶",
        { Huidig: VerkoopStap.KlaarOmAfTeHalen } => "Klant komt afhalen ▶",
        _ => null
    };

    // ── Stappenbalk / voetbalk ───────────────────────────────────────────────

    [RelayCommand]
    private async Task BekijkStapAsync(DossierStap? stap)
    {
        if (stap is null || !stap.KanBekijken) return;
        BekekenStap = stap.Stap;
        VulStappen();
        await VulStapInhoudAsync();
    }

    [RelayCommand]
    private async Task VorigeStapAsync()
    {
        if (!KanVorigeStap) return;
        BekekenStap -= 1;
        VulStappen();
        await VulStapInhoudAsync();
    }

    [RelayCommand]
    private async Task NaarHuidigeStapAsync()
    {
        if (Info is null) return;
        BekekenStap = Info.Huidig;
        VulStappen();
        await VulStapInhoudAsync();
    }

    [RelayCommand]
    private Task TerugNaarVerkopenAsync() => _nav.NavigateToAsync<OffertesLijstViewModel>();

    [RelayCommand]
    private async Task HoofdKnopAsync()
    {
        if (Info is null || IsTerugGekeken) return;
        try
        {
            switch (Info)
            {
                case { IsAfgehaaldNogTeBetalen: true }:
                    await OpenBestelbonnenAsync();
                    return;
                case { Huidig: VerkoopStap.Offerte }:
                    await OpenOfferteAsync();
                    return;
                case { Huidig: VerkoopStap.Werkbon }:
                    var ontbreekt = await _dossier.WatOntbreektVoorProductieAsync(OfferteId);
                    if (ontbreekt.Count > 0)
                    {
                        _toast.Warning(string.Join(" ", ontbreekt));
                        return;
                    }
                    await _dossier.StartProductieAsync(OfferteId);
                    _toast.Success("De productie is gestart.");
                    break;
                case { Huidig: VerkoopStap.InProductie }:
                    if (!await _dialogs.ConfirmAsync("Klaar om af te halen?",
                            "De werkbon gaat naar \"Klaar om af te halen\": gereserveerde stukken worden verbruikt en de bestelbon wordt aangemaakt of bijgewerkt (zoals nu)."))
                        return;
                    var nr = await _dossier.MeldKlaarAsync(OfferteId);
                    _toast.Success(string.IsNullOrWhiteSpace(nr) ? "Klaar om af te halen." : $"Klaar om af te halen. Bestelbon {nr}.");
                    break;
                case { Huidig: VerkoopStap.KlaarOmAfTeHalen }:
                    // Afhalen en afrekenen gebeurt zoals nu in het werkbonvenster (US-76).
                    await OpenWerkBonAsync();
                    return;
            }
            await LaadAsync(naarHuidigeStap: true);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    // ── Bestaande schermen ───────────────────────────────────────────────────

    [RelayCommand]
    private Task OpenOfferteAsync() => _offerteNav.OpenOfferteAsync(OfferteId);

    [RelayCommand]
    private async Task OpenWerkBonAsync()
    {
        if (_offerte?.WerkBon is not { } wb) return;
        var vm = new WerkBonDetailViewModel(_factory, _workflow, _offerteNav, _toast, wb.Id, _ontvangsten);
        await vm.LoadAsync();
        var window = new QuadroApp.Views.WerkBonDetailWindow { DataContext = vm };
        if (EigenaarVenster() is { } owner)
            await window.ShowDialog(owner);
        await LaadAsync(naarHuidigeStap: true);
    }

    [RelayCommand]
    private async Task OpenPlanningAsync()
    {
        if (_offerte?.WerkBon is not { } wb) return;
        var vm = new PlanningCalendarViewModel(_factory, _werkBonWorkflow, _toast, _workflow);
        await vm.InitializeAsync(wb.Id);
        var window = new QuadroApp.Views.PlanningCalendarWindow { DataContext = vm };
        if (EigenaarVenster() is { } owner)
            await window.ShowDialog(owner);
        await LaadAsync();
    }

    [RelayCommand]
    private Task OpenBestelbonnenAsync() => _nav.NavigateToAsync<BestelbonnenViewModel>();

    // ── Menu ⋯ ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AnnulerenAsync()
    {
        if (!KanAnnuleren || _offerte is null) return;

        var vm = new AnnuleerVerkoopDialogViewModel(_offerte.OfferteNummer, _ontvangen, _bestelbon?.VolgNr is null ? null : _bestelbon.BestelbonNummer);
        if (EigenaarVenster() is not { } owner) return;
        var dialog = new QuadroApp.Views.AnnuleerVerkoopDialog { DataContext = vm };
        vm.RequestClose = ok => dialog.Close(ok);
        if (!await dialog.ShowDialog<bool>(owner)) return;

        try
        {
            await _dossier.AnnuleerAsync(OfferteId, vm.ToOpdracht());
            _toast.Success($"Verkoop {_offerte.OfferteNummer} is geannuleerd.");
            await LaadAsync(naarHuidigeStap: true);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    private static Avalonia.Controls.Window? EigenaarVenster()
        => App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow
            : null;
}
