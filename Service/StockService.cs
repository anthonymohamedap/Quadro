using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public class StockService : IStockService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IToastService _toast;

        public StockService(IDbContextFactory<AppDbContext> factory, IToastService toast)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
        }

        public async Task ReserveStockForWerkBonAsync(int werkBonId)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                await ReserveStockForWerkBonInternalAsync(db, werkBonId);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Voorraad werd intussen gewijzigd. Vernieuw het scherm en probeer opnieuw.", ex);
            }
        }

        public async Task ConsumeReservationsForWerkBonAsync(int werkBonId)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var werkBon = await db.WerkBonnen
                    .Include(w => w.Taken)
                        .ThenInclude(t => t.OfferteRegel)
                            .ThenInclude(r => r!.TypeLijst)
                    .FirstOrDefaultAsync(w => w.Id == werkBonId);

                if (werkBon is null)
                    throw new InvalidOperationException("Werkbon niet gevonden.");

                foreach (var taak in werkBon.Taken.Where(t => t.VoorraadStatus == VoorraadStatus.Reserved))
                {
                    EnsureBenodigdeMeter(taak);
                    ValidateWerkTaakForStock(taak);

                    var typeLijst = taak.OfferteRegel?.TypeLijst;
                    if (typeLijst is null)
                        continue;

                    var meter = taak.BenodigdeMeter;
                    typeLijst.GereserveerdeVoorraadMeter = Math.Max(0m, typeLijst.GereserveerdeVoorraadMeter - meter);
                    typeLijst.VoorraadMeter = Math.Max(0m, typeLijst.VoorraadMeter - meter);
                    typeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;
                    taak.VoorraadStatus = VoorraadStatus.Ready;
                    taak.IsOpVoorraad = true;

                    db.VoorraadMutaties.Add(new VoorraadMutatie
                    {
                        TypeLijstId = typeLijst.Id,
                        WerkBonId = werkBon.Id,
                        WerkTaakId = taak.Id,
                        MutatieType = VoorraadMutatieType.Consume,
                        AantalMeter = meter,
                        Referentie = $"WerkBon:{werkBon.Id}",
                        Opmerking = $"Definitief verbruik voor werktaak {taak.Id}"
                    });
                }

                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Voorraad werd intussen gewijzigd. Vernieuw het scherm en probeer opnieuw.", ex);
            }
        }

        public async Task ReleaseReservationsForWerkBonAsync(int werkBonId, bool cancelOpenOrders = false)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var werkBon = await db.WerkBonnen
                    .Include(w => w.Taken)
                        .ThenInclude(t => t.OfferteRegel)
                            .ThenInclude(r => r!.TypeLijst)
                    .FirstOrDefaultAsync(w => w.Id == werkBonId);

                if (werkBon is null)
                    throw new InvalidOperationException("Werkbon niet gevonden.");

                foreach (var taak in werkBon.Taken)
                {
                    EnsureBenodigdeMeter(taak);

                    var typeLijst = taak.OfferteRegel?.TypeLijst;
                    if (typeLijst is null)
                        continue;

                    if (taak.VoorraadStatus == VoorraadStatus.Reserved)
                    {
                        typeLijst.GereserveerdeVoorraadMeter = Math.Max(0m, typeLijst.GereserveerdeVoorraadMeter - taak.BenodigdeMeter);
                        db.VoorraadMutaties.Add(new VoorraadMutatie
                        {
                            TypeLijstId = typeLijst.Id,
                            WerkBonId = werkBon.Id,
                            WerkTaakId = taak.Id,
                            MutatieType = VoorraadMutatieType.Release,
                            AantalMeter = taak.BenodigdeMeter,
                            Referentie = $"WerkTaak:{taak.Id}",
                            Opmerking = $"Reservering vrijgegeven voor werktaak {taak.Id}"
                        });
                    }

                    taak.IsOpVoorraad = false;
                    taak.VoorraadStatus = taak.LeverancierBestelLijnId.HasValue || taak.IsBesteld
                        ? VoorraadStatus.Ordered
                        : VoorraadStatus.Shortage;
                }

                if (cancelOpenOrders)
                {
                    var bestellingIds = await db.Set<LeverancierBestelLijn>()
                        .Where(l => l.WerkBonId == werkBonId)
                        .Select(l => l.LeverancierBestellingId)
                        .Distinct()
                        .ToListAsync();

                    foreach (var bestellingId in bestellingIds)
                    {
                        await CancelSupplierOrderInternalAsync(db, bestellingId);
                    }
                }

                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Voorraad werd intussen gewijzigd. Vernieuw het scherm en probeer opnieuw.", ex);
            }
        }

        public async Task PlaceSupplierOrderForWerkTaakAsync(int werkTaakId, DateTime bestelDatum)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var taak = await db.WerkTaken
                    .Include(t => t.WerkBon)
                    .Include(t => t.OfferteRegel)
                        .ThenInclude(r => r!.TypeLijst)
                            .ThenInclude(l => l.Leverancier)
                    .FirstOrDefaultAsync(t => t.Id == werkTaakId);

                if (taak is null)
                    throw new InvalidOperationException("Werktaak niet gevonden.");

                EnsureBenodigdeMeter(taak);
                ValidateWerkTaakForStock(taak);

                var typeLijst = taak.OfferteRegel?.TypeLijst;
                if (typeLijst is null)
                    throw new InvalidOperationException("Geen type lijst gekoppeld aan werktaak.");

                if (typeLijst.BeschikbareVoorraadMeter >= taak.BenodigdeMeter && taak.VoorraadStatus != VoorraadStatus.Ordered)
                {
                    await ReserveTaakInternalAsync(db, taak, typeLijst);
                    await RefreshAlertsInternalAsync(db);
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    _toast.Success($"Voldoende voorraad beschikbaar voor {typeLijst.Artikelnummer}; taak is gereserveerd.");
                    return;
                }

                var bestelling = await GetOrCreateOpenSupplierOrderAsync(db, typeLijst.Leverancier, bestelDatum);

                var lijn = taak.LeverancierBestelLijnId.HasValue
                    ? await db.Set<LeverancierBestelLijn>().FirstOrDefaultAsync(x => x.Id == taak.LeverancierBestelLijnId.Value)
                    : null;

                if (lijn is null)
                {
                    lijn = new LeverancierBestelLijn
                    {
                        LeverancierBestelling = bestelling,
                        TypeLijstId = typeLijst.Id,
                        WerkBonId = taak.WerkBonId,
                        AantalMeterBesteld = taak.BenodigdeMeter,
                        AantalMeterOntvangen = 0m,
                        RedenType = LeverancierBestelRedenType.TekortWerkTaak,
                        Opmerking = $"Automatisch aangemaakt voor werktaak {taak.Id}"
                    };

                    db.Set<LeverancierBestelLijn>().Add(lijn);
                    typeLijst.InBestellingMeter += taak.BenodigdeMeter;
                }

                taak.IsBesteld = true;
                taak.BestelDatum = bestelDatum;
                taak.IsOpVoorraad = false;
                taak.VoorraadStatus = VoorraadStatus.Ordered;

                await db.SaveChangesAsync();

                taak.LeverancierBestelLijnId = lijn.Id;
                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _toast.Success($"Bestelling {bestelling.BestelNummer} geplaatst voor {typeLijst.Artikelnummer}.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Bestelling kon niet geplaatst worden omdat de voorraad of bestelling intussen gewijzigd is.", ex);
            }
        }

        public async Task CreateSupplierOrderAsync(int typeLijstId, decimal aantalMeter, DateTime bestelDatum, string? opmerking = null)
        {
            if (aantalMeter <= 0m)
                throw new InvalidOperationException("Aantal meter moet groter zijn dan 0.");

            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var typeLijst = await db.TypeLijsten
                    .Include(x => x.Leverancier)
                    .FirstOrDefaultAsync(x => x.Id == typeLijstId);

                if (typeLijst is null)
                    throw new InvalidOperationException("TypeLijst niet gevonden.");

                var bestelling = await GetOrCreateOpenSupplierOrderAsync(db, typeLijst.Leverancier, bestelDatum);

                var lijn = bestelling.Lijnen.FirstOrDefault(x =>
                    x.TypeLijstId == typeLijst.Id &&
                    x.WerkBonId is null &&
                    x.RedenType == LeverancierBestelRedenType.MinimumVoorraadAanvulling &&
                    x.AantalMeterOntvangen == 0m);

                if (lijn is null)
                {
                    lijn = new LeverancierBestelLijn
                    {
                        LeverancierBestelling = bestelling,
                        TypeLijstId = typeLijst.Id,
                        AantalMeterBesteld = aantalMeter,
                        AantalMeterOntvangen = 0m,
                        RedenType = LeverancierBestelRedenType.MinimumVoorraadAanvulling,
                        Opmerking = string.IsNullOrWhiteSpace(opmerking) ? "Handmatig aangemaakt vanuit leveranciersoverzicht" : opmerking.Trim()
                    };

                    db.Set<LeverancierBestelLijn>().Add(lijn);
                }
                else
                {
                    lijn.AantalMeterBesteld += aantalMeter;
                    if (!string.IsNullOrWhiteSpace(opmerking))
                        lijn.Opmerking = opmerking.Trim();
                }

                typeLijst.InBestellingMeter += aantalMeter;

                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _toast.Success($"Bestelling {bestelling.BestelNummer} geplaatst voor {typeLijst.Artikelnummer}.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Bestelling kon niet geplaatst worden omdat de voorraad of bestelling intussen gewijzigd is.", ex);
            }
        }

        public async Task ReceiveSupplierOrderLineAsync(int bestelLijnId, decimal? aantalMeter = null)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var lijn = await db.Set<LeverancierBestelLijn>()
                    .Include(l => l.LeverancierBestelling)
                        .ThenInclude(b => b.Lijnen)
                    .Include(l => l.TypeLijst)
                    .FirstOrDefaultAsync(l => l.Id == bestelLijnId);

                if (lijn is null)
                    throw new InvalidOperationException("Bestellijn niet gevonden.");

                var resterend = lijn.AantalMeterBesteld - lijn.AantalMeterOntvangen;
                if (resterend <= 0m)
                    return;

                var ontvangst = Math.Min(aantalMeter ?? resterend, resterend);
                if (ontvangst <= 0m)
                    throw new ValidationException("Ontvangsthoeveelheid moet groter zijn dan 0.");

                lijn.AantalMeterOntvangen += ontvangst;
                lijn.TypeLijst.VoorraadMeter += ontvangst;
                lijn.TypeLijst.InBestellingMeter = Math.Max(0m, lijn.TypeLijst.InBestellingMeter - ontvangst);
                lijn.TypeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;

                db.Set<VoorraadMutatie>().Add(new VoorraadMutatie
                {
                    TypeLijstId = lijn.TypeLijstId,
                    LeverancierBestelLijnId = lijn.Id,
                    WerkBonId = lijn.WerkBonId,
                    MutatieType = VoorraadMutatieType.Receipt,
                    AantalMeter = ontvangst,
                    Referentie = lijn.LeverancierBestelling.BestelNummer,
                    Opmerking = $"Ontvangst op bestelling {lijn.LeverancierBestelling.BestelNummer}"
                });

                var alleLijnen = lijn.LeverancierBestelling.Lijnen;
                if (alleLijnen.All(x => x.AantalMeterOntvangen >= x.AantalMeterBesteld))
                {
                    lijn.LeverancierBestelling.Status = LeverancierBestellingStatus.VolledigOntvangen;
                    lijn.LeverancierBestelling.OntvangenOp = DateTime.UtcNow;
                }
                else if (alleLijnen.Any(x => x.AantalMeterOntvangen > 0m))
                {
                    lijn.LeverancierBestelling.Status = LeverancierBestellingStatus.DeelsOntvangen;
                }

                var gekoppeldeTaak = await db.WerkTaken
                    .Include(t => t.OfferteRegel)
                        .ThenInclude(r => r!.TypeLijst)
                    .FirstOrDefaultAsync(t => t.LeverancierBestelLijnId == lijn.Id);

                if (gekoppeldeTaak is not null
                    && gekoppeldeTaak.VoorraadStatus == VoorraadStatus.Ordered
                    && lijn.TypeLijst.BeschikbareVoorraadMeter >= gekoppeldeTaak.BenodigdeMeter)
                {
                    await ReserveTaakInternalAsync(db, gekoppeldeTaak, lijn.TypeLijst);
                }

                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _toast.Success($"Ontvangst geboekt voor bestelling {lijn.LeverancierBestelling.BestelNummer}.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Ontvangst kon niet verwerkt worden omdat de bestelling intussen gewijzigd is.", ex);
            }
        }

        public async Task CancelSupplierOrderAsync(int bestellingId)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                await CancelSupplierOrderInternalAsync(db, bestellingId);
                await RefreshAlertsInternalAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Bestelling kon niet geannuleerd worden omdat ze intussen gewijzigd is.", ex);
            }
        }

        public async Task RefreshAlertsAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            await RefreshAlertsInternalAsync(db);
            await db.SaveChangesAsync();
        }

        private async Task ReserveStockForWerkBonInternalAsync(AppDbContext db, int werkBonId)
        {
            var werkBon = await db.WerkBonnen
                .Include(w => w.Taken)
                    .ThenInclude(t => t.OfferteRegel)
                        .ThenInclude(r => r!.TypeLijst)
                .FirstOrDefaultAsync(w => w.Id == werkBonId);

            if (werkBon is null)
                throw new InvalidOperationException("Werkbon niet gevonden.");

            foreach (var taak in werkBon.Taken)
            {
                EnsureBenodigdeMeter(taak);
                ValidateWerkTaakForStock(taak);

                var typeLijst = taak.OfferteRegel?.TypeLijst;
                if (typeLijst is null)
                {
                    taak.IsOpVoorraad = false;
                    taak.VoorraadStatus = VoorraadStatus.Shortage;
                    continue;
                }

                if (taak.VoorraadStatus == VoorraadStatus.Reserved || taak.VoorraadStatus == VoorraadStatus.Ready)
                    continue;

                if (typeLijst.BeschikbareVoorraadMeter >= taak.BenodigdeMeter)
                {
                    await ReserveTaakInternalAsync(db, taak, typeLijst);
                    _toast.Success($"Lijst succesvol gereserveerd voor {typeLijst.Artikelnummer}");
                }
                else
                {
                    taak.IsOpVoorraad = false;
                    taak.VoorraadStatus = taak.LeverancierBestelLijnId.HasValue || taak.IsBesteld
                        ? VoorraadStatus.Ordered
                        : VoorraadStatus.Shortage;
                    _toast.Warning($"Onvoldoende voorraad voor lijst {typeLijst.Artikelnummer}");
                }

                typeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;

                if (typeLijst.BeschikbareVoorraadMeter <= typeLijst.MinimumVoorraad)
                {
                    _toast.Warning($"Voorraad bijna op voor lijst {typeLijst.Artikelnummer}");
                }
            }

            werkBon.StockReservationProcessed = true;
            await RefreshAlertsInternalAsync(db);
        }

        private async Task ReserveTaakInternalAsync(AppDbContext db, WerkTaak taak, TypeLijst typeLijst)
        {
            EnsureBenodigdeMeter(taak);

            var hasReserve = await db.Set<VoorraadMutatie>()
                .AnyAsync(m => m.WerkTaakId == taak.Id && m.MutatieType == VoorraadMutatieType.Reserve);

            if (hasReserve)
            {
                taak.IsOpVoorraad = true;
                taak.VoorraadStatus = VoorraadStatus.Reserved;
                return;
            }

            typeLijst.GereserveerdeVoorraadMeter += taak.BenodigdeMeter;
            typeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;
            taak.IsOpVoorraad = true;
            taak.VoorraadStatus = VoorraadStatus.Reserved;

            db.Set<VoorraadMutatie>().Add(new VoorraadMutatie
            {
                TypeLijstId = typeLijst.Id,
                WerkBonId = taak.WerkBonId,
                WerkTaakId = taak.Id,
                MutatieType = VoorraadMutatieType.Reserve,
                AantalMeter = taak.BenodigdeMeter,
                Referentie = $"WerkTaak:{taak.Id}",
                Opmerking = $"Reservering voor werktaak {taak.Id}"
            });
        }

        private async Task CancelSupplierOrderInternalAsync(AppDbContext db, int bestellingId)
        {
            var bestelling = await db.Set<LeverancierBestelling>()
                .Include(b => b.Lijnen)
                    .ThenInclude(l => l.TypeLijst)
                .FirstOrDefaultAsync(b => b.Id == bestellingId);

            if (bestelling is null)
                throw new InvalidOperationException("Bestelling niet gevonden.");

            if (bestelling.Status == LeverancierBestellingStatus.VolledigOntvangen)
                throw new InvalidOperationException("Volledig ontvangen bestelling kan niet meer geannuleerd worden.");

            foreach (var lijn in bestelling.Lijnen)
            {
                var nogNietOntvangen = Math.Max(0m, lijn.AantalMeterBesteld - lijn.AantalMeterOntvangen);
                lijn.TypeLijst.InBestellingMeter = Math.Max(0m, lijn.TypeLijst.InBestellingMeter - nogNietOntvangen);

                var gekoppeldeTaken = await db.WerkTaken
                    .Where(t => t.LeverancierBestelLijnId == lijn.Id)
                    .ToListAsync();

                foreach (var taak in gekoppeldeTaken)
                {
                    taak.IsBesteld = lijn.AantalMeterOntvangen > 0m;
                    taak.BestelDatum = lijn.AantalMeterOntvangen > 0m ? taak.BestelDatum : null;
                    taak.LeverancierBestelLijnId = null;
                    taak.VoorraadStatus = lijn.AantalMeterOntvangen > 0m ? taak.VoorraadStatus : VoorraadStatus.Shortage;
                }
            }

            bestelling.Status = LeverancierBestellingStatus.Geannuleerd;
        }

        private async Task RefreshAlertsInternalAsync(AppDbContext db)
        {
            var alerts = await db.Set<VoorraadAlert>().ToListAsync();
            var desiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lijsten = await db.TypeLijsten.ToListAsync();
            foreach (var lijst in lijsten)
            {
                var herbestelNiveau = lijst.HerbestelNiveauMeter ?? lijst.MinimumVoorraad;
                if (lijst.MinimumVoorraad > 0m && lijst.BeschikbareVoorraadMeter < lijst.MinimumVoorraad)
                {
                    UpsertAlert(db, alerts, desiredKeys, lijst.Id, VoorraadAlertType.BelowMinimum,
                        $"TypeLijst:{lijst.Id}:BelowMinimum",
                        $"Voorraad onder minimum voor {lijst.Artikelnummer} ({lijst.BeschikbareVoorraadMeter:0.##} m beschikbaar).");
                }
                else if (herbestelNiveau > 0m && lijst.BeschikbareVoorraadMeter <= herbestelNiveau)
                {
                    UpsertAlert(db, alerts, desiredKeys, lijst.Id, VoorraadAlertType.LowStock,
                        $"TypeLijst:{lijst.Id}:LowStock",
                        $"Voorraad bijna op voor {lijst.Artikelnummer} ({lijst.BeschikbareVoorraadMeter:0.##} m beschikbaar).");
                }
            }

            var shortageTaken = await db.WerkTaken
                .Include(t => t.OfferteRegel)
                    .ThenInclude(r => r!.TypeLijst)
                .Where(t => t.VoorraadStatus == VoorraadStatus.Shortage)
                .ToListAsync();

            foreach (var taak in shortageTaken)
            {
                var artikel = taak.OfferteRegel?.TypeLijst?.Artikelnummer ?? "onbekend artikel";
                UpsertAlert(db, alerts, desiredKeys, taak.OfferteRegel?.TypeLijstId, VoorraadAlertType.OpenShortage,
                    $"WerkTaak:{taak.Id}:OpenShortage",
                    $"Open tekort voor werktaak {taak.Id} ({artikel}).");
            }

            var bestellingen = await db.Set<LeverancierBestelling>()
                .Include(b => b.Leverancier)
                .Include(b => b.Lijnen)
                .Where(b => b.Status == LeverancierBestellingStatus.Besteld || b.Status == LeverancierBestellingStatus.DeelsOntvangen)
                .ToListAsync();

            foreach (var bestelling in bestellingen)
            {
                if (bestelling.VerwachteLeverdatum.HasValue && bestelling.VerwachteLeverdatum.Value.Date < DateTime.Today)
                {
                    UpsertAlert(db, alerts, desiredKeys, null, VoorraadAlertType.OrderOverdue,
                        $"Bestelling:{bestelling.Id}:OrderOverdue",
                        $"Bestelling {bestelling.BestelNummer} voor leverancier {bestelling.Leverancier.Naam} is over tijd.");
                }

                if (bestelling.Status == LeverancierBestellingStatus.DeelsOntvangen)
                {
                    UpsertAlert(db, alerts, desiredKeys, null, VoorraadAlertType.PartialReceiptPending,
                        $"Bestelling:{bestelling.Id}:PartialReceiptPending",
                        $"Bestelling {bestelling.BestelNummer} is deels ontvangen en nog niet afgerond.");
                }
            }

            foreach (var alert in alerts.Where(a => a.Status == VoorraadAlertStatus.Open))
            {
                var key = BuildKey(alert.BronReferentie, alert.AlertType);
                if (!desiredKeys.Contains(key))
                    alert.Status = VoorraadAlertStatus.Resolved;
            }
        }

        private static void UpsertAlert(
            AppDbContext db,
            List<VoorraadAlert> alerts,
            ISet<string> desiredKeys,
            int? typeLijstId,
            VoorraadAlertType alertType,
            string bronReferentie,
            string bericht)
        {
            var key = BuildKey(bronReferentie, alertType);
            desiredKeys.Add(key);

            var alert = alerts.FirstOrDefault(a => a.BronReferentie == bronReferentie && a.AlertType == alertType);
            if (alert is null)
            {
                alert = new VoorraadAlert
                {
                    TypeLijstId = typeLijstId,
                    AlertType = alertType,
                    Status = VoorraadAlertStatus.Open,
                    BronReferentie = bronReferentie,
                    Bericht = bericht,
                    LaatstHerinnerdOp = DateTime.UtcNow,
                    VolgendeHerinneringOp = GetNextReminder(alertType)
                };
                alerts.Add(alert);
                db.Set<VoorraadAlert>().Add(alert);
                return;
            }

            alert.TypeLijstId = typeLijstId;
            alert.Status = VoorraadAlertStatus.Open;
            alert.Bericht = bericht;
            alert.LaatstHerinnerdOp ??= DateTime.UtcNow;
            alert.VolgendeHerinneringOp = GetNextReminder(alertType);
        }

        private static string BuildKey(string? bronReferentie, VoorraadAlertType alertType) =>
            $"{bronReferentie ?? "none"}::{alertType}";

        private static DateTime GetNextReminder(VoorraadAlertType alertType)
        {
            var now = DateTime.UtcNow;
            return alertType switch
            {
                VoorraadAlertType.BelowMinimum => now.AddDays(1),
                VoorraadAlertType.OpenShortage => now.AddDays(1),
                VoorraadAlertType.OrderOverdue => now.AddDays(1),
                _ => now.AddDays(3)
            };
        }

        private async Task<LeverancierBestelling> GetOrCreateOpenSupplierOrderAsync(AppDbContext db, Leverancier leverancier, DateTime bestelDatum)
        {
            var bestelling = await db.Set<LeverancierBestelling>()
                .Include(b => b.Lijnen)
                .Where(b => b.LeverancierId == leverancier.Id
                    && b.Status != LeverancierBestellingStatus.VolledigOntvangen
                    && b.Status != LeverancierBestellingStatus.Geannuleerd)
                .OrderByDescending(b => b.BesteldOp)
                .FirstOrDefaultAsync();

            if (bestelling is null)
            {
                bestelling = new LeverancierBestelling
                {
                    LeverancierId = leverancier.Id,
                    BestelNummer = await GenerateBestelNummerAsync(db, leverancier.Naam),
                    BesteldOp = bestelDatum,
                    VerwachteLeverdatum = bestelDatum.Date.AddDays(7),
                    Status = LeverancierBestellingStatus.Besteld
                };

                db.Set<LeverancierBestelling>().Add(bestelling);
                return bestelling;
            }

            if (bestelling.Status == LeverancierBestellingStatus.Concept)
            {
                bestelling.Status = LeverancierBestellingStatus.Besteld;
                bestelling.BesteldOp = bestelDatum;
                bestelling.VerwachteLeverdatum ??= bestelDatum.Date.AddDays(7);
            }

            return bestelling;
        }

        private async Task<string> GenerateBestelNummerAsync(AppDbContext db, string leverancierCode)
        {
            var prefix = $"{leverancierCode}-{DateTime.UtcNow:yyyyMMdd}";
            var todayCount = await db.Set<LeverancierBestelling>()
                .CountAsync(x => x.BestelNummer.StartsWith(prefix));
            return $"{prefix}-{todayCount + 1:D2}";
        }

        private static void EnsureBenodigdeMeter(WerkTaak taak)
        {
            if (taak.BenodigdeMeter > 0m)
                return;

            var regel = taak.OfferteRegel;
            var lijst = regel?.TypeLijst;
            if (regel is null || lijst is null)
                return;

            var stuks = Math.Max(1, regel.AantalStuks);
            var lengtePerStuk = (((regel.BreedteCm + regel.HoogteCm) * 2m) + (lijst.BreedteCm * 10m)) / 100m;
            taak.BenodigdeMeter = Math.Round(Math.Max(0.01m, lengtePerStuk * stuks), 2, MidpointRounding.AwayFromZero);
        }

        private static void ValidateWerkTaakForStock(WerkTaak taak)
        {
            if (taak.BenodigdeMeter <= 0)
                throw new ValidationException("BenodigdeMeter moet groter zijn dan 0.");

            if (taak.IsBesteld && taak.BestelDatum is null)
                throw new ValidationException("BestelDatum is verplicht wanneer IsBesteld=true.");
        }
    }
}
