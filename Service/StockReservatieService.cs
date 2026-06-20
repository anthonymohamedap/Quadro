using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>
/// Reserveert, verbruikt en geeft vrij voorraad voor werktaken.
/// Alle methoden opereren op een al-geopende <see cref="AppDbContext"/> zodat ze
/// deel kunnen uitmaken van een grotere transactie in <see cref="StockService"/>.
/// </summary>
internal static class StockReservatieService
{
    /// <summary>
    /// Reserveert voorraad voor alle taken van een werkbon die nog niet gereserveerd zijn.
    /// Let op: <see cref="VoorraadAlertService.RefreshAsync"/> wordt NIET aangeroepen —
    /// dat doet <see cref="StockService"/> na terugkeer, vóór SaveChangesAsync.
    /// </summary>
    internal static async Task ReserveWerkBonAsync(AppDbContext db, int werkBonId, IToastService toast)
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
                await ReserveTaakAsync(db, taak, typeLijst);
                toast.Success($"Lijst succesvol gereserveerd voor {typeLijst.Artikelnummer}");
            }
            else
            {
                taak.IsOpVoorraad = false;
                taak.VoorraadStatus = taak.LeverancierBestelLijnId.HasValue || taak.IsBesteld
                    ? VoorraadStatus.Ordered
                    : VoorraadStatus.Shortage;
                toast.Warning($"Onvoldoende voorraad voor lijst {typeLijst.Artikelnummer}");
            }

            typeLijst.LaatsteVoorraadCheckOp = DateTime.UtcNow;

            if (typeLijst.BeschikbareVoorraadMeter <= typeLijst.MinimumVoorraad)
                toast.Warning($"Voorraad bijna op voor lijst {typeLijst.Artikelnummer}");
        }

        werkBon.StockReservationProcessed = true;
    }

    /// <summary>
    /// Reserveert voorraad voor één werktaak. Idempotent: herhaalde aanroep heeft geen effect
    /// als de reservering al bestaat.
    /// </summary>
    internal static async Task ReserveTaakAsync(AppDbContext db, WerkTaak taak, TypeLijst typeLijst)
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

    /// <summary>
    /// Berekent en stelt <see cref="WerkTaak.BenodigdeMeter"/> in als die nog 0 is.
    /// </summary>
    internal static void EnsureBenodigdeMeter(WerkTaak taak)
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

    /// <summary>
    /// Gooit een <see cref="ValidationException"/> als de werktaak niet voldoet aan de
    /// minimumvereisten voor voorraadverwerking.
    /// </summary>
    internal static void ValidateWerkTaakForStock(WerkTaak taak)
    {
        if (taak.BenodigdeMeter <= 0)
            throw new ValidationException("BenodigdeMeter moet groter zijn dan 0.");

        if (taak.IsBesteld && taak.BestelDatum is null)
            throw new ValidationException("BestelDatum is verplicht wanneer IsBesteld=true.");
    }
}
