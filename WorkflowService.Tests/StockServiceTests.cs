using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// P4 — dekking voor de voorraad-lifecycle in <see cref="StockService"/>:
/// reserveren, verbruiken, vrijgeven, bestellen (voorraad vs. leverancier) en ontvangen.
/// Draait op een echte (in-memory) SQLite zodat transacties en mutaties realistisch zijn.
/// </summary>
public class StockServiceTests
{
    private static StockService CreateSut(IDbContextFactory<AppDbContext> factory, out TestToastService toast)
    {
        toast = new TestToastService();
        return new StockService(factory, toast);
    }

    /// <summary>Seedt Leverancier → TypeLijst → Offerte → OfferteRegel → WerkBon → WerkTaak.</summary>
    private static async Task<(int werkBonId, int werkTaakId, int typeLijstId)> SeedAsync(
        IDbContextFactory<AppDbContext> factory,
        decimal voorraadMeter,
        decimal benodigdeMeter,
        bool metLeverancier = true)
    {
        await using var db = await factory.CreateDbContextAsync();

        Leverancier? lev = metLeverancier ? new Leverancier { Naam = "LEV" } : null;

        var lijst = new TypeLijst
        {
            Artikelnummer = "ART1",
            Levcode = "A1",
            BreedteCm = 10,
            Soort = "ALU",
            PrijsPerMeter = 10m,
            VasteKost = 1m,
            WerkMinuten = 5,
            VoorraadMeter = voorraadMeter,
            MinimumVoorraad = 0m,
            InventarisKost = 0m,
            Leverancier = lev
        };

        var offerte = new Offerte { Datum = DateTime.Today, Status = OfferteStatus.InProductie };
        var regel = new OfferteRegel { Offerte = offerte, TypeLijst = lijst, AantalStuks = 1, BreedteCm = 20m, HoogteCm = 30m };
        var werkbon = new WerkBon { Offerte = offerte, Status = WerkBonStatus.InUitvoering };
        var taak = new WerkTaak
        {
            WerkBon = werkbon,
            OfferteRegel = regel,
            GeplandVan = DateTime.Today,
            DuurMinuten = 30,
            BenodigdeMeter = benodigdeMeter,
            Omschrijving = "test"
        };

        if (lev is not null) db.Leveranciers.Add(lev);
        db.TypeLijsten.Add(lijst);
        db.Offertes.Add(offerte);
        db.OfferteRegels.Add(regel);
        db.WerkBonnen.Add(werkbon);
        db.WerkTaken.Add(taak);
        await db.SaveChangesAsync();

        return (werkbon.Id, taak.Id, lijst.Id);
    }

    private static async Task<TypeLijst> GetLijstAsync(IDbContextFactory<AppDbContext> factory, int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TypeLijsten.FirstAsync(l => l.Id == id);
    }

    private static async Task<WerkTaak> GetTaakAsync(IDbContextFactory<AppDbContext> factory, int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WerkTaken.FirstAsync(t => t.Id == id);
    }

    private static async Task<int> CountMutatiesAsync(IDbContextFactory<AppDbContext> factory, int typeLijstId, VoorraadMutatieType type)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.VoorraadMutaties.CountAsync(m => m.TypeLijstId == typeLijstId && m.MutatieType == type);
    }

    // ── Reserveren ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reserve_met_voldoende_voorraad_reserveert_en_boekt_mutatie()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (werkBonId, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 100m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        var lijst = await GetLijstAsync(factory, lijstId);
        var taak = await GetTaakAsync(factory, taakId);
        Assert.Equal(10m, lijst.GereserveerdeVoorraadMeter);
        Assert.Equal(100m, lijst.VoorraadMeter); // fysieke voorraad blijft, enkel gereserveerd
        Assert.Equal(VoorraadStatus.Reserved, taak.VoorraadStatus);
        Assert.True(taak.IsOpVoorraad);
        Assert.Equal(1, await CountMutatiesAsync(factory, lijstId, VoorraadMutatieType.Reserve));
    }

    [Fact]
    public async Task Reserve_met_onvoldoende_voorraad_zet_shortage_zonder_mutatie()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (werkBonId, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 5m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        var lijst = await GetLijstAsync(factory, lijstId);
        var taak = await GetTaakAsync(factory, taakId);
        Assert.Equal(0m, lijst.GereserveerdeVoorraadMeter);
        Assert.Equal(VoorraadStatus.Shortage, taak.VoorraadStatus);
        Assert.False(taak.IsOpVoorraad);
        Assert.Equal(0, await CountMutatiesAsync(factory, lijstId, VoorraadMutatieType.Reserve));
    }

    // ── Verbruiken ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Consume_na_reservering_verlaagt_voorraad_en_zet_ready()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (werkBonId, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 100m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.ReserveStockForWerkBonAsync(werkBonId);
        await sut.ConsumeReservationsForWerkBonAsync(werkBonId);

        var lijst = await GetLijstAsync(factory, lijstId);
        var taak = await GetTaakAsync(factory, taakId);
        Assert.Equal(90m, lijst.VoorraadMeter);              // fysiek verbruikt
        Assert.Equal(0m, lijst.GereserveerdeVoorraadMeter);  // reservering vrijgevallen
        Assert.Equal(VoorraadStatus.Ready, taak.VoorraadStatus);
        Assert.Equal(1, await CountMutatiesAsync(factory, lijstId, VoorraadMutatieType.Consume));
    }

    // ── Vrijgeven ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Release_na_reservering_geeft_gereserveerde_meter_vrij()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (werkBonId, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 100m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.ReserveStockForWerkBonAsync(werkBonId);
        await sut.ReleaseReservationsForWerkBonAsync(werkBonId);

        var lijst = await GetLijstAsync(factory, lijstId);
        var taak = await GetTaakAsync(factory, taakId);
        Assert.Equal(0m, lijst.GereserveerdeVoorraadMeter);
        Assert.Equal(100m, lijst.VoorraadMeter);            // fysieke voorraad ongewijzigd
        Assert.Equal(VoorraadStatus.Shortage, taak.VoorraadStatus);
        Assert.False(taak.IsOpVoorraad);
        Assert.Equal(1, await CountMutatiesAsync(factory, lijstId, VoorraadMutatieType.Release));
    }

    // ── Leveranciersbestelling: aanmaken en ontvangen ───────────────────────────

    [Fact]
    public async Task CreateSupplierOrder_maakt_bestelling_en_verhoogt_in_bestelling()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (_, _, lijstId) = await SeedAsync(factory, voorraadMeter: 0m, benodigdeMeter: 1m);
        var sut = CreateSut(factory, out var toast);

        await sut.CreateSupplierOrderAsync(lijstId, aantalMeter: 50m, bestelDatum: DateTime.Today);

        var lijst = await GetLijstAsync(factory, lijstId);
        Assert.Equal(50m, lijst.InBestellingMeter);

        await using var db = await factory.CreateDbContextAsync();
        var lijn = await db.Set<LeverancierBestelLijn>().SingleAsync(l => l.TypeLijstId == lijstId);
        Assert.Equal(50m, lijn.AantalMeterBesteld);
        Assert.Equal(0m, lijn.AantalMeterOntvangen);
        Assert.NotEmpty(toast.SuccessMessages);
    }

    [Fact]
    public async Task ReceiveSupplierOrderLine_verhoogt_voorraad_en_sluit_bestelling()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (_, _, lijstId) = await SeedAsync(factory, voorraadMeter: 0m, benodigdeMeter: 1m);
        var sut = CreateSut(factory, out _);

        await sut.CreateSupplierOrderAsync(lijstId, aantalMeter: 50m, bestelDatum: DateTime.Today);

        int lijnId;
        await using (var db = await factory.CreateDbContextAsync())
            lijnId = await db.Set<LeverancierBestelLijn>().Where(l => l.TypeLijstId == lijstId).Select(l => l.Id).SingleAsync();

        await sut.ReceiveSupplierOrderLineAsync(lijnId, aantalMeter: 50m);

        var lijst = await GetLijstAsync(factory, lijstId);
        Assert.Equal(50m, lijst.VoorraadMeter);
        Assert.Equal(0m, lijst.InBestellingMeter);
        Assert.Equal(1, await CountMutatiesAsync(factory, lijstId, VoorraadMutatieType.Receipt));

        await using var db2 = await factory.CreateDbContextAsync();
        var bestelling = await db2.Set<LeverancierBestelling>().SingleAsync();
        Assert.Equal(LeverancierBestellingStatus.VolledigOntvangen, bestelling.Status);
    }

    // ── Bestellen voor een werktaak: voorraad vs. leverancier ───────────────────

    [Fact]
    public async Task PlaceSupplierOrderForWerkTaak_reserveert_bij_voldoende_voorraad()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (_, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 100m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.PlaceSupplierOrderForWerkTaakAsync(taakId, DateTime.Today);

        var taak = await GetTaakAsync(factory, taakId);
        var lijst = await GetLijstAsync(factory, lijstId);
        Assert.Equal(VoorraadStatus.Reserved, taak.VoorraadStatus);
        Assert.Equal(10m, lijst.GereserveerdeVoorraadMeter);

        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Set<LeverancierBestelling>().CountAsync()); // geen bestelling nodig
    }

    [Fact]
    public async Task PlaceSupplierOrderForWerkTaak_bestelt_bij_onvoldoende_voorraad()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;
        var (werkBonId, taakId, lijstId) = await SeedAsync(factory, voorraadMeter: 5m, benodigdeMeter: 10m);
        var sut = CreateSut(factory, out _);

        await sut.PlaceSupplierOrderForWerkTaakAsync(taakId, DateTime.Today);

        var taak = await GetTaakAsync(factory, taakId);
        var lijst = await GetLijstAsync(factory, lijstId);
        Assert.Equal(VoorraadStatus.Ordered, taak.VoorraadStatus);
        Assert.True(taak.IsBesteld);
        Assert.Equal(10m, lijst.InBestellingMeter);

        await using var db = await factory.CreateDbContextAsync();
        var lijn = await db.Set<LeverancierBestelLijn>().SingleAsync(l => l.TypeLijstId == lijstId);
        Assert.Equal(werkBonId, lijn.WerkBonId);
    }
}
