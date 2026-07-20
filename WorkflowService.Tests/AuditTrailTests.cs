using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-36 — audit trail via SaveChangesAsync.</summary>
/// <remarks>
/// Zelfde collection als AuthServiceTests: beide raken de statische
/// AuditContext, dus ze mogen niet parallel lopen.
/// </remarks>
[Collection("StaticAuthState")]
public class AuditTrailTests
{
    public AuditTrailTests() => AuditContext.Reset();

    [Fact]
    public async Task Modify_Klant_WritesAuditWithOldAndNewValue()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        AuditContext.CurrentUserName = "veerle";

        int id;
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "Jan", Achternaam = "Peeters" };
            ctx.Klanten.Add(klant);
            await ctx.SaveChangesAsync();
            id = klant.Id;
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var klant = await ctx.Klanten.FindAsync(id);
            klant!.Achternaam = "Janssens";
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var log = await ctx.AuditLogs
                .Where(a => a.EntiteitType == nameof(Klant) && a.Actie == "Gewijzigd")
                .SingleAsync();

            Assert.Equal("veerle", log.Gebruiker);
            Assert.Equal(id.ToString(), log.EntiteitId);
            Assert.Contains("Peeters", log.Wijzigingen);
            Assert.Contains("Janssens", log.Wijzigingen);
        }
    }

    [Fact]
    public async Task Add_Leverancier_WritesAuditWithRealId()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        int id;
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var lev = new Leverancier { Naam = "ICO" };
            ctx.Leveranciers.Add(lev);
            await ctx.SaveChangesAsync();
            id = lev.Id;
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var log = await ctx.AuditLogs
                .SingleAsync(a => a.EntiteitType == nameof(Leverancier) && a.Actie == "Toegevoegd");
            Assert.Equal(id.ToString(), log.EntiteitId); // echte Id, niet 0
            Assert.Equal(AuditContext.Systeem, log.Gebruiker); // niemand ingelogd
        }
    }

    [Fact]
    public async Task Gebruiker_WachtwoordHash_NeverInAuditLog()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.Gebruikers.Add(new Gebruiker
            {
                GebruikersNaam = "test",
                VolledigeNaam = "Test",
                WachtwoordHash = "SUPERGEHEIMEHASH"
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var logs = await ctx.AuditLogs.ToListAsync();
            Assert.All(logs, l => Assert.DoesNotContain("SUPERGEHEIMEHASH", l.Wijzigingen));
        }
    }

    [Fact]
    public async Task NonAuditedEntity_WritesNoAudit()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.Instellingen.Add(new Instelling { Sleutel = "test", Waarde = "x" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            Assert.Empty(await ctx.AuditLogs.ToListAsync());
        }
    }

    [Fact]
    public async Task Delete_Klant_WritesVerwijderdEntry()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        int id;
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var klant = new Klant { Voornaam = "Weg", Achternaam = "Ermee" };
            ctx.Klanten.Add(klant);
            await ctx.SaveChangesAsync();
            id = klant.Id;
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var klant = await ctx.Klanten.FindAsync(id);
            ctx.Klanten.Remove(klant!);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var log = await ctx.AuditLogs
                .SingleAsync(a => a.EntiteitType == nameof(Klant) && a.Actie == "Verwijderd");
            Assert.Equal(id.ToString(), log.EntiteitId);
        }
    }
}
