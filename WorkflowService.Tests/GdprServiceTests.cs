using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-37 — GDPR export, anonimisering en retentie.</summary>
[Collection("StaticAuthState")]
public class GdprServiceTests
{
    private sealed class FakeAuth : IAuthService
    {
        public bool Toegestaan { get; set; } = true;
        public Gebruiker? CurrentUser => null;
        public event EventHandler? CurrentUserChanged { add { } remove { } }
        public Task<string?> LoginAsync(string g, string w) => Task.FromResult<string?>(null);
        public void Logout() { }
        public bool HeeftPermissie(Permissie p) => Toegestaan;
        public void VereisPermissie(Permissie p) { if (!Toegestaan) throw new OnvoldoendeRechtenException(p); }
        public Task<string?> WijzigWachtwoordAsync(string h, string n) => Task.FromResult<string?>(null);
        public Task SeedDefaultAdminAsync() => Task.CompletedTask;
    }

    private static async Task<int> SeedKlantMetDocumentenAsync(SqliteTestDatabase db)
    {
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var klant = new Klant
        {
            Voornaam = "Griet", Achternaam = "Vermeulen",
            Email = "griet@example.com", Telefoon = "0470123456",
            Straat = "Kerkstraat", Nummer = "12", Postcode = "9000", Gemeente = "Gent"
        };
        var offerte = new Offerte { Klant = klant, Datum = DateTime.Now.AddYears(-1), TotaalInclBtw = 250m };
        ctx.Offertes.Add(offerte);
        await ctx.SaveChangesAsync();

        ctx.Facturen.Add(new Factuur
        {
            OfferteId = offerte.Id, Jaar = 2025, VolgNr = 1, FactuurNummer = "2025-001",
            KlantNaam = "Griet Vermeulen", KlantAdres = "Kerkstraat 12, 9000 Gent",
            FactuurDatum = DateTime.Now.AddYears(-1), VervalDatum = DateTime.Now.AddYears(-1).AddDays(30),
            TotaalInclBtw = 250m
        });
        await ctx.SaveChangesAsync();
        return klant.Id;
    }

    private static GdprService CreateSut(SqliteTestDatabase db, FakeAuth? auth = null) =>
        new(db.Factory, auth ?? new FakeAuth(), NullLogger<GdprService>.Instance);

    [Fact]
    public async Task Export_BevatKlantOffertesEnFacturen()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedKlantMetDocumentenAsync(db);

        var json = await CreateSut(db).ExporteerKlantAsync(id);

        Assert.Contains("Griet", json);
        Assert.Contains("griet@example.com", json);
        Assert.Contains("2025-001", json);
        Assert.Contains("250", json);
    }

    [Fact]
    public async Task Anonimiseer_ScrubtKlant_MaarFactuurBlijftIntact()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedKlantMetDocumentenAsync(db);

        await CreateSut(db).AnonimiseerKlantAsync(id);

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var klant = await ctx.Klanten.IgnoreQueryFilters().SingleAsync(k => k.Id == id);
        Assert.Equal(GdprService.Geanonimiseerd, klant.Voornaam);
        Assert.Null(klant.Email);
        Assert.Null(klant.Telefoon);
        Assert.Null(klant.Straat);
        Assert.True(klant.IsGearchiveerd);

        // Factuur = boekhouddocument: naam-snapshot blijft (wettelijke bewaarplicht)
        var factuur = await ctx.Facturen.SingleAsync();
        Assert.Equal("Griet Vermeulen", factuur.KlantNaam);
    }

    [Fact]
    public async Task Anonimiseer_ScrubtOudeAuditRecords()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        AuditContext.Reset();
        var id = await SeedKlantMetDocumentenAsync(db);

        // Wijziging vóór anonimisering → audit-record met persoonsdata
        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var klant = await ctx.Klanten.SingleAsync(k => k.Id == id);
            klant.Telefoon = "0479999999";
            await ctx.SaveChangesAsync();
        }

        await CreateSut(db).AnonimiseerKlantAsync(id);

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var logs = await ctx.AuditLogs
                .Where(a => a.EntiteitType == nameof(Klant) && a.EntiteitId == id.ToString())
                .ToListAsync();
            Assert.NotEmpty(logs);
            Assert.All(logs, l =>
            {
                Assert.DoesNotContain("0479999999", l.Wijzigingen);
                Assert.DoesNotContain("Vermeulen", l.Wijzigingen);
                Assert.DoesNotContain("griet@example.com", l.Wijzigingen);
            });
        }
    }

    [Fact]
    public async Task ZonderPermissie_WordtGeweigerd()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedKlantMetDocumentenAsync(db);
        var sut = CreateSut(db, new FakeAuth { Toegestaan = false });

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.ExporteerKlantAsync(id));
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.AnonimiseerKlantAsync(id));
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.VindKandidatenVoorbijRetentieAsync());
    }

    [Fact]
    public async Task Retentie_VindtAlleenOudeKlanten()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            var oud = new Klant { Voornaam = "Oude", Achternaam = "Klant" };
            var recent = new Klant { Voornaam = "Recente", Achternaam = "Klant" };
            ctx.Offertes.Add(new Offerte { Klant = oud, Datum = DateTime.Now.AddYears(-10) });
            ctx.Offertes.Add(new Offerte { Klant = recent, Datum = DateTime.Now.AddMonths(-2) });
            ctx.Klanten.Add(new Klant { Voornaam = "Zonder", Achternaam = "Activiteit" }); // ook kandidaat
            await ctx.SaveChangesAsync();
        }

        var kandidaten = await CreateSut(db).VindKandidatenVoorbijRetentieAsync();

        Assert.Equal(2, kandidaten.Count);
        Assert.Contains(kandidaten, k => k.Naam.Contains("Oude"));
        Assert.Contains(kandidaten, k => k.Naam.Contains("Zonder"));
        Assert.DoesNotContain(kandidaten, k => k.Naam.Contains("Recente"));
    }

    [Fact]
    public async Task Retentie_RespecteertInstellingUitDatabase()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();

        await using (var ctx = await db.Factory.CreateDbContextAsync())
        {
            ctx.Instellingen.Add(new Instelling { Sleutel = GdprService.RetentieSleutel, Waarde = "1" });
            var klant = new Klant { Voornaam = "Twee", Achternaam = "JaarOud" };
            ctx.Offertes.Add(new Offerte { Klant = klant, Datum = DateTime.Now.AddYears(-2) });
            await ctx.SaveChangesAsync();
        }

        var kandidaten = await CreateSut(db).VindKandidatenVoorbijRetentieAsync();

        Assert.Single(kandidaten); // 2 jaar oud > retentie van 1 jaar
    }
}
