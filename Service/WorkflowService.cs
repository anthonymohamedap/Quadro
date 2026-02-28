using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class WorkflowService : IWorkflowService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<WorkflowService> _logger;
        private readonly IToastService _toast;

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

        public WorkflowService(IDbContextFactory<AppDbContext> factory, ILogger<WorkflowService> logger, IToastService toast)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
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
                    var werkBon = new WerkBon
                    {
                        OfferteId = offerte.Id,
                        TotaalPrijsIncl = offerte.TotaalInclBtw,
                        Status = WerkBonStatus.Gepland,
                        StockReservationProcessed = false
                    };

                    db.WerkBonnen.Add(werkBon);
                    await db.SaveChangesAsync();
                    await ReserveStockForWerkBonInternalAsync(db, werkBon.Id);
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

        public async Task ReserveStockForWerkBonAsync(int werkBonId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            await ReserveStockForWerkBonInternalAsync(db, werkBonId);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task MarkLijstAsBesteldAsync(int werkTaakId, DateTime bestelDatum)
        {
            await using var db = await _factory.CreateDbContextAsync();

            var taak = await db.WerkTaken.FirstOrDefaultAsync(t => t.Id == werkTaakId);
            if (taak is null)
            {
                _toast.Error("Werktaak niet gevonden.");
                throw new InvalidOperationException("Werktaak niet gevonden.");
            }

            taak.IsBesteld = true;
            taak.BestelDatum = bestelDatum;
            ValidateWerkTaakForStock(taak);

            await db.SaveChangesAsync();
            _toast.Success($"Lijst voor werktaak {werkTaakId} gemarkeerd als besteld.");
        }

        private async Task ReserveStockForWerkBonInternalAsync(AppDbContext db, int werkBonId)
        {
            var werkBon = await db.WerkBonnen
                .Include(w => w.Taken)
                    .ThenInclude(t => t.OfferteRegel)
                        .ThenInclude(r => r!.TypeLijst)
                .FirstOrDefaultAsync(w => w.Id == werkBonId);

            if (werkBon is null)
            {
                _toast.Error("Werkbon niet gevonden.");
                throw new InvalidOperationException("Werkbon niet gevonden.");
            }

            if (werkBon.StockReservationProcessed)
            {
                _logger.LogInformation("Stock reservation skipped for WerkBon {WerkBonId}; already processed.", werkBonId);
                return;
            }

            foreach (var taak in werkBon.Taken)
            {
                ValidateWerkTaakForStock(taak);

                var typeLijst = taak.OfferteRegel?.TypeLijst;
                if (typeLijst is null)
                {
                    _toast.Warning($"Geen lijst gekoppeld aan werktaak {taak.Id}; voorraadcontrole overgeslagen.");
                    taak.IsOpVoorraad = false;
                    continue;
                }

                if (typeLijst.VoorraadMeter >= taak.BenodigdeMeter)
                {
                    typeLijst.VoorraadMeter -= taak.BenodigdeMeter;
                    taak.IsOpVoorraad = true;
                    _toast.Success($"Lijst succesvol gereserveerd voor {typeLijst.Artikelnummer}");

                    if (typeLijst.VoorraadMeter < typeLijst.MinimumVoorraad)
                    {
                        _toast.Warning($"Voorraad bijna op voor lijst {typeLijst.Artikelnummer}");
                    }
                }
                else
                {
                    taak.IsOpVoorraad = false;
                    _toast.Warning($"Onvoldoende voorraad voor lijst {typeLijst.Artikelnummer}");
                }
            }

            werkBon.StockReservationProcessed = true;
        }

        private static void ValidateWerkTaakForStock(WerkTaak taak)
        {
            if (taak.BenodigdeMeter <= 0)
            {
                throw new ValidationException("BenodigdeMeter moet groter zijn dan 0.");
            }

            if (taak.IsBesteld && taak.BestelDatum is null)
            {
                throw new ValidationException("BestelDatum is verplicht wanneer IsBesteld=true.");
            }
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
