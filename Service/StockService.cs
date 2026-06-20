using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
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

                await StockReservatieService.ReserveWerkBonAsync(db, werkBonId, _toast);
                await VoorraadAlertService.RefreshAsync(db);
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
                    StockReservatieService.EnsureBenodigdeMeter(taak);
                    StockReservatieService.ValidateWerkTaakForStock(taak);

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

                await VoorraadAlertService.RefreshAsync(db);
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
                    StockReservatieService.EnsureBenodigdeMeter(taak);

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
                        await LeverancierBestelService.CancelOrderAsync(db, bestellingId);
                    }
                }

                await VoorraadAlertService.RefreshAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Voorraad werd intussen gewijzigd. Vernieuw het scherm en probeer opnieuw.", ex);
            }
        }

        public async Task PlaceSupplierOrderForWerkTaakAsync(int werkTaakId, DateTime bestelDatum, BestelVorm bestelVorm = BestelVorm.Verstek)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync();
                await using var tx = await db.Database.BeginTransactionAsync();

                var taak = await db.WerkTaken
                    .Include(t => t.WerkBon)
                    .Include(t => t.OfferteRegel)
                        .ThenInclude(r => r!.TypeLijst!)
                            .ThenInclude(l => l.Leverancier)
                    .FirstOrDefaultAsync(t => t.Id == werkTaakId);

                if (taak is null)
                    throw new InvalidOperationException("Werktaak niet gevonden.");

                StockReservatieService.EnsureBenodigdeMeter(taak);
                StockReservatieService.ValidateWerkTaakForStock(taak);

                var typeLijst = taak.OfferteRegel?.TypeLijst;
                if (typeLijst is null)
                    throw new InvalidOperationException("Geen type lijst gekoppeld aan werktaak.");

                // Gemonteerde stuks worden door de leverancier gemaakt → NOOIT uit de eigen
                // (lijst-)voorraad reserveren; altijd een leveranciersbestelling plaatsen.
                var isGemonteerd = bestelVorm == BestelVorm.Gemonteerd;

                if (!isGemonteerd
                    && typeLijst.BeschikbareVoorraadMeter >= taak.BenodigdeMeter
                    && taak.VoorraadStatus != VoorraadStatus.Ordered)
                {
                    await StockReservatieService.ReserveTaakAsync(db, taak, typeLijst);
                    await VoorraadAlertService.RefreshAsync(db);
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    _toast.Success($"Voldoende voorraad beschikbaar voor {typeLijst.Artikelnummer}; taak is gereserveerd.");
                    return;
                }

                if (typeLijst.Leverancier is null)
                    throw new InvalidOperationException(
                        $"Geen leverancier gekoppeld aan lijst '{typeLijst.Artikelnummer}'. Koppel eerst een leverancier voordat je bestelt.");

                var bestelling = await LeverancierBestelService.GetOrCreateOpenOrderAsync(db, typeLijst.Leverancier, bestelDatum);

                var lijn = taak.LeverancierBestelLijnId.HasValue
                    ? await db.Set<LeverancierBestelLijn>().FirstOrDefaultAsync(x => x.Id == taak.LeverancierBestelLijnId.Value)
                    : null;

                // Bij gemonteerd is de hoeveelheid het aantal kaders (stuks); anders meters moulding.
                var besteldHoeveelheid = isGemonteerd
                    ? System.Math.Max(1, taak.OfferteRegel?.AantalStuks ?? 1)
                    : taak.BenodigdeMeter;

                if (lijn is null)
                {
                    lijn = new LeverancierBestelLijn
                    {
                        LeverancierBestelling = bestelling,
                        TypeLijstId = typeLijst.Id,
                        WerkBonId = taak.WerkBonId,
                        AantalMeterBesteld = besteldHoeveelheid,
                        AantalMeterOntvangen = 0m,
                        RedenType = LeverancierBestelRedenType.TekortWerkTaak,
                        BestelVorm = bestelVorm,
                        Opmerking = $"Automatisch aangemaakt voor werktaak {taak.Id}"
                    };

                    db.Set<LeverancierBestelLijn>().Add(lijn);

                    // Alleen moulding-in-bestelling bijhouden voor meter-bestellingen.
                    // Gemonteerde kaders raken de moulding-voorraad niet.
                    if (!isGemonteerd)
                        typeLijst.InBestellingMeter += taak.BenodigdeMeter;
                }

                taak.IsBesteld = true;
                taak.BestelDatum = bestelDatum;
                taak.IsOpVoorraad = false;
                taak.VoorraadStatus = VoorraadStatus.Ordered;

                await db.SaveChangesAsync();

                taak.LeverancierBestelLijnId = lijn.Id;
                await VoorraadAlertService.RefreshAsync(db);
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _toast.Success($"Bestelling {bestelling.BestelNummer} geplaatst voor {typeLijst.Artikelnummer}.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("Bestelling kon niet geplaatst worden omdat de voorraad of bestelling intussen gewijzigd is.", ex);
            }
        }

        public async Task CreateSupplierOrderAsync(int typeLijstId, decimal aantalMeter, DateTime bestelDatum, string? opmerking = null, BestelVorm bestelVorm = BestelVorm.Verstek)
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

                var bestelling = await LeverancierBestelService.GetOrCreateOpenOrderAsync(db, typeLijst.Leverancier!, bestelDatum);

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
                        BestelVorm = bestelVorm,
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

                await VoorraadAlertService.RefreshAsync(db);
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

                // Guard: TypeLijst kan gearchiveerd zijn (soft delete + query filter).
                // Ontvangst boeken op een gearchiveerde lijst is niet mogelijk.
                if (lijn.TypeLijst is null)
                    throw new InvalidOperationException(
                        $"De gekoppelde lijst voor bestellijn {lijn.Id} is gearchiveerd. " +
                        "Voorraadbeheer is niet meer mogelijk voor deze lijn.");

                lijn.AantalMeterOntvangen += ontvangst;

                // Gemonteerde kaders zijn stuks (door leverancier gemaakt), geen moulding-meters →
                // raak de moulding-voorraad en -mutaties NIET aan.
                var isGemonteerd = lijn.BestelVorm == BestelVorm.Gemonteerd;
                if (!isGemonteerd)
                {
                    lijn.TypeLijst.VoorraadMeter += ontvangst;
                    lijn.TypeLijst.InBestellingMeter = Math.Max(0m, lijn.TypeLijst.InBestellingMeter - ontvangst);
                }
                lijn.TypeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;

                if (!isGemonteerd)
                {
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
                }

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

                if (!isGemonteerd
                    && gekoppeldeTaak is not null
                    && gekoppeldeTaak.VoorraadStatus == VoorraadStatus.Ordered
                    && lijn.TypeLijst.BeschikbareVoorraadMeter >= gekoppeldeTaak.BenodigdeMeter)
                {
                    await StockReservatieService.ReserveTaakAsync(db, gekoppeldeTaak, lijn.TypeLijst);
                }
                else if (isGemonteerd
                    && gekoppeldeTaak is not null
                    && lijn.AantalMeterOntvangen >= lijn.AantalMeterBesteld)
                {
                    // Gemonteerde kaders volledig ontvangen → taak is verwerkt (geen moulding-voorraad nodig).
                    gekoppeldeTaak.VoorraadStatus = VoorraadStatus.Ready;
                }

                await VoorraadAlertService.RefreshAsync(db);
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

                await LeverancierBestelService.CancelOrderAsync(db, bestellingId);
                await VoorraadAlertService.RefreshAsync(db);
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
            await VoorraadAlertService.RefreshAsync(db);
            await db.SaveChangesAsync();
        }
    }
}
