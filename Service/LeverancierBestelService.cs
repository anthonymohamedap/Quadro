using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>
/// Database-operaties voor leveranciersbestellingen (aanmaken, ophalen, annuleren).
/// Alle methoden opereren op een al-geopende <see cref="AppDbContext"/> zodat ze
/// deel kunnen uitmaken van een grotere transactie in <see cref="StockService"/>.
/// </summary>
internal static class LeverancierBestelService
{
    /// <summary>
    /// Zoekt een openstaande bestelling voor de leverancier of maakt er een aan.
    /// Een Concept-bestelling wordt tegelijk naar Besteld gepromoveerd.
    /// </summary>
    internal static async Task<LeverancierBestelling> GetOrCreateOpenOrderAsync(
        AppDbContext db, Leverancier leverancier, DateTime bestelDatum)
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

    /// <summary>
    /// Annuleert een bestelling en verwijdert de koppeling naar werktaken.
    /// <see cref="VoorraadAlertService.RefreshAsync"/> wordt NIET aangeroepen —
    /// dat doet <see cref="StockService"/> na terugkeer, vóór SaveChangesAsync.
    /// </summary>
    internal static async Task CancelOrderAsync(AppDbContext db, int bestellingId)
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
            // TypeLijst kan gearchiveerd zijn — query filter retourneert null via Include.
            // In dat geval is voorraadcorrectie niet meer mogelijk, maar annulering mag doorgaan.
            if (lijn.TypeLijst is not null)
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

    private static async Task<string> GenerateBestelNummerAsync(AppDbContext db, string leverancierCode)
    {
        var prefix = $"{leverancierCode}-{DateTime.UtcNow:yyyyMMdd}";
        var todayCount = await db.Set<LeverancierBestelling>()
            .CountAsync(x => x.BestelNummer.StartsWith(prefix));
        return $"{prefix}-{todayCount + 1:D2}";
    }
}
