using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

public class WorkflowServiceTests
{
    [Fact]
    public async Task Valid_status_transition_succeeds()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        await SeedOfferteAsync(factory, 1, OfferteStatus.Concept);

        var sut = CreateSut(factory, toast);

        await sut.ChangeOfferteStatusAsync(1, OfferteStatus.Verzonden);

        await using var db = await factory.CreateDbContextAsync();
        var offerte = await db.Offertes.FindAsync(1);
        Assert.NotNull(offerte);
        Assert.Equal(OfferteStatus.Verzonden, offerte!.Status);
    }

    [Fact]
    public async Task Invalid_transition_throws_exception()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        await SeedOfferteAsync(factory, 2, OfferteStatus.Concept);

        var sut = CreateSut(factory, toast);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ChangeOfferteStatusAsync(2, OfferteStatus.Betaald));
    }

    [Fact]
    public async Task WerkBon_is_created_when_Offerte_becomes_Goedgekeurd()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        await SeedOfferteAsync(factory, 3, OfferteStatus.Verzonden);

        var sut = CreateSut(factory, toast);

        await sut.ChangeOfferteStatusAsync(3, OfferteStatus.Goedgekeurd);

        await using var db = await factory.CreateDbContextAsync();
        var count = await db.WerkBonnen.CountAsync(w => w.OfferteId == 3);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Duplicate_WerkBon_is_not_created_if_already_exists()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        await SeedOfferteAsync(factory, 4, OfferteStatus.Verzonden, createWerkBon: true);

        var sut = CreateSut(factory, toast);

        await sut.ChangeOfferteStatusAsync(4, OfferteStatus.Goedgekeurd);

        await using var db = await factory.CreateDbContextAsync();
        var count = await db.WerkBonnen.CountAsync(w => w.OfferteId == 4);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Stock_is_reserved_when_sufficient_stock()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 20m, minimumVoorraad: 2m, benodigdeMeter: 5m);

        var sut = CreateSut(factory, toast);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        await using var db = await factory.CreateDbContextAsync();
        var lijst = await db.TypeLijsten.FirstAsync();
        var taak = await db.WerkTaken.FirstAsync();

        Assert.Equal(20m, lijst.VoorraadMeter);
        Assert.Equal(5m, lijst.GereserveerdeVoorraadMeter);
        Assert.True(taak.IsOpVoorraad);
        Assert.Equal(VoorraadStatus.Reserved, taak.VoorraadStatus);
        Assert.Contains(toast.SuccessMessages, m => m.Contains("Lijst succesvol gereserveerd", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Warning_triggered_when_stock_is_insufficient()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 2m, minimumVoorraad: 1m, benodigdeMeter: 5m);

        var sut = CreateSut(factory, toast);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        await using var db = await factory.CreateDbContextAsync();
        var lijst = await db.TypeLijsten.FirstAsync();
        var taak = await db.WerkTaken.FirstAsync();

        Assert.Equal(2m, lijst.VoorraadMeter);
        Assert.False(taak.IsOpVoorraad);
        Assert.Equal(VoorraadStatus.Shortage, taak.VoorraadStatus);
        Assert.Contains(toast.WarningMessages, m => m.Contains("Onvoldoende voorraad", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MinimumVoorraad_warning_is_triggered()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 10m, minimumVoorraad: 6m, benodigdeMeter: 5m);

        var sut = CreateSut(factory, toast);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        Assert.Contains(toast.WarningMessages, m => m.Contains("Voorraad bijna op", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAlerts_scans_all_lijsten_and_creates_expected_stock_alerts()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();

        await using (var db = await factory.CreateDbContextAsync())
        {
            var leverancier = new Leverancier { Naam = "LEV" };
            db.Leveranciers.Add(leverancier);

            db.TypeLijsten.AddRange(
                new TypeLijst
                {
                    Artikelnummer = "ART-MIN",
                    Leverancier = leverancier,
                    Levcode = "LEV-MIN",
                    BreedteCm = 10,
                    Soort = "HOUT",
                    PrijsPerMeter = 10m,
                    VasteKost = 0m,
                    WerkMinuten = 5,
                    VoorraadMeter = 4m,
                    InventarisKost = 0m,
                    MinimumVoorraad = 5m
                },
                new TypeLijst
                {
                    Artikelnummer = "ART-LOW",
                    Leverancier = leverancier,
                    Levcode = "LEV-LOW",
                    BreedteCm = 12,
                    Soort = "ALU",
                    PrijsPerMeter = 10m,
                    VasteKost = 0m,
                    WerkMinuten = 5,
                    VoorraadMeter = 8m,
                    InventarisKost = 0m,
                    MinimumVoorraad = 3m,
                    HerbestelNiveauMeter = 8m
                },
                new TypeLijst
                {
                    Artikelnummer = "ART-OK",
                    Leverancier = leverancier,
                    Levcode = "LEV-OK",
                    BreedteCm = 15,
                    Soort = "PVC",
                    PrijsPerMeter = 10m,
                    VasteKost = 0m,
                    WerkMinuten = 5,
                    VoorraadMeter = 25m,
                    InventarisKost = 0m,
                    MinimumVoorraad = 5m,
                    HerbestelNiveauMeter = 10m
                });

            await db.SaveChangesAsync();
        }

        var stockService = new QuadroApp.Service.StockService(factory, toast);
        await stockService.RefreshAlertsAsync();

        await using var checkDb = await factory.CreateDbContextAsync();
        var alerts = await checkDb.VoorraadAlerts
            .Where(x => x.Status == VoorraadAlertStatus.Open)
            .OrderBy(x => x.Bericht)
            .ToListAsync();

        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, x => x.AlertType == VoorraadAlertType.BelowMinimum && x.Bericht.Contains("ART-MIN", StringComparison.Ordinal));
        Assert.Contains(alerts, x => x.AlertType == VoorraadAlertType.LowStock && x.Bericht.Contains("ART-LOW", StringComparison.Ordinal));
        Assert.DoesNotContain(alerts, x => x.Bericht.Contains("ART-OK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WerkTaak_can_be_marked_as_besteld()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 0m, minimumVoorraad: 2m, benodigdeMeter: 5m);

        await using var db = await factory.CreateDbContextAsync();
        var taakId = await db.WerkTaken.Select(t => t.Id).FirstAsync();

        var sut = CreateSut(factory, toast);
        var date = new DateTime(2026, 1, 15);

        await sut.MarkLijstAsBesteldAsync(taakId, date);

        await using var checkDb = await factory.CreateDbContextAsync();
        var taak = await checkDb.WerkTaken.FirstAsync(t => t.Id == taakId);
        var lijst = await checkDb.TypeLijsten.FirstAsync();
        var bestellijn = await checkDb.LeverancierBestelLijnen.FirstAsync();
        var bestelling = await checkDb.LeverancierBestellingen.FirstAsync();

        Assert.True(taak.IsBesteld);
        Assert.Equal(date, taak.BestelDatum);
        Assert.Equal(VoorraadStatus.Ordered, taak.VoorraadStatus);
        Assert.NotNull(taak.LeverancierBestelLijnId);
        Assert.Equal(5m, lijst.InBestellingMeter);
        Assert.Equal(5m, bestellijn.AantalMeterBesteld);
        Assert.Equal(LeverancierBestellingStatus.Besteld, bestelling.Status);
        Assert.Contains(toast.SuccessMessages, m => m.Contains("Bestelling", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manual_supplier_order_reuses_open_order_for_same_supplier()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();

        int lijstId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var leverancier = new Leverancier { Naam = "MAN" };
            var lijst = new TypeLijst
            {
                Artikelnummer = "ART-MAN-01",
                Leverancier = leverancier,
                Levcode = "MAN-01",
                BreedteCm = 10,
                Soort = "HOUT",
                PrijsPerMeter = 10m,
                VasteKost = 0m,
                WerkMinuten = 5,
                VoorraadMeter = 1m,
                InventarisKost = 0m,
                MinimumVoorraad = 8m
            };

            db.TypeLijsten.Add(lijst);
            await db.SaveChangesAsync();
            lijstId = lijst.Id;
        }

        var stock = new QuadroApp.Service.StockService(factory, toast);
        var date = new DateTime(2026, 3, 18);

        await stock.CreateSupplierOrderAsync(lijstId, 6m, date, "Eerste aanvulling");
        await stock.CreateSupplierOrderAsync(lijstId, 2.5m, date, "Tweede aanvulling");

        await using var checkDb = await factory.CreateDbContextAsync();
        var lijstCheck = await checkDb.TypeLijsten.SingleAsync(x => x.Id == lijstId);
        var bestellingen = await checkDb.LeverancierBestellingen.Include(x => x.Lijnen).ToListAsync();

        Assert.Single(bestellingen);
        Assert.Single(bestellingen[0].Lijnen);
        Assert.Equal(LeverancierBestellingStatus.Besteld, bestellingen[0].Status);
        Assert.Equal(8.5m, bestellingen[0].Lijnen.First().AantalMeterBesteld);
        Assert.Equal(LeverancierBestelRedenType.MinimumVoorraadAanvulling, bestellingen[0].Lijnen.First().RedenType);
        Assert.Equal(8.5m, lijstCheck.InBestellingMeter);
        Assert.Contains(toast.SuccessMessages, m => m.Contains("Bestelling", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Duplicate_reservation_is_prevented()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 20m, minimumVoorraad: 2m, benodigdeMeter: 5m);

        var sut = CreateSut(factory, toast);

        await sut.ReserveStockForWerkBonAsync(werkBonId);
        await sut.ReserveStockForWerkBonAsync(werkBonId);

        await using var db = await factory.CreateDbContextAsync();
        var lijst = await db.TypeLijsten.FirstAsync();
        Assert.Equal(20m, lijst.VoorraadMeter);
        Assert.Equal(5m, lijst.GereserveerdeVoorraadMeter);
    }

    [Fact]
    public async Task Receiving_supplier_order_updates_stock_and_reserves_task()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 0m, minimumVoorraad: 1m, benodigdeMeter: 5m);

        await using var db = await factory.CreateDbContextAsync();
        var taakId = await db.WerkTaken.Select(t => t.Id).FirstAsync();

        var workflow = CreateSut(factory, toast);
        await workflow.MarkLijstAsBesteldAsync(taakId, new DateTime(2026, 1, 15));

        await using var orderDb = await factory.CreateDbContextAsync();
        var lijnId = await orderDb.LeverancierBestelLijnen.Select(x => x.Id).FirstAsync();

        var stock = new QuadroApp.Service.StockService(factory, toast);
        await stock.ReceiveSupplierOrderLineAsync(lijnId);

        await using var checkDb = await factory.CreateDbContextAsync();
        var lijst = await checkDb.TypeLijsten.FirstAsync();
        var taak = await checkDb.WerkTaken.FirstAsync();
        var bestelling = await checkDb.LeverancierBestellingen.FirstAsync();

        Assert.Equal(5m, lijst.VoorraadMeter);
        Assert.Equal(5m, lijst.GereserveerdeVoorraadMeter);
        Assert.Equal(0m, lijst.InBestellingMeter);
        Assert.True(taak.IsOpVoorraad);
        Assert.Equal(VoorraadStatus.Reserved, taak.VoorraadStatus);
        Assert.Equal(LeverancierBestellingStatus.VolledigOntvangen, bestelling.Status);
    }

    [Fact]
    public async Task Missing_benodigde_meter_is_recalculated_from_regel()
    {
        var factory = CreateFactory();
        var toast = new TestToastService();
        var werkBonId = await SeedWerkBonWithStockAsync(factory, voorraadMeter: 20m, minimumVoorraad: 2m, benodigdeMeter: 0m);

        var sut = CreateSut(factory, toast);

        await sut.ReserveStockForWerkBonAsync(werkBonId);

        await using var db = await factory.CreateDbContextAsync();
        var taak = await db.WerkTaken.FirstAsync();
        Assert.True(taak.BenodigdeMeter > 0m);
        Assert.Equal(VoorraadStatus.Reserved, taak.VoorraadStatus);
    }

    private static QuadroApp.Service.WorkflowService CreateSut(IDbContextFactory<AppDbContext> factory, IToastService toast) =>
        new(factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance, toast);

    private static IDbContextFactory<AppDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PooledDbContextFactory<AppDbContext>(options);
    }

    private static async Task SeedOfferteAsync(IDbContextFactory<AppDbContext> factory, int id, OfferteStatus status, bool createWerkBon = false)
    {
        await using var db = await factory.CreateDbContextAsync();

        var offerte = new Offerte
        {
            Id = id,
            Datum = DateTime.UtcNow,
            Status = status,
            TotaalInclBtw = 100
        };

        db.Offertes.Add(offerte);

        if (createWerkBon)
        {
            db.WerkBonnen.Add(new WerkBon
            {
                OfferteId = id,
                Status = WerkBonStatus.Gepland,
                TotaalPrijsIncl = 100
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedWerkBonWithStockAsync(
        IDbContextFactory<AppDbContext> factory,
        decimal voorraadMeter,
        decimal minimumVoorraad,
        decimal benodigdeMeter)
    {
        await using var db = await factory.CreateDbContextAsync();

        var leverancier = new Leverancier { Naam = "LEV" };
        db.Leveranciers.Add(leverancier);

        var lijst = new TypeLijst
        {
            Artikelnummer = "ART-001",
            Leverancier = leverancier,
            Levcode = "LEV-001",
            BreedteCm = 10,
            Soort = "HOUT",
            PrijsPerMeter = 10m,
            VasteKost = 0m,
            WerkMinuten = 5,
            VoorraadMeter = voorraadMeter,
            InventarisKost = 0m,
            MinimumVoorraad = minimumVoorraad
        };

        var offerte = new Offerte
        {
            Datum = DateTime.UtcNow,
            Status = OfferteStatus.Goedgekeurd,
            TotaalInclBtw = 200m
        };

        var regel = new OfferteRegel
        {
            Offerte = offerte,
            TypeLijst = lijst,
            AantalStuks = 1,
            BreedteCm = 10,
            HoogteCm = 10
        };

        var werkBon = new WerkBon
        {
            Offerte = offerte,
            TotaalPrijsIncl = 200m,
            Status = WerkBonStatus.Gepland,
            StockReservationProcessed = false
        };

        var taak = new WerkTaak
        {
            WerkBon = werkBon,
            OfferteRegel = regel,
            GeplandVan = DateTime.UtcNow,
            GeplandTot = DateTime.UtcNow.AddMinutes(30),
            DuurMinuten = 30,
            Omschrijving = "Zaagwerk",
            BenodigdeMeter = benodigdeMeter
        };

        db.WerkTaken.Add(taak);
        await db.SaveChangesAsync();

        return werkBon.Id;
    }

    private sealed class TestToastService : IToastService
    {
        public List<string> SuccessMessages { get; } = new();
        public List<string> WarningMessages { get; } = new();

        // Not used in tests — satisfy the interface contract
        public ReadOnlyObservableCollection<QuadroApp.Model.Toast.ToastMessage> Messages { get; } =
            new(new ObservableCollection<QuadroApp.Model.Toast.ToastMessage>());

        public void Show(string message, QuadroApp.Service.Toast.ToastType type, int durationMs = 3000)
        {
            if (type == QuadroApp.Service.Toast.ToastType.Success)
                SuccessMessages.Add(message);
            else if (type == QuadroApp.Service.Toast.ToastType.Warning)
                WarningMessages.Add(message);
        }

        public void Success(string message) => SuccessMessages.Add(message);
        public void Error(string message) { }
        public void Warning(string message) => WarningMessages.Add(message);
        public void Info(string message) { }
    }
}
