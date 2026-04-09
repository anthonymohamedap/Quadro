using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Snapshot;
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
        private readonly IWerkBonArchiefService _archief;

        private sealed class NoOpFactuurWorkflowService : IFactuurWorkflowService
        {
            public Task<Factuur> MaakFactuurVanOfferteAsync(int offerteId) =>
                Task.FromResult(new Factuur { OfferteId = offerteId });

            public Task<Factuur> MaakFactuurVanWerkBonAsync(int werkBonId) =>
                Task.FromResult(new Factuur { WerkBonId = werkBonId });

            public Task<Factuur?> GetFactuurAsync(int factuurId) => Task.FromResult<Factuur?>(null);

            public Task<Factuur?> GetFactuurVoorOfferteAsync(int offerteId) => Task.FromResult<Factuur?>(null);

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
            public Task CreateSupplierOrderAsync(int typeLijstId, decimal aantalMeter, DateTime bestelDatum, string? opmerking = null) => _inner.CreateSupplierOrderAsync(typeLijstId, aantalMeter, bestelDatum, opmerking);
            public Task ReceiveSupplierOrderLineAsync(int bestelLijnId, decimal? aantalMeter = null) => _inner.ReceiveSupplierOrderLineAsync(bestelLijnId, aantalMeter);
            public Task CancelSupplierOrderAsync(int bestellingId) => _inner.CancelSupplierOrderAsync(bestellingId);
            public Task RefreshAlertsAsync() => _inner.RefreshAlertsAsync();
        }

        // ── State-machine tabellen ──────────────────────────────────────────
        private static readonly IReadOnlyDictionary<OfferteStatus, HashSet<OfferteStatus>> OfferteTransitions =
            new Dictionary<OfferteStatus, HashSet<OfferteStatus>>
            {
                [OfferteStatus.Concept]     = new() { OfferteStatus.Verzonden, OfferteStatus.Geannuleerd },
                [OfferteStatus.Verzonden]   = new() { OfferteStatus.Goedgekeurd, OfferteStatus.Geannuleerd },
                [OfferteStatus.Goedgekeurd] = new() { OfferteStatus.InProductie, OfferteStatus.Geannuleerd },
                [OfferteStatus.InProductie] = new() { OfferteStatus.Afgewerkt, OfferteStatus.Geannuleerd },
                [OfferteStatus.Afgewerkt]   = new() { OfferteStatus.Gefactureerd },
                [OfferteStatus.Gefactureerd]= new() { OfferteStatus.Betaald }
            };

        private static readonly IReadOnlyDictionary<WerkBonStatus, HashSet<WerkBonStatus>> WerkBonTransitions =
            new Dictionary<WerkBonStatus, HashSet<WerkBonStatus>>
            {
                [WerkBonStatus.Gepland]     = new() { WerkBonStatus.InUitvoering },
                [WerkBonStatus.InUitvoering]= new() { WerkBonStatus.Afgewerkt },
                [WerkBonStatus.Afgewerkt]   = new() { WerkBonStatus.Afgehaald }
            };

        // ── Constructors ────────────────────────────────────────────────────

        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast,
            IFactuurWorkflowService factuurWorkflow,
            IStockService stock,
            IWerkBonArchiefService archief)
        {
            _factory        = factory        ?? throw new ArgumentNullException(nameof(factory));
            _logger         = logger         ?? throw new ArgumentNullException(nameof(logger));
            _toast          = toast          ?? throw new ArgumentNullException(nameof(toast));
            _factuurWorkflow= factuurWorkflow ?? throw new ArgumentNullException(nameof(factuurWorkflow));
            _stock          = stock          ?? throw new ArgumentNullException(nameof(stock));
            _archief        = archief        ?? throw new ArgumentNullException(nameof(archief));
        }

        // Backward-compatible constructor (geen archief → no-op)
        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast)
            : this(factory, logger, toast,
                   new NoOpFactuurWorkflowService(),
                   new FallbackStockService(factory, toast),
                   new NoOpWerkBonArchiefService())
        { }

        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast,
            IFactuurWorkflowService factuurWorkflow)
            : this(factory, logger, toast, factuurWorkflow,
                   new FallbackStockService(factory, toast),
                   new NoOpWerkBonArchiefService())
        { }

        public WorkflowService(
            IDbContextFactory<AppDbContext> factory,
            ILogger<WorkflowService> logger,
            IToastService toast,
            IFactuurWorkflowService factuurWorkflow,
            IStockService stock)
            : this(factory, logger, toast, factuurWorkflow, stock,
                   new NoOpWerkBonArchiefService())
        { }

        // ── ChangeOfferteStatusAsync ────────────────────────────────────────

        public async Task ChangeOfferteStatusAsync(int offerteId, OfferteStatus newStatus)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var offerte = await db.Offertes
                .Include(o => o.WerkBon)
                .FirstOrDefaultAsync(o => o.Id == offerteId)
                ?? throw new InvalidOperationException("Offerte niet gevonden.");

            var oldStatus = offerte.Status;
            ValidateOfferteTransition(oldStatus, newStatus);

            offerte.Status = newStatus;

            // ── Annulering: archiveer werkbon + geef stock vrij ─────────────
            if (newStatus == OfferteStatus.Geannuleerd && offerte.WerkBon is not null)
            {
                var werkBonId = offerte.WerkBon.Id;

                // Commit status EERST zodat de snapshot de juiste statussen vastlegt
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                // Stock vrijgeven (buiten transactie — eigen transactie intern)
                await _stock.ReleaseReservationsForWerkBonAsync(werkBonId, cancelOpenOrders: true);

                // Archiveer — eigen transactie intern in de service
                try
                {
                    await _archief.ArchiveerAsync(werkBonId, annuleringsReden: null);
                }
                catch (Exception ex)
                {
                    // Archivering mag de annulering zelf NIET blokkeren — log en ga door.
                    _logger.LogError(ex,
                        "Archivering van WerkBon {WerkBonId} mislukt na annulering van offerte {OfferteId}.",
                        werkBonId, offerteId);
                }

                _logger.LogInformation(
                    "Offerte {OfferteId} geannuleerd (was {OldStatus}), WerkBon {WerkBonId} gearchiveerd.",
                    offerteId, oldStatus, werkBonId);

                return;
            }

            // ── Goedkeuring: maak werkbon aan indien nog niet bestaat ───────
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
                offerteId, oldStatus, newStatus, DateTime.UtcNow);
        }

        // ── ChangeWerkBonStatusAsync ────────────────────────────────────────

        public async Task ChangeWerkBonStatusAsync(int werkBonId, WerkBonStatus newStatus)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var werkBon = await db.WerkBonnen.FirstOrDefaultAsync(w => w.Id == werkBonId)
                ?? throw new InvalidOperationException("Werkbon niet gevonden.");

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
                werkBonId, oldStatus, newStatus, DateTime.UtcNow);
        }

        public async Task ReserveStockForWerkBonAsync(int werkBonId)
            => await _stock.ReserveStockForWerkBonAsync(werkBonId);

        public async Task MarkLijstAsBesteldAsync(int werkTaakId, DateTime bestelDatum)
            => await _stock.PlaceSupplierOrderForWerkTaakAsync(werkTaakId, bestelDatum);

        // ── Validatie ───────────────────────────────────────────────────────

        private static void ValidateOfferteTransition(OfferteStatus oldStatus, OfferteStatus newStatus)
        {
            if (!OfferteTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
                throw new InvalidOperationException(
                    $"Ongeldige statusovergang: {oldStatus} → {newStatus}");
        }

        private static void ValidateWerkBonTransition(WerkBonStatus oldStatus, WerkBonStatus newStatus)
        {
            if (!WerkBonTransitions.TryGetValue(oldStatus, out var allowed) || !allowed.Contains(newStatus))
                throw new InvalidOperationException(
                    $"Ongeldige statusovergang: {oldStatus} → {newStatus}");
        }

        // ── No-op archief voor backward-compatible constructors ─────────────

        private sealed class NoOpWerkBonArchiefService : IWerkBonArchiefService
        {
            public Task<WerkBonArchief> ArchiveerAsync(int werkBonId, string? annuleringsReden = null)
                => Task.FromResult(new WerkBonArchief { OrigineleWerkBonId = werkBonId });

            public Task<int> HerstellenAsync(int archiefId)
                => Task.FromResult(0);

            public Task<List<WerkBonArchief>> GetAlleAsync()
                => Task.FromResult(new List<WerkBonArchief>());

            public Task<WerkBonArchiefSnapshot?> GetSnapshotAsync(int archiefId)
                => Task.FromResult<WerkBonArchiefSnapshot?>(null);
        }
    }
}
