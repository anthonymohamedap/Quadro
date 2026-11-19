using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// De klant krijgt de bestelbon als prijsvoorstel. Vóór het akkoord is het een voorlopige bon zonder
/// volgnummer; het volgnummer komt pas bij "Bevestigen → Werkbon", zodat klanten die niet
/// bestellen geen gaten in de nummering laten.
/// </summary>
public class BestelbonVolgnummerTests
{
    private static BestelbonWorkflowService Sut(IDbContextFactory<QuadroApp.Data.AppDbContext> factory)
        => new(factory,
            new PricingService(factory, new VastePrijsInstellingen(), new PricingEngine(), NullLogger<PricingService>.Instance),
            new TestAuthService());

    private static async Task<int> SeedAsync(SqliteTestDatabase scope, int offerteNummer, OfferteStatus status = OfferteStatus.Concept)
    {
        await using var db = await scope.Factory.CreateDbContextAsync();
        var offerte = new Offerte
        {
            OfferteNummer = offerteNummer, Datum = DateTime.Today, Status = status,
            Klant = new Klant { Voornaam = "Test", Achternaam = "Quadro" },
            Regels = { new OfferteRegel { AantalStuks = 1, BreedteCm = 30, HoogteCm = 40, AfgesprokenPrijsExcl = 121m } }
        };
        db.Offertes.Add(offerte);
        await db.SaveChangesAsync();
        return offerte.Id;
    }

    [Fact]
    public async Task Bon_voor_het_akkoord_is_voorlopig_zonder_volgnummer()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedAsync(scope, 412);

        var bon = await Sut(scope.Factory).MaakBestelbonVanOfferteAsync(id);

        Assert.True(bon.IsVoorlopig);
        Assert.Null(bon.VolgNr);
        Assert.Equal("V412", bon.BestelbonNummer);
        Assert.Equal("voorlopig (offerte 412)", bon.NummerLabel);
    }

    [Fact]
    public async Task Voorlopige_bonnen_laten_geen_gat_in_de_nummering()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = Sut(scope.Factory);
        var nietBesteld = await SeedAsync(scope, 410);
        var besteld = await SeedAsync(scope, 411, OfferteStatus.Goedgekeurd);

        await sut.MaakBestelbonVanOfferteAsync(nietBesteld);
        var bon = await sut.MaakBestelbonVanOfferteAsync(besteld);

        Assert.False(bon.IsVoorlopig);
        Assert.Equal(1, bon.VolgNr);
        Assert.Equal($"{DateTime.Today.Year}-1", bon.BestelbonNummer);
    }

    [Fact]
    public async Task Bevestigen_kent_het_volgnummer_toe_met_besteldatum_vandaag()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = Sut(scope.Factory);
        var id = await SeedAsync(scope, 412);
        var voorlopig = await sut.MaakBestelbonVanOfferteAsync(id);

        await using (var db = await scope.Factory.CreateDbContextAsync())
        {
            var f = await db.Bestelbonnen.FindAsync(voorlopig.Id);
            f!.BestelbonDatum = DateTime.Today.AddDays(-5);   // prijsvoorstel van een paar dagen geleden
            await db.SaveChangesAsync();
        }

        await sut.KenVolgnummerToeBijBevestigingAsync(id);
        await sut.KenVolgnummerToeBijBevestigingAsync(id);   // tweede keer: niets meer te doen

        await using var check = await scope.Factory.CreateDbContextAsync();
        var bon = await check.Bestelbonnen.SingleAsync();
        Assert.False(bon.IsVoorlopig);
        Assert.Equal(1, bon.VolgNr);
        Assert.Equal($"{DateTime.Today.Year}-1", bon.BestelbonNummer);
        Assert.Equal(DateTime.Today, bon.BestelbonDatum);
    }

    [Fact]
    public async Task Bevestigen_via_de_workflow_nummert_de_voorlopige_bon()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var bestelbonWorkflow = Sut(scope.Factory);
        var id = await SeedAsync(scope, 412, OfferteStatus.Verzonden);
        await bestelbonWorkflow.MaakBestelbonVanOfferteAsync(id);

        var workflow = new QuadroApp.Service.WorkflowService(
            scope.Factory, NullLogger<QuadroApp.Service.WorkflowService>.Instance, new TestToastService(), bestelbonWorkflow);
        await workflow.ChangeOfferteStatusAsync(id, OfferteStatus.Goedgekeurd);

        await using var check = await scope.Factory.CreateDbContextAsync();
        Assert.False((await check.Bestelbonnen.SingleAsync()).IsVoorlopig);
    }

    [Fact]
    public async Task Voorlopige_bon_afdrukken_telt_niet_mee_voor_de_boekhouding()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await SeedAsync(scope, 412);
        var bon = await Sut(scope.Factory).MaakBestelbonVanOfferteAsync(id);
        var map = Path.Combine(Path.GetTempPath(), "quadro-voorlopig-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await new BestelbonExportService(scope.Factory, new[] { new PdfBestelbonExporter() }).AfdrukkenAsync(bon.Id, map);

            Assert.True(result.Success, result.Message);
            Assert.Equal("Bestelbon-V412.pdf", Path.GetFileName(result.BestandPad));
            await using var check = await scope.Factory.CreateDbContextAsync();
            Assert.Equal(BestelbonStatus.Draft, (await check.Bestelbonnen.SingleAsync()).Status);
        }
        finally
        {
            if (Directory.Exists(map)) Directory.Delete(map, recursive: true);
        }
    }

    private sealed class VastePrijsInstellingen : IPricingSettingsProvider
    {
        public Task<decimal> GetUurloonAsync() => Task.FromResult(60m);
        public Task<decimal> GetBtwPercentAsync() => Task.FromResult(21m);
        public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(0m);
        public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(1m);
        public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(10m);
    }
}
