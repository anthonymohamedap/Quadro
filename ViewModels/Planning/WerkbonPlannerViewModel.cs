using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>US-92 — één werkdag in de planner van stap 2 (2 weken of maand).</summary>
public sealed partial class PlannerDag : ObservableObject
{
    private static readonly CultureInfo Nl = new("nl-BE");

    public DateTime Datum { get; init; }
    public int GeplandMinuten { get; init; }
    public IReadOnlyList<string> TakenDezeVerkoop { get; init; } = Array.Empty<string>();

    /// <summary>Klanten van het andere werk die dag (zoals de kalender ze op de dagtegel toonde).</summary>
    public IReadOnlyList<string> AnderWerk { get; init; } = Array.Empty<string>();
    public bool HeeftAnderWerk => AnderWerk.Count > 0;
    public string AnderWerkTekst => AnderWerk.Count <= 3 ? string.Join(", ", AnderWerk) : $"{string.Join(", ", AnderWerk.Take(3))} +{AnderWerk.Count - 3}";
    public bool IsGeblokkeerd { get; init; }
    public bool IsAfhaaldag { get; init; }

    public string Kop => Datum.ToString("ddd d/MM", Nl);
    public string UrenTekst => IsGeblokkeerd ? "geblokkeerd" : $"{GeplandMinuten / 60.0:0.#} u · {(int)Math.Round(Bezetting * 100)}%";

    // Zelfde drempels als de kalender (DayTile): ≤50% rustig, ≤75% midden, ≤90% hoog, anders vol.
    public bool CapLow => !IsGeblokkeerd && Bezetting <= 0.5;
    public bool CapMid => !IsGeblokkeerd && Bezetting > 0.5 && Bezetting <= 0.75;
    public bool CapHigh => !IsGeblokkeerd && Bezetting > 0.75 && Bezetting <= 0.9;
    public bool CapFull => IsGeblokkeerd || Bezetting > 0.9;
    public double Bezetting => IsGeblokkeerd ? 1 : Math.Clamp(GeplandMinuten / (double)PlanningUitvoeringViewModel.CapaciteitMinuten, 0, 1);
    public bool IsVandaag => Datum.Date == DateTime.Today;
    public bool IsWeekend => Datum.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public bool IsVerleden => Datum.Date < DateTime.Today;
    public bool IsVol => GeplandMinuten >= PlanningUitvoeringViewModel.CapaciteitMinuten;
    public bool HeeftTakenDezeVerkoop => TakenDezeVerkoop.Count > 0;
    public string TakenTekst => string.Join(" · ", TakenDezeVerkoop);
    public bool KanPlannen => !IsVerleden && !IsGeblokkeerd;

    [ObservableProperty] private bool isGeselecteerd;
}

/// <summary>US-92 — één inlijsting van de werkbon in stap 2, met planstatus en bestelstatus (zoals de werkbon).</summary>
public sealed partial class PlannerRegel : ObservableObject
{
    public int RegelId { get; init; }
    public string Kop { get; init; } = "";
    public string Omschrijving { get; init; } = "";
    public int DuurMinuten { get; init; }
    public string? Opmerking { get; init; }
    public WerkTaak? Taak { get; init; }

    /// <summary>
    /// Alle delen van deze inlijsting: de planning verdeelt een lange inlijsting over meerdere dagen
    /// ("deel 1/2", "deel 2/2"). Herplannen en ✕ werken per deel, zoals in de kalender.
    /// </summary>
    public IReadOnlyList<WerkTaak> Taken { get; init; } = Array.Empty<WerkTaak>();
    public bool HeeftMeerdereDelen => Taken.Count > 1;

    /// <summary>Gepland in één deel: herplannen en ✕ naast de status (meerdere delen: per deel).</summary>
    public bool IsEnkelGepland => Taken.Count == 1;
    public IEnumerable<PlannerDeel> Delen => Taken.Select((t, i) => new PlannerDeel(this, t, i + 1, Taken.Count));

    /// <summary>Zelfde selectie-object als de kalender (Plan op dag / Plan per regel gebruiken het).</summary>
    public RegelPlanItem? Item { get; init; }

    public string DuurTekst => $"± {DuurMinuten} min";
    public bool IsGepland => Taak is not null;
    public string PlanTekst => Taken.Count switch
    {
        0 => "nog niet gepland",
        1 => $"gepland {Taken[0].GeplandVan.ToString("ddd d/MM", new CultureInfo("nl-BE"))}",
        _ => $"gepland in {Taken.Count} delen"
    };

    /// <summary>Bestelstatus van de taak (zoals in het werkbonvenster); leeg zolang er niets gepland is.</summary>
    public string? BestelTekst => Taken.Count == 0 ? null
        : Taken.All(t => t.IsOpVoorraad) ? "uit voorraad"
        : IsBestelOk ? "besteld"
        : "te bestellen";
    public bool HeeftBestelTekst => BestelTekst is not null;
    public bool IsTeBestellen => Taken.Any(t => !t.IsOpVoorraad && !t.IsBesteld);
    public bool IsBestelOk => Taken.Count > 0 && !IsTeBestellen;
    public bool HeeftOpmerking => !string.IsNullOrWhiteSpace(Opmerking);

    public bool IsSelected
    {
        get => Item?.IsSelected ?? false;
        set
        {
            if (Item is null || Item.IsSelected == value) return;
            Item.IsSelected = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>US-92 — één deel van een over meerdere dagen verdeelde inlijsting.</summary>
public sealed record PlannerDeel(PlannerRegel Regel, WerkTaak Taak, int Nummer, int Aantal)
{
    public string Tekst => $"deel {Nummer}/{Aantal}: {Taak.GeplandVan.ToString("ddd d/MM", new CultureInfo("nl-BE"))} · {Taak.DuurMinuten} min";
}

/// <summary>
/// US-92 — de planner van stap 2 in het verkoopdossier (mock stap2.html): links de werkdagen
/// (2 weken of maand) met de bezetting en het werk van deze verkoop, rechts de inlijstingen van de
/// werkbon. Alle acties gaan via dezelfde kalender/uitvoering als de pagina Planning (8 u per dag,
/// niet in het verleden, Plan op dag, Plan per regel, slepen, herplannen, taak verwijderen), dus de
/// planlogica verandert niet.
/// </summary>
public partial class WerkbonPlannerViewModel : ObservableObject
{
    private static readonly CultureInfo Nl = new("nl-BE");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly int? _werkBonId;

    /// <summary>Vanuit een werkbon (dossier, Werkbonnen, Verkopen, offerte): met de inlijstingen rechts.</summary>
    public bool HeeftWerkBon => _werkBonId is not null;
    public int PlannerKolommen => HeeftWerkBon ? 1 : 2;

    public WerkbonPlannerViewModel(PlanningCalendarViewModel kalender, IDbContextFactory<AppDbContext> factory, int? werkBonId)
    {
        Kalender = kalender;
        _factory = factory;
        _werkBonId = werkBonId;
        startMaandag = Maandag(DateTime.Today);
        geselecteerdeDag = VolgendeWerkdag(DateTime.Today);
        Kalender.SelectedDate = geselecteerdeDag;
        // Herplannen/verwijderen in de dagdetails of blokkeren laat de kalender herladen: dan ook de planner.
        Kalender.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PlanningCalendarViewModel.KpiUtilWeek) or nameof(PlanningCalendarViewModel.KpiBlokdagen)
                or nameof(PlanningCalendarViewModel.KpiOpenWerkbonnen) or nameof(PlanningCalendarViewModel.KpiBeschikbareUren))
                _ = HerlaadNaKalenderAsync();
        };
    }

    private bool _herlaadt;

    private async Task HerlaadNaKalenderAsync()
    {
        if (_herlaadt) return;
        _herlaadt = true;
        try
        {
            await Task.Delay(250);
            await LaadAsync();
        }
        catch { /* niet fataal: volgende actie herlaadt opnieuw */ }
        finally { _herlaadt = false; }
    }

    public PlanningCalendarViewModel Kalender { get; }

    /// <summary>"Bestellen…" bij een inlijsting: opent het bestellen zoals nu (werkbonvenster). Gezet door het dossier.</summary>
    public IAsyncRelayCommand? BestellenCommand { get; set; }

    public ObservableCollection<PlannerDag> Dagen { get; } = new();
    public ObservableCollection<PlannerRegel> Regels { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaand), nameof(PeriodeTekst))]
    private bool isTweeWeken = true;

    public bool IsMaand => !IsTweeWeken;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodeTekst))]
    private DateTime startMaandag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanOpDagTekst))]
    private DateTime geselecteerdeDag;

    [ObservableProperty] private string selectieTekst = "Selecteer inlijstingen en kies een dag, of sleep ze naar de kalender.";
    [ObservableProperty] private bool heeftSelectie;
    [ObservableProperty] private string kopRechts = "";

    public string PlanOpDagTekst => $"Plan op {GeselecteerdeDag.ToString("ddd d/MM", Nl)}";

    public string PeriodeTekst => IsTweeWeken
        ? $"{StartMaandag:d/MM} – {StartMaandag.AddDays(13):d/MM}"
        : Nl.TextInfo.ToTitleCase(StartMaandag.AddDays(7).ToString("MMMM yyyy", Nl));

    private DateTime? _afhaalDatum;

    public static DateTime Maandag(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static DateTime VolgendeWerkdag(DateTime d)
    {
        var x = d.Date;
        while (x.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) x = x.AddDays(1);
        return x;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    public async Task LaadAsync()
    {
        var (van, tot) = Periode();
        await using var db = await _factory.CreateDbContextAsync();

        int? offerteId = _werkBonId is int wbId
            ? await db.WerkBonnen.Where(w => w.Id == wbId).Select(w => (int?)w.OfferteId).FirstOrDefaultAsync()
            : null;
        _afhaalDatum = offerteId is int oid
            ? await db.Offertes.Where(o => o.Id == oid).Select(o => o.AfhaalDatum).FirstOrDefaultAsync()
            : null;

        var taken = await db.WerkTaken.AsNoTracking()
            .Where(t => t.GeplandVan >= van && t.GeplandVan < tot)
            .Select(t => new
            {
                t.GeplandVan, t.DuurMinuten, t.WerkBonId, t.Omschrijving,
                Klant = t.WerkBon != null && t.WerkBon.Offerte != null && t.WerkBon.Offerte.Klant != null
                    ? t.WerkBon.Offerte.Klant.Achternaam : null
            })
            .ToListAsync();
        var geblokkeerd = (await db.GeblokkeerDagen.AsNoTracking()
                .Where(g => g.Datum >= van && g.Datum < tot).Select(g => g.Datum).ToListAsync())
            .Select(d => d.Date).ToHashSet();

        Dagen.Clear();
        for (var d = van; d < tot; d = d.AddDays(1))
        {
            // Ook zaterdag en zondag: de planning kan daar werk zetten (zoals in de kalender).
            var vandaag = taken.Where(t => t.GeplandVan.Date == d).ToList();
            Dagen.Add(new PlannerDag
            {
                Datum = d,
                GeplandMinuten = vandaag.Sum(t => t.DuurMinuten),
                TakenDezeVerkoop = vandaag.Where(t => t.WerkBonId == _werkBonId).Select(t => t.Omschrijving).ToList(),
                AnderWerk = vandaag.Where(t => t.WerkBonId != _werkBonId)
                    .Select(t => string.IsNullOrWhiteSpace(t.Klant) ? "zonder klant" : t.Klant!).Distinct().ToList(),
                IsGeblokkeerd = geblokkeerd.Contains(d),
                IsAfhaaldag = _afhaalDatum?.Date == d,
                IsGeselecteerd = d == GeselecteerdeDag.Date
            });
        }

        if (offerteId is int id)
            await LaadRegelsAsync(db, id);
    }

    private async Task LaadRegelsAsync(AppDbContext db, int offerteId)
    {
        var regels = await db.OfferteRegels.AsNoTracking()
            .Include(r => r.TypeLijst).Include(r => r.KantKlaarKader)
            .Include(r => r.Glas).Include(r => r.GlasVariant)
            .Include(r => r.PassePartout1).Include(r => r.PassePartout1Variant)
            .Include(r => r.PassePartout2).Include(r => r.PassePartout2Variant)
            .Include(r => r.DiepteKern).Include(r => r.DiepteKernVariant)
            .Include(r => r.Opkleven).Include(r => r.OpklevenVariant)
            .Include(r => r.Rug).Include(r => r.RugVariant)
            .Where(r => r.OfferteId == offerteId)
            .OrderBy(r => r.Id)
            .ToListAsync();
        var taken = await db.WerkTaken.AsNoTracking()
            .Where(t => t.WerkBonId == _werkBonId)
            .OrderBy(t => t.GeplandVan)
            .ToListAsync();

        foreach (var oud in Regels) oud.PropertyChanged -= OpRegelGewijzigd;
        Regels.Clear();
        foreach (var r in regels)
        {
            var delen = taken.Where(t => t.OfferteRegelId == r.Id).ToList();
            var taak = delen.FirstOrDefault();
            var item = Kalender.RegelsVanWerkBon.FirstOrDefault(i => i.RegelId == r.Id);
            if (taak is not null && item is not null) item.IsSelected = false;   // gepland → niet meer geselecteerd
            var lijst = r.KantKlaarKader is not null ? r.StandaardkaderLabel
                : r.TypeLijst is null ? "geen lijst" : $"{r.TypeLijst.Artikelnummer} · {r.TypeLijst.Soort}";
            var afwerking = r.AfwerkingSamenvatting;
            var regel = new PlannerRegel
            {
                RegelId = r.Id,
                Kop = $"{(string.IsNullOrWhiteSpace(r.Titel) ? "Inlijsting" : r.Titel)} · {r.BreedteCm:0.#} × {r.HoogteCm:0.#}"
                      + (r.AantalStuks > 1 ? $" ({r.AantalStuks}×)" : ""),
                Omschrijving = string.IsNullOrWhiteSpace(afwerking) ? lijst : $"{lijst} · {afwerking}",
                DuurMinuten = delen.Count > 0 ? delen.Sum(t => t.DuurMinuten) : PlanningUitvoeringViewModel.CalcMinutenVoorRegel(r),
                Opmerking = r.Opmerking,
                Taak = taak,
                Taken = delen,
                Item = item
            };
            regel.PropertyChanged += OpRegelGewijzigd;
            Regels.Add(regel);
        }

        var open = Regels.Count(r => !r.IsGepland);
        KopRechts = Regels.Count == 0 ? "" : open == 0 ? "alles ingepland" : open == 1 ? "1 nog te plannen" : $"{open} nog te plannen";
        WerkSelectieBij();
    }

    private void OpRegelGewijzigd(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannerRegel.IsSelected)) WerkSelectieBij();
    }

    private void WerkSelectieBij()
    {
        var gekozen = Regels.Where(r => r.IsSelected).ToList();
        HeeftSelectie = gekozen.Count > 0;
        SelectieTekst = gekozen.Count == 0
            ? "Selecteer inlijstingen en kies een dag, of sleep ze naar de kalender."
            : $"{gekozen.Count} geselecteerd · ± {gekozen.Sum(r => r.DuurMinuten)} min";
    }

    private (DateTime Van, DateTime Tot) Periode()
    {
        if (IsTweeWeken) return (StartMaandag, StartMaandag.AddDays(14));
        // Maand: van de maandag van de week met de 1e tot na de week met de laatste dag.
        var referentie = StartMaandag.AddDays(7);
        var eerste = new DateTime(referentie.Year, referentie.Month, 1);
        var laatste = eerste.AddMonths(1).AddDays(-1);
        return (Maandag(eerste), Maandag(laatste).AddDays(7));
    }

    // ── Weergave en navigatie ────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToonTweeWekenAsync() { IsTweeWeken = true; StartMaandag = Maandag(GeselecteerdeDag); await LaadAsync(); }

    [RelayCommand]
    private async Task ToonMaandAsync() { IsTweeWeken = false; StartMaandag = Maandag(GeselecteerdeDag); await LaadAsync(); }

    [RelayCommand]
    private async Task VorigeAsync()
    {
        StartMaandag = IsTweeWeken ? StartMaandag.AddDays(-14) : Maandag(StartMaandag.AddDays(7).AddMonths(-1));
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VolgendeAsync()
    {
        StartMaandag = IsTweeWeken ? StartMaandag.AddDays(14) : Maandag(StartMaandag.AddDays(7).AddMonths(1));
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VandaagAsync()
    {
        StartMaandag = Maandag(DateTime.Today);
        KiesDag(VolgendeWerkdag(DateTime.Today));
        await LaadAsync();
    }

    [RelayCommand]
    private void KiesDag(PlannerDag? dag)
    {
        if (dag is null) return;
        KiesDag(dag.Datum);
    }

    private void KiesDag(DateTime datum)
    {
        GeselecteerdeDag = datum.Date;
        Kalender.SelectedDate = datum.Date;
        foreach (var d in Dagen) d.IsGeselecteerd = d.Datum == datum.Date;
    }

    // ── Plannen (zelfde uitvoering als de pagina Planning) ───────────────────

    [RelayCommand]
    private async Task PlanOpDagAsync()
    {
        Kalender.SelectedDate = GeselecteerdeDag;
        await Kalender.PlanGeselecteerdeRegelsCommand.ExecuteAsync(null);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task PlanPerRegelAsync()
    {
        await Kalender.PlanPerRegelCommand.ExecuteAsync(null);
        await LaadAsync();
    }

    /// <summary>Slepen: één inlijsting op een dag laten vallen.</summary>
    public async Task PlanRegelOpDagAsync(int regelId, DateTime datum)
    {
        await Kalender.PlanRegelOpDatumAsync(regelId, datum);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task HerplanDeelAsync(PlannerDeel? deel)
    {
        if (deel is null) return;
        await Kalender.HerplanTaakCommand.ExecuteAsync(deel.Taak);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VerwijderDeelAsync(PlannerDeel? deel)
    {
        if (deel is null) return;
        await Kalender.VerwijderTaakCommand.ExecuteAsync(deel.Taak);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task HerplanAsync(PlannerRegel? regel)
    {
        if (regel?.Taak is null) return;
        await Kalender.HerplanTaakCommand.ExecuteAsync(regel.Taak);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VerwijderTaakAsync(PlannerRegel? regel)
    {
        if (regel?.Taak is null) return;
        await Kalender.VerwijderTaakCommand.ExecuteAsync(regel.Taak);
        await LaadAsync();
    }

    [RelayCommand]
    private Task WeeklijstAfdrukkenAsync() => Kalender.OpenWeekWerkLijstCommand.ExecuteAsync(null);
}
