using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// "Naar archief" enkel bij een open offerte (nog geen werkbon), een geannuleerde of een afgeronde verkoop
/// (afspraak 19/11/2026). Een lopende verkoop moet eerst geannuleerd worden in het dossier.
/// </summary>
public class NaarArchiefRegelTests
{
    private static async Task<(OfferteArchiefService Sut, int OfferteId, Func<Task<int>> AantalOffertes, IAsyncDisposable Scope)> MaakAsync(
        OfferteStatus status, WerkBonStatus? werkBon, BestelbonStatus? bon)
    {
        var scope = await DbFactoryBuilder.CreateSqliteAsync();
        int id;
        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var o = new Offerte
            {
                OfferteNummer = 412, Datum = new DateTime(2026, 11, 1), Status = status, TotaalInclBtw = 100m,
                Klant = new Klant { Voornaam = "Test", Achternaam = "Klant" }
            };
            if (werkBon is { } wb) o.WerkBon = new WerkBon { Status = wb, TotaalPrijsIncl = 100m };
            db.Offertes.Add(o);
            if (bon is { } b)
                db.Bestelbonnen.Add(new Bestelbon
                {
                    Offerte = o, Jaar = 2026, VolgNr = 1, BestelbonNummer = "2026-1", KlantNaam = "Test Klant", Status = b,
                    BestelbonDatum = new DateTime(2026, 11, 1), VervalDatum = new DateTime(2026, 12, 1), TotaalInclBtw = 100m
                });
            await db.SaveChangesAsync();
            id = o.Id;
        }
        var sut = new OfferteArchiefService(scope.Factory, new TestAuthService(), NullLogger<OfferteArchiefService>.Instance);
        async Task<int> Aantal() { await using var db = await scope.Factory.CreateDbContextAsync(); return await db.Offertes.CountAsync(); }
        return (sut, id, Aantal, scope);
    }

    [Theory]
    [InlineData(OfferteStatus.Concept, null, null)]                                       // open offerte
    [InlineData(OfferteStatus.Geannuleerd, null, null)]                                   // geannuleerd
    [InlineData(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald)] // afgerond
    public async Task Mag_naar_archief(OfferteStatus status, WerkBonStatus? werkBon, BestelbonStatus? bon)
    {
        var (sut, id, aantal, scope) = await MaakAsync(status, werkBon, bon);
        await using var _ = scope;

        await sut.ArchiveerAsync(id);

        Assert.Equal(0, await aantal());
    }

    [Theory]
    [InlineData(OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland, null)]                   // werkbon
    [InlineData(OfferteStatus.InProductie, WerkBonStatus.InUitvoering, null)]              // in productie
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgewerkt, BestelbonStatus.Draft)]    // klaar om af te halen
    [InlineData(OfferteStatus.Besteld, WerkBonStatus.Afgehaald, BestelbonStatus.Draft)]    // afgehaald, nog niet betaald
    public async Task Lopende_verkoop_moet_eerst_geannuleerd_worden(OfferteStatus status, WerkBonStatus? werkBon, BestelbonStatus? bon)
    {
        var (sut, id, aantal, scope) = await MaakAsync(status, werkBon, bon);
        await using var _ = scope;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ArchiveerAsync(id));

        Assert.Equal(OfferteArchiefService.EerstAnnulerenMelding, ex.Message);
        Assert.Equal(1, await aantal());   // niets gewijzigd
    }
}
