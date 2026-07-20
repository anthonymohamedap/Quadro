using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.Service;

/// <summary>
/// US-37 — GDPR-verwerkingen. Gevoelige acties vereisen de GdprBeheer-permissie
/// (alleen Admin). Retentie configureerbaar via Instelling "Gdpr.RetentieJaren"
/// (standaard 7 — Belgische boekhoudkundige bewaartermijn).
/// </summary>
public sealed class GdprService : IGdprService
{
    public const string Geanonimiseerd = "GEANONIMISEERD";
    public const string RetentieSleutel = "Gdpr.RetentieJaren";
    public const int StandaardRetentieJaren = 7;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;
    private readonly ILogger<GdprService> _logger;

    public GdprService(IDbContextFactory<AppDbContext> factory, IAuthService auth, ILogger<GdprService> logger)
    {
        _factory = factory;
        _auth = auth;
        _logger = logger;
    }

    public async Task<string> ExporteerKlantAsync(int klantId)
    {
        _auth.VereisPermissie(Permissie.GdprBeheer);

        await using var db = await _factory.CreateDbContextAsync();

        var klant = await db.Klanten.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == klantId)
            ?? throw new InvalidOperationException($"Klant {klantId} niet gevonden.");

        var offertes = await db.Offertes.IgnoreQueryFilters()
            .Where(o => o.KlantId == klantId)
            .Select(o => new { o.Id, o.Datum, Status = o.Status.ToString(), o.TotaalInclBtw, o.Opmerking })
            .ToListAsync();

        var offerteIds = offertes.Select(o => o.Id).ToList();
        var facturen = await db.Facturen
            .Where(f => f.OfferteId != null && offerteIds.Contains(f.OfferteId.Value))
            .Select(f => new { f.Id, f.FactuurNummer, f.DocumentType, f.FactuurDatum, f.KlantNaam, f.KlantAdres, f.KlantBtwNummer, f.TotaalInclBtw })
            .ToListAsync();

        var export = new
        {
            ExportDatum = DateTime.Now,
            Klant = new
            {
                klant.Id, klant.Voornaam, klant.Achternaam, klant.Email, klant.Telefoon,
                klant.Straat, klant.Nummer, klant.Postcode, klant.Gemeente,
                klant.BtwNummer, klant.Opmerking, klant.IsGearchiveerd
            },
            Offertes = offertes,
            FacturenEnBestelbonnen = facturen
        };

        _logger.LogInformation("[GDPR] Inzage-export voor klant {KlantId} door '{User}'.",
            klantId, AuditContext.CurrentUserName);

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task AnonimiseerKlantAsync(int klantId)
    {
        _auth.VereisPermissie(Permissie.GdprBeheer);

        await using var db = await _factory.CreateDbContextAsync();

        var klant = await db.Klanten.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == klantId)
            ?? throw new InvalidOperationException($"Klant {klantId} niet gevonden.");

        // 1. Persoonsgegevens op de klant onherstelbaar vervangen.
        //    Facturen/bestelbonnen blijven ongewijzigd (wettelijke bewaarplicht) —
        //    zij dragen hun eigen naam-snapshot als onderdeel van het boekhouddocument.
        klant.Voornaam = Geanonimiseerd;
        klant.Achternaam = $"{Geanonimiseerd}-{klantId}";
        klant.Email = null;
        klant.Telefoon = null;
        klant.Straat = null;
        klant.Nummer = null;
        klant.Postcode = null;
        klant.Gemeente = null;
        klant.BtwNummer = null;
        klant.Opmerking = null;
        klant.IsGearchiveerd = true;
        klant.GearchiveerdOp = DateTime.Now;

        await db.SaveChangesAsync(); // audit-record van deze wijziging is gewenst

        // 2. Oudere audit-records van deze klant bevatten de oorspronkelijke
        //    persoonsgegevens — vervang de inhoud (het feit dát er gewijzigd is blijft).
        var auditRecords = await db.AuditLogs
            .Where(a => a.EntiteitType == nameof(Klant) && a.EntiteitId == klantId.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();

        // Het laatste record is de anonimisering zelf; die bevat geen persoonsdata
        // in de 'nieuw'-waarden maar wél in de 'oud'-waarden — dus ook scrubben.
        foreach (var record in auditRecords)
            record.Wijzigingen = "{\"opmerking\":\"inhoud verwijderd wegens GDPR-anonimisering\"}";

        await db.SaveChangesAsync();

        _logger.LogInformation("[GDPR] Klant {KlantId} geanonimiseerd door '{User}' ({AuditCount} audit-records geschoond).",
            klantId, AuditContext.CurrentUserName, auditRecords.Count);
    }

    public async Task<IReadOnlyList<(int KlantId, string Naam, DateTime? LaatsteActiviteit)>> VindKandidatenVoorbijRetentieAsync()
    {
        _auth.VereisPermissie(Permissie.GdprBeheer);

        await using var db = await _factory.CreateDbContextAsync();

        var retentieJaren = StandaardRetentieJaren;
        var instelling = await db.Instellingen.FindAsync(RetentieSleutel);
        if (instelling is not null && int.TryParse(instelling.Waarde, out var jaren) && jaren > 0)
            retentieJaren = jaren;

        var cutoff = DateTime.Now.AddYears(-retentieJaren);

        var klanten = await db.Klanten.IgnoreQueryFilters()
            .Where(k => k.Achternaam != null && !k.Achternaam.StartsWith(Geanonimiseerd))
            .Select(k => new
            {
                k.Id,
                Naam = k.Voornaam + " " + k.Achternaam,
                LaatsteOfferte = db.Offertes.IgnoreQueryFilters()
                    .Where(o => o.KlantId == k.Id)
                    .Max(o => (DateTime?)o.Datum)
            })
            .ToListAsync();

        return klanten
            .Where(k => k.LaatsteOfferte == null || k.LaatsteOfferte < cutoff)
            .Select(k => (k.Id, k.Naam, k.LaatsteOfferte))
            .ToList();
    }
}
