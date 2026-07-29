using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Toast;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-42 — statussynchronisatie offerte ↔ werkbon ↔ bestelbon.</summary>
public class OfferteStatusSyncTests
{
    // ── Pure helper: OfferteStatusPropagation.TryAdvanceTo ──────────────────

    [Fact]
    public void Advance_moves_offerte_forward_one_step()
    {
        var offerte = new Offerte { Status = OfferteStatus.Goedgekeurd };
        var changed = OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.InProductie);
        Assert.True(changed);
        Assert.Equal(OfferteStatus.InProductie, offerte.Status);
    }

    [Fact]
    public void Advance_can_skip_steps_forward()
    {
        // InProductie → Besteld (Afgewerkt overgeslagen), zoals afgesproken.
        var offerte = new Offerte { Status = OfferteStatus.InProductie };
        Assert.True(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.Besteld));
        Assert.Equal(OfferteStatus.Besteld, offerte.Status);
    }

    [Fact]
    public void Advance_is_idempotent_when_already_at_target()
    {
        var offerte = new Offerte { Status = OfferteStatus.InProductie };
        Assert.False(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.InProductie));
        Assert.Equal(OfferteStatus.InProductie, offerte.Status);
    }

    [Fact]
    public void Advance_never_moves_backward()
    {
        var offerte = new Offerte { Status = OfferteStatus.Besteld };
        Assert.False(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.InProductie));
        Assert.Equal(OfferteStatus.Besteld, offerte.Status);
    }

    [Fact]
    public void Advance_ignores_cancelled_offerte()
    {
        var offerte = new Offerte { Status = OfferteStatus.Geannuleerd };
        Assert.False(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.InProductie));
        Assert.Equal(OfferteStatus.Geannuleerd, offerte.Status);
    }

    [Fact]
    public void Advance_ignores_offerte_before_goedgekeurd()
    {
        var offerte = new Offerte { Status = OfferteStatus.Verzonden };
        Assert.False(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.InProductie));
        Assert.Equal(OfferteStatus.Verzonden, offerte.Status);
    }

    [Fact]
    public void Advance_rejects_non_production_targets()
    {
        var offerte = new Offerte { Status = OfferteStatus.Goedgekeurd };
        Assert.False(OfferteStatusPropagation.TryAdvanceTo(offerte, OfferteStatus.Verzonden));
        Assert.Equal(OfferteStatus.Goedgekeurd, offerte.Status);
    }

    // ── Integratie: werkbon → offerte ───────────────────────────────────────

    [Fact]
    public async Task WerkBon_InUitvoering_zet_offerte_op_InProductie()
    {
        var factory = CreateInMemoryFactory();
        var (offerteId, werkBonId) = await SeedAsync(factory, OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland);
        var sut = new QuadroApp.Service.WorkflowService(
            factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance, new NullToast());

        await sut.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.InUitvoering);

        await using var db = await factory.CreateDbContextAsync();
        var offerte = await db.Offertes.FindAsync(offerteId);
        Assert.Equal(OfferteStatus.InProductie, offerte!.Status);
    }

    [Fact]
    public async Task WerkBon_Afgewerkt_zet_offerte_op_Besteld()
    {
        var factory = CreateInMemoryFactory();
        var (offerteId, werkBonId) = await SeedAsync(factory, OfferteStatus.InProductie, WerkBonStatus.InUitvoering);
        var sut = new QuadroApp.Service.WorkflowService(
            factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance, new NullToast());

        await sut.ChangeWerkBonStatusAsync(werkBonId, WerkBonStatus.Afgewerkt);

        await using var db = await factory.CreateDbContextAsync();
        var offerte = await db.Offertes.FindAsync(offerteId);
        Assert.Equal(OfferteStatus.Besteld, offerte!.Status);
    }

    // ── Integratie: bestelbon betaald → offerte ─────────────────────────────

    [Fact]
    public async Task Bestelbon_Betaald_zet_offerte_op_Betaald()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var factory = scope.Factory;

        int offerteId, factuurId;
        await using (var db = await factory.CreateDbContextAsync())
        {
            var offerte = new Offerte { Datum = DateTime.UtcNow, Status = OfferteStatus.Besteld, TotaalInclBtw = 100m };
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();

            var factuur = new Factuur
            {
                OfferteId = offerte.Id,
                Jaar = 2026,
                VolgNr = 1,
                FactuurNummer = "B2026-0001",
                Status = FactuurStatus.KlaarVoorExport,
                KlantNaam = "Test",
                FactuurDatum = DateTime.UtcNow,
                VervalDatum = DateTime.UtcNow
            };
            db.Facturen.Add(factuur);
            await db.SaveChangesAsync();

            offerteId = offerte.Id;
            factuurId = factuur.Id;
        }

        var sut = new FactuurWorkflowService(
            factory,
            new PricingService(factory, new FixedPricing(), new PricingEngine(),
                NullLogger<PricingService>.Instance),
            new TestAuthService());

        await sut.MarkeerBetaaldAsync(factuurId);

        await using var check = await factory.CreateDbContextAsync();
        Assert.Equal(FactuurStatus.Betaald, (await check.Facturen.FindAsync(factuurId))!.Status);
        Assert.Equal(OfferteStatus.Betaald, (await check.Offertes.FindAsync(offerteId))!.Status);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IDbContextFactory<AppDbContext> CreateInMemoryFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new PooledDbContextFactory<AppDbContext>(options);
    }

    private static async Task<(int OfferteId, int WerkBonId)> SeedAsync(
        IDbContextFactory<AppDbContext> factory, OfferteStatus offerteStatus, WerkBonStatus werkBonStatus)
    {
        await using var db = await factory.CreateDbContextAsync();
        var offerte = new Offerte { Datum = DateTime.UtcNow, Status = offerteStatus, TotaalInclBtw = 100m };
        var werkBon = new WerkBon
        {
            Offerte = offerte,
            Status = werkBonStatus,
            TotaalPrijsIncl = 100m,
            StockReservationProcessed = true
        };
        db.WerkBonnen.Add(werkBon);
        await db.SaveChangesAsync();
        return (offerte.Id, werkBon.Id);
    }

    private sealed class FixedPricing : IPricingSettingsProvider
    {
        public Task<decimal> GetUurloonAsync() => Task.FromResult(60m);
        public Task<decimal> GetBtwPercentAsync() => Task.FromResult(21m);
        public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(0m);
        public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(1m);
        public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(10m);
    }

    private sealed class NullToast : IToastService
    {
        public ReadOnlyObservableCollection<ToastMessage> Messages { get; } =
            new(new ObservableCollection<ToastMessage>());
        public void Show(string message, ToastType type, int durationMs = 3000) { }
        public void Success(string message) { }
        public void Error(string message) { }
        public void Warning(string message) { }
        public void Info(string message) { }
        public void Info(string message, string actionLabel, Action onAction, int durationMs = 30_000) { }
    }
}
