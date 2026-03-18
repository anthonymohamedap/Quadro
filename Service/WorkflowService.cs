using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class WorkflowService : IWorkflowService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<WorkflowService> _logger;
        private readonly IToastService _toast;
        private readonly IFactuurWorkflowService _factuurWorkflow;
        private readonly IStockService _stock;

        private sealed class NoOpFactuurWorkflowService : IFactuurWorkflowService
        {
            public Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId) =>
                Task.FromResult(new Factuur { WerkBonId = werkBonId });

            public Task<Factuur?> GetFactuurAsync(int factuurId) => Task.FromResult<Factuur?>(null);

            public Task MarkeerKlaarVoorExportAsync(int factuurId) => Task.CompletedTask;

            public Task MarkeerBetaaldAsync(int factuurId) => Task.CompletedTask;

            public Task SaveDraftAsync(Factuur factuur) => Task.CompletedTask;

            public Task HerberekenTotalenAsync(int factuurId) => Task.CompletedTask;
        }

        private sealed class FallbackStockService : IStockService
        {
            private readonly StockService _inner;

            public FallbackStockService(IDbContextFactory<AppDbContext> factory, IToastService toast)
            {
                _inner = new StockService(factory, toast);
            }

            public Task ReserveStockForWerkBonAsync(int werkBonId) => _inner.ReserveStockForWerkBonAsync(werkBonId);
            public Task ConsumeReservationsForWerkBonAsync(int werkBonId) => _inner.ConsumeReservationsForWerkBonAsync(werkBonId);
            public Task ReleaseReservationsForWerkBonAsync(int werkBonId, bool cancelOpenOrders = false) => _inner.ReleaseReservationsForWerkBonAsync(werkBonId, cancelOpenOrders);
            public Task PlaceSupplierOrderForWerkTaakAsync(int werkTaakId, DateTime bestelDatum) => _inner.PlaceSupplierOrderForWerkTaakAsync(werkTaakId, bestelDatum);
            public Task ReceiveSupplierOrderLineAsync(int bestelLijnId, decimal? aantalMeter = null) => _inner.ReceiveSupplierOrderLineAsync(bestelLijnId, aantalMeter);
            public Task CancelSupplierOrderAsync(int bestellingId) => _inner.CancelSupplierOrderAsync(bestellingId);
            public Task RefreshAlertsAsync() => _inner.RefreshAlertsAsync();
        }

        private static readonly IReadOnlyDictionary<OfferteStatus, HashSet<OfferteStatus>> OfferteTransitions =
            new Dictionary<OfferteStatus, HashSet<OfferteStatus>>
            {
                [OfferteStatus.Concept] = new() { OfferteStatus.Verzonden },
                [OfferteStatus.Verzonden] = new() { OfferteStatus.Goedgekeurd, OfferteStatus.Geannuleerd },
                [OfferteStatus.Goedgekeurd] = new() { OfferteStatus.InProductie },
                [OfferteStatus.InProductie] = new() { OfferteStatus.Afgewerkt },
                [OfferteStatus.Afgewerkt] = new() { OfferteStatus.Gefactureerd },
                [OfferteStatus.Gefactureerd] = new() { OfferteStatus.Betaald }
            };

        private static readonly IReadOnlyDictionary<WerkBonStatus, HashSet<WerkBonStatus>> WerkBonTransitions =
            new Dictionary<WerkBonStatus, HashSet<WerkBonStatus>>
            {
                [WerkBonStatus.Gepland] = new() { WerkBonStatus.InUitvoering },
                [WerkBonStatus.InUitvoering] = new() { WerkBonStatus.Afgewerkt },
                [WerkBonStatus.Afgewerkt] = new() { WerkBonStatus.Afgehaald }
            };

        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast,
            IFactuurWorkflowService factuurWorkflow,
            IStockService stock)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
            _factuurWorkflow = factuurWorkflow ?? throw new ArgumentNullException(nameof(factuurWorkflow));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));
        }

        // Backward-compatible constructor for older test/setup code paths
        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast)
            : this(factory, logger, toast, new NoOpFactuurWorkflowService(), new FallbackStockService(factory, toast))
        {
        }

        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast,
            IFactuurWorkflowService factuurWorkflow)
            : this(factory, logger, toast, factuurWorkflow, new FallbackStockService(factory, toast))
        {
        }

        public async Task ChangeOfferteStatusAsync(int offerteId, OfferteStatus newStatus)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var offerte = await db.Offertes
                .Include(o => o.WerkBon)
                .FirstOrDefaultAsync(o => o.Id == offerteId);

            if (offerte is null)
            {
                throw new InvalidOperationException("Offerte niet gevonden.");
            }

            var oldStatus = offerte.Status;
            ValidateOfferteTransition(oldStatus, newStatus);

            offerte.Status = newStatus;

            if (newStatus == OfferteStatus.Geannuleerd && offerte.WerkBon is not null)
            {
                await _stock.ReleaseReservationsForWerkBonAsync(offerte.WerkBon.Id, cancelOpenOrders: true);
            }

            var createdWerkBonId = 0;
            if (newStatus == OfferteStatus.Goedgekeurd && offerte.WerkBon is null)
            {
                var existingWerkBon = await db.WerkBonnen
                    .FirstOrDefaultAsync(w => w.OfferteId == offerteId);

                if (existingWerkBon is null)
                {
                    var werkBon = new WerkBon
                    {
                        OfferteId = offerte.Id,
                        TotaalPrijsIncl = offerte.TotaalInclBtw,
                        Status = WerkBonStatus.Gepland,
                        StockReservationProcessed = false
                    };

                    db.WerkBonnen.Add(werkBon);
                    await db.SaveChangesAsync();
                    createdWerkBonId = werkBon.Id;
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            if (createdWerkBonId > 0)
            {
                await _stock.ReserveStockForWerkBonAsync(createdWerkBonId);
            }

            _logger.LogInformation(
                "Offerte {OfferteId} status changed from {OldStatus} to {NewStatus} at {Timestamp}",
                offerteId,
                oldStatus,
                newStatus,
                DateTime.UtcNow);
        }

        public async Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus newStatus)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId);
            if (werkBon is null)
            {
                throw new InvalidOperationException("Werkbon niet gevonden.");
            }

            var oldStatus = werkBon.Status;
            ValidateWerkBonTransition(oldStatus, newStatus);

            werkBon.Status = newStatus;
            await db.SaveChangesAsync();

            if (newStatus == WerkBonStatus.Afgewerkt)
            {
                await _stock.ConsumeReservationsForWerkBonAsync(werkBonId);
                await _factuurWorkflow.MaakFactuurVanWerkBonAsync(werkBonId);
            }

            _logger.LogInformation(
                "WerkBon {WerkBonId} status changed from {OldStatus} to {NewStatus} at {Timestamp}",
                werkBonId,
                oldStatus,
                newStatus,
                DateTime.UtcNow);
        }

        public async Task ReserveStockForWerkBonAsync(int werkBonId)
        {
            await _stock.ReserveStockForWerkBonAsync(werkBonId);
        }

        public async Task MarkLijstAsBesteldAsync(int werkTaakId, DateTime bestelDatum)
        {
            await _stock.PlaceSupplierOrderForWerkTaakAsync(werkTaakId, bestelDatum);
        }

        private static void ValidateOfferteTransition(OfferteStatus oldStatus, OfferteStatus newStatus)
        {
            if (!OfferteTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException("Invalid status transition");
            }
        }

        private static void ValidateWerkBonTransition(WerkBonStatus oldStatus, WerkBonStatus newStatus)
        {
            if (!WerkBonTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException("Invalid status transition");
            }
        }
    }
}
