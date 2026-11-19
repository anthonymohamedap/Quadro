using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

/// <summary>Eén regel in het overzicht van "Startklaar maken": wat, hoeveel rijen.</summary>
public sealed record StartklaarRegel(string Wat, int Aantal);

/// <summary>Wat er bij "Startklaar maken" weggaat en wat er blijft staan.</summary>
public sealed record StartklaarOverzicht(IReadOnlyList<StartklaarRegel> Weg, IReadOnlyList<StartklaarRegel> Blijft)
{
    public int TotaalWeg => Weg.Sum(r => r.Aantal);
}

public interface IStartklaarService
{
    /// <summary>Telt wat er zou verdwijnen en wat blijft, zonder iets te wijzigen.</summary>
    Task<StartklaarOverzicht> TelAsync();

    /// <summary>Verwijdert alle test- en verkoopgegevens; de basisgegevens blijven. Enkel voor de beheerder.</summary>
    Task<StartklaarOverzicht> VoerUitAsync();
}

/// <summary>
/// Start op 1 januari 2027 (afspraak met Anthony, 19/11/2026): alles wat tijdens het testen ontstond, gaat weg;
/// alles wat nodig is om een offerte te maken, blijft.
/// <list type="bullet">
/// <item>Weg: klanten (allemaal testklanten), offertes en inlijstingen, werkbonnen en planning, bestelbonnen,
/// betalingen en winkelverkopen, archief, leveranciersbestellingen, voorraadgeschiedenis en -meldingen,
/// importlogboek, foutmeldingen en auditlog.</item>
/// <item>Blijft: lijsten (met hun voorraad), afwerkingen, standaardkaders, leveranciers, instellingen,
/// gebruikers en geblokkeerde dagen.</item>
/// <item>De gereserveerde voorraad gaat naar 0: die reservaties hoorden bij de verwijderde werkbonnen.</item>
/// <item>De interne tellers (Id) van de leeggemaakte tabellen beginnen opnieuw bij 1. Het offertenummer begint
/// daardoor bij 1 en het eerste bestelbonnummer van 2027 wordt 2027-1.</item>
/// </list>
/// Alles gebeurt in één transactie: lukt één stap niet, dan blijft alles zoals het was.
/// Er wordt rechtstreeks verwijderd (ExecuteDelete), zonder per rij een auditregel te schrijven; na afloop komt
/// er één auditregel die vermeldt wie het deed.
/// </summary>
public sealed class StartklaarService : IStartklaarService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuthService _auth;

    public StartklaarService(IDbContextFactory<AppDbContext> factory, IAuthService auth)
    {
        _factory = factory;
        _auth = auth;
    }

    public async Task<StartklaarOverzicht> TelAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await TelAsync(db);
    }

    public async Task<StartklaarOverzicht> VoerUitAsync()
    {
        _auth.VereisPermissie(Permissie.GebruikersBeheren);
        var wie = _auth.CurrentUser?.GebruikersNaam ?? "onbekend";

        await using var db = await _factory.CreateDbContextAsync();
        var voor = await TelAsync(db);

        await db.ExecuteWithRetryAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            // Volgorde: eerst wat naar andere tabellen verwijst. IgnoreQueryFilters: ook gearchiveerde rijen.
            await db.VoorraadMutaties.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.VoorraadAlerts.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.BestelbonLijnen.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Ontvangsten.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Bestelbonnen.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.WerkTaken.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.LeverancierBestelLijnen.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.LeverancierBestellingen.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.WerkBonnen.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.OfferteRegels.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Offertes.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.OfferteArchieven.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.WerkBonArchieven.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Klanten.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.ImportRowLogs.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.ImportSessions.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.FoutLogs.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.AuditLogs.IgnoreQueryFilters().ExecuteDeleteAsync();

            // Reservaties hoorden bij de verwijderde werkbonnen; de voorraad zelf blijft.
            await db.TypeLijsten.IgnoreQueryFilters()
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.GereserveerdeVoorraadMeter, 0m));

            await ZetTellersTerugAsync(db);
            await tx.CommitAsync();
        });

        db.AuditLogs.Add(new AuditLog
        {
            Tijdstip = DateTime.UtcNow,
            Gebruiker = wie,
            EntiteitType = "Startklaar",
            EntiteitId = "-",
            Actie = "Startklaar gemaakt",
            Wijzigingen = $"{{\"verwijderd\":{voor.TotaalWeg}}}"
        });
        await db.SaveChangesAsync();

        return voor;
    }

    /// <summary>Tabellen die leeggemaakt worden; hun teller (Id) begint opnieuw bij 1.</summary>
    internal static readonly string[] LeeggemaakteTabellen =
    {
        "VoorraadMutaties", "VoorraadAlerts", "BestelbonLijnen", "Ontvangsten", "Bestelbonnen", "WerkTaken",
        "LeverancierBestelLijnen", "LeverancierBestellingen", "WerkBonnen", "OfferteRegels", "Offertes",
        "OfferteArchieven", "WerkBonArchieven", "Klanten", "ImportRowLogs", "ImportSessions", "FoutLogs", "AuditLogs"
    };

    private static async Task ZetTellersTerugAsync(AppDbContext db)
    {
        var tabellen = LeeggemaakteTabellen
            .Select(t => db.Model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == t)?.GetTableName())
            .Where(t => t is not null)
            .Cast<string>()
            .ToList();

        if (db.Database.IsNpgsql())
        {
            foreach (var t in tabellen)
            {
                // Werkt voor serial- en identity-kolommen; 'false' = de volgende waarde wordt 1.
                // De tabelnaam komt uit de vaste lijst hierboven, niet van de gebruiker.
                var sql = "SELECT setval(pg_get_serial_sequence('\"" + t + "\"', 'Id'), 1, false);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
        else if (db.Database.IsSqlite())
        {
            var bestaat = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_sequence'")
                .SingleAsync();
            if (bestaat > 0)
            {
                var lijst = string.Join(", ", tabellen.Select(t => "'" + t + "'"));
                var sql = "DELETE FROM sqlite_sequence WHERE name IN (" + lijst + ");";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
    }

    private static async Task<StartklaarOverzicht> TelAsync(AppDbContext db)
    {
        var weg = new List<StartklaarRegel>
        {
            new("Klanten", await db.Klanten.IgnoreQueryFilters().CountAsync()),
            new("Offertes", await db.Offertes.IgnoreQueryFilters().CountAsync()),
            new("Inlijstingen op offertes", await db.OfferteRegels.IgnoreQueryFilters().CountAsync()),
            new("Werkbonnen", await db.WerkBonnen.IgnoreQueryFilters().CountAsync()),
            new("Werktaken in de planning", await db.WerkTaken.IgnoreQueryFilters().CountAsync()),
            new("Bestelbonnen", await db.Bestelbonnen.IgnoreQueryFilters().CountAsync()),
            new("Betalingen, voorschotten en winkelverkopen", await db.Ontvangsten.IgnoreQueryFilters().CountAsync()),
            new("Archief (offertes en werkbonnen)",
                await db.OfferteArchieven.IgnoreQueryFilters().CountAsync() + await db.WerkBonArchieven.IgnoreQueryFilters().CountAsync()),
            new("Bestellingen bij leveranciers", await db.LeverancierBestellingen.IgnoreQueryFilters().CountAsync()),
            new("Voorraadgeschiedenis en -meldingen",
                await db.VoorraadMutaties.IgnoreQueryFilters().CountAsync() + await db.VoorraadAlerts.IgnoreQueryFilters().CountAsync()),
            new("Importlogboek", await db.ImportSessions.IgnoreQueryFilters().CountAsync()),
            new("Foutmeldingen", await db.FoutLogs.IgnoreQueryFilters().CountAsync()),
            new("Auditlog", await db.AuditLogs.IgnoreQueryFilters().CountAsync()),
        };

        var blijft = new List<StartklaarRegel>
        {
            new("Lijsten (met hun voorraad)", await db.TypeLijsten.IgnoreQueryFilters().CountAsync()),
            new("Afwerkingen", await db.AfwerkingsOpties.IgnoreQueryFilters().CountAsync()),
            new("Standaardkaders", await db.KantKlaarKaders.IgnoreQueryFilters().CountAsync()),
            new("Leveranciers", await db.Leveranciers.IgnoreQueryFilters().CountAsync()),
            new("Gebruikers", await db.Gebruikers.IgnoreQueryFilters().CountAsync()),
            new("Instellingen", await db.Instellingen.IgnoreQueryFilters().CountAsync()),
            new("Geblokkeerde dagen", await db.GeblokkeerDagen.IgnoreQueryFilters().CountAsync()),
        };

        return new StartklaarOverzicht(weg, blijft);
    }
}
