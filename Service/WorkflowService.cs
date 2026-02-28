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
    public sealed class WorkflowService : IWorkflowService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<WorkflowService> _logger;

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

        public WorkflowService(IDbContextFactory<AppDbContext> factory, ILogger<WorkflowService> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            if (newStatus == OfferteStatus.Goedgekeurd && offerte.WerkBon is null)
            {
                var existingWerkBon = await db.WerkBonnen
                    .FirstOrDefaultAsync(w => w.OfferteId == offerteId);

                if (existingWerkBon is null)
                {
                    db.WerkBonnen.Add(new WerkBon
                    {
                        OfferteId = offerte.Id,
                        TotaalPrijsIncl = offerte.TotaalInclBtw,
                        Status = WerkBonStatus.Gepland
                    });
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

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

            _logger.LogInformation(
                "WerkBon {WerkBonId} status changed from {OldStatus} to {NewStatus} at {Timestamp}",
                werkBonId,
                oldStatus,
                newStatus,
                DateTime.UtcNow);
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
