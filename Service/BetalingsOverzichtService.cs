using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>
/// US-67 — leest het ontvangstenregister voor het "overzicht betalingen".
/// <para>Bonnummer: het nummer van de bestelbon. Een voorschot hangt enkel aan de offerte; is er
/// intussen een bestelbon voor die offerte, dan wordt diens nummer getoond, anders "offerte X".
/// Winkelverkopen hebben geen bon (blanco, zoals op de oude kassa-afdruk).</para>
/// <para>Optellen gebeurt in het geheugen: SQLite kan geen SUM op decimal.</para>
/// </summary>
public sealed class BetalingsOverzichtService : IBetalingsOverzichtService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public BetalingsOverzichtService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<BetalingsOverzicht> GetOverzichtAsync(DateTime van, DateTime tot)
    {
        if (tot.Date < van.Date)
            (van, tot) = (tot, van);

        var vanaf = van.Date;
        var totExclusief = tot.Date.AddDays(1);

        await using var db = await _factory.CreateDbContextAsync();

        var ontvangsten = await db.Ontvangsten.AsNoTracking()
            .Where(o => o.Datum >= vanaf && o.Datum < totExclusief)
            .Select(o => new
            {
                o.Id,
                o.Datum,
                o.Soort,
                o.Betaalwijze,
                o.BedragIncl,
                o.Omschrijving,
                o.OfferteId,
                FactuurNummer = o.Factuur != null ? o.Factuur.FactuurNummer : null,
                FactuurJaar = o.Factuur != null ? (int?)o.Factuur.Jaar : null,
                OfferteNummer = o.Offerte != null ? (int?)o.Offerte.OfferteNummer : null
            })
            .ToListAsync();

        // Voorschotten (enkel aan een offerte gekoppeld): zoek de bestelbon van die offerte op.
        var offerteIdsZonderBon = ontvangsten
            .Where(o => o.FactuurNummer is null && o.OfferteId.HasValue)
            .Select(o => o.OfferteId!.Value)
            .Distinct()
            .ToList();

        var bonPerOfferte = new Dictionary<int, (string Nummer, int Jaar)>();
        if (offerteIdsZonderBon.Count > 0)
        {
            var bons = await db.Facturen.AsNoTracking()
                .Where(f => f.OfferteId.HasValue && offerteIdsZonderBon.Contains(f.OfferteId.Value))
                .Select(f => new { OfferteId = f.OfferteId!.Value, f.FactuurNummer, f.Jaar })
                .ToListAsync();
            foreach (var b in bons)
                bonPerOfferte[b.OfferteId] = (b.FactuurNummer, b.Jaar);
        }

        var regels = ontvangsten.Select(o =>
        {
            string bon = string.Empty;
            int? jaar = null;

            if (o.FactuurNummer is not null)
            {
                bon = o.FactuurNummer;
                jaar = o.FactuurJaar;
            }
            else if (o.OfferteId is int offerteId)
            {
                if (bonPerOfferte.TryGetValue(offerteId, out var b))
                {
                    bon = b.Nummer;
                    jaar = b.Jaar;
                }
                else if (o.OfferteNummer is int nr && nr > 0)
                {
                    bon = $"offerte {nr}";
                }
            }

            return new BetalingsOverzichtRegel(
                o.Id, o.Datum, o.Soort, o.Betaalwijze, o.BedragIncl, bon, jaar, o.Omschrijving);
        });

        return BetalingsOverzicht.Bouw(vanaf, tot.Date, regels);
    }
}
