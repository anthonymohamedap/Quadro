using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public class AuditServiceTests
{
    // ── Parser ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parser_formats_modified_field_old_to_new()
    {
        var regels = AuditWijzigingParser.Beschrijf("{\"Prijs\":{\"oud\":\"10\",\"nieuw\":\"12\"}}");
        Assert.Equal(new[] { "Prijs: 10 → 12" }, regels);
    }

    [Fact]
    public void Parser_formats_added_field_without_old()
    {
        var regels = AuditWijzigingParser.Beschrijf("{\"Naam\":{\"nieuw\":\"Jan\"}}");
        Assert.Equal(new[] { "Naam: — → Jan" }, regels);
    }

    [Fact]
    public void Parser_returns_empty_for_deleted_and_invalid()
    {
        Assert.Empty(AuditWijzigingParser.Beschrijf("{}"));
        Assert.Empty(AuditWijzigingParser.Beschrijf(null));
        Assert.Empty(AuditWijzigingParser.Beschrijf("geen json"));
    }

    // ── Service ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Zoek_filters_by_gebruiker()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService());

        var pagina = await sut.ZoekAsync(new AuditFilter(Gebruiker: "anna"), 0, 100);

        Assert.All(pagina.Records, r => Assert.Equal("anna", r.Gebruiker));
        Assert.Equal(2, pagina.TotaalAantal);
    }

    [Fact]
    public async Task Zoek_filters_by_type_and_actie()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService());

        var pagina = await sut.ZoekAsync(new AuditFilter(EntiteitType: "Offerte", Actie: "Gewijzigd"), 0, 100);

        Assert.All(pagina.Records, r => Assert.Equal("Offerte", r.EntiteitType));
        Assert.All(pagina.Records, r => Assert.Equal("Gewijzigd", r.Actie));
    }

    [Fact]
    public async Task Zoek_orders_newest_first_and_paginates()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService());

        var pagina1 = await sut.ZoekAsync(new AuditFilter(), 0, 2);
        var pagina2 = await sut.ZoekAsync(new AuditFilter(), 2, 2);

        Assert.Equal(5, pagina1.TotaalAantal);
        Assert.Equal(2, pagina1.Records.Count);
        // Nieuwste eerst
        Assert.True(pagina1.Records[0].Tijdstip >= pagina1.Records[1].Tijdstip);
        // Paginering levert andere records
        Assert.Empty(pagina1.Records.Select(r => r.Id).Intersect(pagina2.Records.Select(r => r.Id)));
    }

    [Fact]
    public async Task Zoek_date_range_excludes_out_of_range()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService());

        var toekomst = await sut.ZoekAsync(new AuditFilter(VanafLokaal: DateTime.Now.AddYears(5)), 0, 100);
        var verleden = await sut.ZoekAsync(new AuditFilter(VanafLokaal: DateTime.Now.AddYears(-5)), 0, 100);

        Assert.Equal(0, toekomst.TotaalAantal);
        Assert.Equal(5, verleden.TotaalAantal);
    }

    [Fact]
    public async Task Zoek_requires_AuditInzien_permission()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService { AlleRechten = false });

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(
            () => sut.ZoekAsync(new AuditFilter(), 0, 100));
    }

    [Fact]
    public async Task GetFilterOpties_returns_distinct_sorted_values()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedAsync(scope.Factory);
        var sut = new AuditService(scope.Factory, new TestAuthService());

        var (gebruikers, types, acties) = await sut.GetFilterOptiesAsync();

        Assert.Equal(new[] { "anna", "bram" }, gebruikers);
        Assert.Contains("Offerte", types);
        Assert.Contains("Gewijzigd", acties);
    }

    private static async Task SeedAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var basis = DateTime.UtcNow.AddDays(-1);
        db.AuditLogs.AddRange(
            new AuditLog { Tijdstip = basis.AddMinutes(1), Gebruiker = "anna", EntiteitType = "Offerte", EntiteitId = "1", Actie = "Toegevoegd", Wijzigingen = "{\"Titel\":{\"nieuw\":\"A\"}}" },
            new AuditLog { Tijdstip = basis.AddMinutes(2), Gebruiker = "anna", EntiteitType = "Offerte", EntiteitId = "1", Actie = "Gewijzigd", Wijzigingen = "{\"Titel\":{\"oud\":\"A\",\"nieuw\":\"B\"}}" },
            new AuditLog { Tijdstip = basis.AddMinutes(3), Gebruiker = "bram", EntiteitType = "Factuur", EntiteitId = "9", Actie = "Gewijzigd", Wijzigingen = "{\"Status\":{\"oud\":\"Draft\",\"nieuw\":\"Betaald\"}}" },
            new AuditLog { Tijdstip = basis.AddMinutes(4), Gebruiker = "bram", EntiteitType = "Klant", EntiteitId = "3", Actie = "Verwijderd", Wijzigingen = "{}" },
            new AuditLog { Tijdstip = basis.AddMinutes(5), Gebruiker = "bram", EntiteitType = "TypeLijst", EntiteitId = "7", Actie = "Gewijzigd", Wijzigingen = "{\"PrijsPerMeter\":{\"oud\":\"10\",\"nieuw\":\"11\"}}" });
        await db.SaveChangesAsync();
    }
}
