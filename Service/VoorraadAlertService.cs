using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>
/// Idempotente voorraad-alert engine: vergelijkt actuele staat met gewenste
/// alerts en maakt/sluit records bij. Wordt aangeroepen aan het einde van
/// elke Stock-transactie (vóór SaveChangesAsync) zodat het alertoverzicht
/// altijd actueel is.
/// </summary>
internal static class VoorraadAlertService
{
    /// <summary>
    /// Herbouwt alle open VoorraadAlerts op basis van de huidige voorraadstaat.
    /// Aanroepen vóór SaveChangesAsync zodat alles in dezelfde transactie valt.
    /// </summary>
    internal static async Task RefreshAsync(AppDbContext db)
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
            .Where(b => b.Status == LeverancierBestellingStatus.Besteld
                     || b.Status == LeverancierBestellingStatus.DeelsOntvangen)
            .ToListAsync();

        foreach (var bestelling in bestellingen)
        {
            if (bestelling.VerwachteLeverdatum.HasValue
                && bestelling.VerwachteLeverdatum.Value.Date < DateTime.Today)
            {
                UpsertAlert(db, alerts, desiredKeys, null, VoorraadAlertType.OrderOverdue,
                    $"Bestelling:{bestelling.Id}:OrderOverdue",
                    $"Bestelling {bestelling.BestelNummer} voor leverancier {bestelling.Leverancier?.Naam ?? "—"} is over tijd.");
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
            VoorraadAlertType.OrderOverdue  => now.AddDays(1),
            _                               => now.AddDays(3)
        };
    }
}
