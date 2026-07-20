using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Toast;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class WeekWerkLijstViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IWorkflowService _workflow;
    private readonly IToastService? _toast;
    private readonly Microsoft.Extensions.Logging.ILogger<WeekWerkLijstViewModel>? _logger;

    [ObservableProperty] private int year;
    [ObservableProperty] private int weekNr;
    [ObservableProperty] private string title = "";

    [ObservableProperty] private ObservableCollection<KlantWeekBlock> blocks = new();

    /// <summary>Toast-meldingen voor de overlay in WeekWerkLijstWindow (zelfde singleton
    /// als StockService gebruikt, dus succes-/foutmeldingen van het bestellen verschijnen hier).</summary>
    public ReadOnlyObservableCollection<ToastMessage>? ToastMessages => _toast?.Messages;

    public WeekWerkLijstViewModel(IDbContextFactory<AppDbContext> factory, IWorkflowService workflow, IToastService? toast = null, Microsoft.Extensions.Logging.ILogger<WeekWerkLijstViewModel>? logger = null)
    {
        _factory = factory;
        _workflow = workflow;
        _toast = toast;
        _logger = logger;
    }

    public async Task InitializeAsync(int year, int weekNr)
    {
        Year = year;
        WeekNr = weekNr;
        Title = $"weekbezetting {weekNr}";
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var start = ISOWeek.ToDateTime(Year, WeekNr, DayOfWeek.Monday).Date;
        var end = start.AddDays(7);

        var taken = await db.WerkTaken
            .Include(t => t.WerkBon)
                .ThenInclude(w => w.Offerte)
                    .ThenInclude(o => o.Klant)
            .Include(t => t.OfferteRegel)
                .ThenInclude(r => r!.TypeLijst)
                    .ThenInclude(l => l!.Leverancier)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.Glas)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout1)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout2)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.DiepteKern)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.Opkleven)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.Rug)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.GlasVariant)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout1Variant)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout2Variant)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.DiepteKernVariant)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.OpklevenVariant)
            .Include(t => t.OfferteRegel).ThenInclude(r => r!.RugVariant)
            .Include(t => t.LeverancierBestelLijn)
                .ThenInclude(l => l!.LeverancierBestelling)
            .Where(t => t.GeplandVan >= start && t.GeplandVan < end)
            .OrderBy(t => t.WerkBonId)
            .ThenBy(t => t.GeplandVan)
            .ToListAsync();

        var grouped = taken
            .GroupBy(t => t.WerkBon?.Offerte?.Klant?.Achternaam?.ToUpperInvariant() ?? "ONBEKEND")
            .OrderBy(g => g.Key);

        Blocks.Clear();

        foreach (var g in grouped)
        {
            var block = new KlantWeekBlock
            {
                KlantNaam = g.Key,
                Items = new ObservableCollection<WeekWerkItem>(
                    g.Select(t => WeekWerkItem.FromTaak(t))
                )
            };
            Blocks.Add(block);
        }
    }


    [RelayCommand]
    private async Task MarkeerBesteldAsync(WeekWerkItem item)
    {
        if (item is null) return;

        try
        {
            var bestelDatum = DateTime.Today;
            await _workflow.MarkLijstAsBesteldAsync(item.TaakId, bestelDatum, item.GeselecteerdeBestelVorm);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            // Zonder deze afhandeling verdween de fout stil (geen UI-wijziging zichtbaar).
            _toast?.Error(ex.GetBaseException().Message);
        }
    }

    /// <summary>Herlaadt de weeklijst — pikt bestellingen op die elders (bv. in het
    /// leveranciersscherm) zijn geplaatst, zodat de status hier mee verandert.</summary>
    [RelayCommand]
    private async Task VerversAsync() => await LoadAsync();

    // ── PDF afdrukken ──────────────────────────────────────────────────────────
    [RelayCommand]
    private void PrintPdf()
    {
        try
        {
            var exporter = new PdfWeekLijstExporter();
            var path = exporter.Export(Year, WeekNr, Blocks);

            if (!File.Exists(path))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // US-31: gestructureerd loggen + gebruiker informeren via toast.
            if (_logger is not null)
                Microsoft.Extensions.Logging.LoggerExtensions.LogError(_logger, ex, "[WeekLijst PDF] Fout: {Message}", ex.Message);
            _toast?.Error($"PDF openen mislukt: {ex.Message}");
        }
    }

    // Save 1 notitie (per taak)
    [RelayCommand]
    private async Task SaveNotitieAsync(WeekWerkItem item)
    {
        if (item == null) return;

        await using var db = await _factory.CreateDbContextAsync();
        var taak = await db.WerkTaken.FirstOrDefaultAsync(x => x.Id == item.TaakId);
        if (taak == null) return;

        taak.WeekNotitie = item.Notitie; // <-- nieuwe kolom
        await db.SaveChangesAsync();
    }
}

public class KlantWeekBlock
{
    public string KlantNaam { get; set; } = "";
    public ObservableCollection<WeekWerkItem> Items { get; set; } = new();
}

public partial class WeekWerkItem : ObservableObject
{
    public int TaakId { get; init; }
    public int BonNr { get; init; }           // WerkBon.Id
    public int Stuks { get; init; }
    public decimal Breedte { get; init; }
    public decimal Hoogte { get; init; }
    public string Omschrijving { get; init; } = "";
    public string Afw { get; init; } = "";    // legacy / afw code
    public string Lijst { get; init; } = "";  // TypeLijst.Artikelnummer
    public string Inleg1 { get; init; } = "";
    public string Inleg2 { get; init; } = "";
    public DateTime ProductieDatum { get; init; } // GeplandVan datum

    /// <summary>Klant-/regelopmerking van de offerteregel (los van de werktaak-omschrijving).</summary>
    public string? RegelOpmerking { get; init; }

    // Afwerkings beschrijvingen — volledig leesbaar voor op de werkbon
    public string? GlasBeschrijving { get; init; }
    public string? Passe1Beschrijving { get; init; }
    public string? Passe2Beschrijving { get; init; }
    public string? DieptyeBeschrijving { get; init; }
    public string? OpklevenBeschrijving { get; init; }
    public string? RugBeschrijving { get; init; }

    [ObservableProperty] private string? notitie;
    [ObservableProperty] private bool isBesteld;
    [ObservableProperty] private DateTime? bestelDatum;
    [ObservableProperty] private bool isOpVoorraad;
    [ObservableProperty] private DateTimeOffset? bestelDatumInput;
    [ObservableProperty] private VoorraadStatus voorraadStatus;
    [ObservableProperty] private string voorraadStatusText = string.Empty;
    [ObservableProperty] private string? bestellingNummer;
    [ObservableProperty] private DateTime? verwachteLeverdatum;
    [ObservableProperty] private string? leverancierNaam;
    [ObservableProperty] private BestelVorm geselecteerdeBestelVorm = BestelVorm.Verstek;

    // RadioButton helpers
    public bool BestelVormIsVerstek
    {
        get => GeselecteerdeBestelVorm == BestelVorm.Verstek;
        set { if (value) GeselecteerdeBestelVorm = BestelVorm.Verstek; }
    }
    public bool BestelVormIsInLengte
    {
        get => GeselecteerdeBestelVorm == BestelVorm.InLengte;
        set { if (value) GeselecteerdeBestelVorm = BestelVorm.InLengte; }
    }
    public bool BestelVormIsGemonteerd
    {
        get => GeselecteerdeBestelVorm == BestelVorm.Gemonteerd;
        set { if (value) GeselecteerdeBestelVorm = BestelVorm.Gemonteerd; }
    }

    partial void OnGeselecteerdeBestelVormChanged(BestelVorm value)
    {
        OnPropertyChanged(nameof(BestelVormIsVerstek));
        OnPropertyChanged(nameof(BestelVormIsInLengte));
        OnPropertyChanged(nameof(BestelVormIsGemonteerd));
        OnPropertyChanged(nameof(BestelEenheid));
    }

    /// <summary>Afgeleide eenheid uit de bestelvorm: Verstek &amp; In lengte → "m", Gemonteerd → "stuks".</summary>
    public string BestelEenheid => GeselecteerdeBestelVorm == BestelVorm.Gemonteerd ? "stuks" : "m";

    public bool CanPlaceOrder => !IsBesteld;
    public bool HasOrder => !string.IsNullOrWhiteSpace(BestellingNummer);

    public static WeekWerkItem FromTaak(WerkTaak t)
    {
        var r = t.OfferteRegel;
        var bestelling = t.LeverancierBestelLijn?.LeverancierBestelling;

        return new WeekWerkItem
        {
            TaakId = t.Id,
            BonNr = t.WerkBonId,
            Stuks = r?.AantalStuks ?? 0,
            Breedte = r?.BreedteCm ?? 0,
            Hoogte = r?.HoogteCm ?? 0,
            Omschrijving = t.Omschrijving ?? "",
            Afw = r?.LegacyCode ?? "",
            Lijst = r?.TypeLijst?.Artikelnummer ?? "",
            Inleg1 = $"{r?.InlegBreedteCm}×{r?.InlegHoogteCm}",
            Inleg2 = "",
            ProductieDatum = t.GeplandVan.Date,
            RegelOpmerking = r?.Opmerking,
            GlasBeschrijving      = AfwLabel(r?.Glas, r?.GlasVariant),
            Passe1Beschrijving    = AfwLabel(r?.PassePartout1, r?.PassePartout1Variant),
            Passe2Beschrijving    = AfwLabel(r?.PassePartout2, r?.PassePartout2Variant),
            DieptyeBeschrijving   = AfwLabel(r?.DiepteKern, r?.DiepteKernVariant),
            OpklevenBeschrijving  = AfwLabel(r?.Opkleven, r?.OpklevenVariant),
            RugBeschrijving       = AfwLabel(r?.Rug, r?.RugVariant),
            Notitie = t.WeekNotitie,
            // Besteld als de taak-vlag gezet is OF er een gekoppelde bestellijn bestaat
            // (afgeleid uit data — geen migratie nodig).
            IsBesteld = t.IsBesteld || t.LeverancierBestelLijn != null,
            BestelDatum = t.BestelDatum,
            IsOpVoorraad = t.IsOpVoorraad,
            BestelDatumInput = (t.BestelDatum ?? DateTime.Today),
            VoorraadStatus = t.VoorraadStatus,
            VoorraadStatusText = GetVoorraadStatusText(t),
            BestellingNummer = bestelling?.BestelNummer,
            VerwachteLeverdatum = bestelling?.VerwachteLeverdatum,
            LeverancierNaam = r?.TypeLijst?.Leverancier?.Naam,
            GeselecteerdeBestelVorm = t.LeverancierBestelLijn?.BestelVorm ?? BestelVorm.Verstek
        };
    }

    /// <summary>Leesbaar label: "Mat Glas — Brons" (met variant) of "Mat Glas" / null.</summary>
    private static string? AfwLabel(AfwerkingsOptie? o, AfwerkingsVariant? v = null)
    {
        if (o is null) return null;
        var bs = v?.Beschrijving?.Trim();
        if (!string.IsNullOrEmpty(bs) && !bs.Equals("Standaard", StringComparison.OrdinalIgnoreCase))
            return $"{o.Naam} — {bs}";
        var kleur = o.Kleur?.Trim();
        return string.IsNullOrEmpty(kleur) || kleur.Equals("Standaard", StringComparison.OrdinalIgnoreCase)
            ? o.Naam
            : $"{o.Naam} ({kleur})";
    }

    private static string GetVoorraadStatusText(WerkTaak taak) =>
        taak.VoorraadStatus switch
        {
            VoorraadStatus.Reserved => "Gereserveerd uit voorraad",
            VoorraadStatus.Shortage => "Tekort - bestellen vereist",
            VoorraadStatus.Ordered => "Besteld bij leverancier",
            VoorraadStatus.Ready => "Voorraad verwerkt",
            _ when taak.IsOpVoorraad => "Op voorraad",
            _ => "Nog niet gecontroleerd"
        };
}
