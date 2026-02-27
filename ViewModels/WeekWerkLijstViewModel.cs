using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class WeekWerkLijstViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    [ObservableProperty] private int year;
    [ObservableProperty] private int weekNr;
    [ObservableProperty] private string title = "";

    [ObservableProperty] private ObservableCollection<KlantWeekBlock> blocks = new();

    public WeekWerkLijstViewModel(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

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
                .ThenInclude(r => r.TypeLijst)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Glas)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.PassePartout1)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.PassePartout2)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.DiepteKern)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Opkleven)
            .Include(t => t.OfferteRegel).ThenInclude(r => r.Rug)
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

    [ObservableProperty] private string? notitie;

    public static WeekWerkItem FromTaak(WerkTaak t)
    {
        var r = t.OfferteRegel;

        return new WeekWerkItem
        {
            TaakId = t.Id,
            BonNr = t.WerkBonId,
            Stuks = r?.AantalStuks ?? 0,
            Breedte = r?.BreedteCm ?? 0,
            Hoogte = r?.HoogteCm ?? 0,
            Omschrijving = t.Omschrijving ?? "",
            Afw = r?.LegacyCode ?? "", // of maak op basis van G/P1/P2/D/O/R
            Lijst = r?.TypeLijst?.Artikelnummer ?? "",
            Inleg1 = $"{r?.InlegBreedteCm}×{r?.InlegHoogteCm}",
            Inleg2 = "",
            ProductieDatum = t.GeplandVan.Date,
            Notitie = t.WeekNotitie
        };
    }
}