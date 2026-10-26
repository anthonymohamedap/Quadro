using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;
using System.IO;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-82 — bestelbon afdrukken met één knop, in elke status.</summary>
public class BestelbonAfdrukkenTests : IDisposable
{
    private readonly string _map = Path.Combine(Path.GetTempPath(), "quadro-us82-" + Guid.NewGuid().ToString("N"));

    public BestelbonAfdrukkenTests()
        => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    public void Dispose()
    {
        if (Directory.Exists(_map)) Directory.Delete(_map, recursive: true);
    }

    private static async Task<int> MaakBonAsync(SqliteTestDatabase scope, FactuurStatus status)
    {
        await using var db = await scope.Factory.CreateDbContextAsync();
        var bon = new Factuur
        {
            Jaar = 2026, VolgNr = 19, FactuurNummer = "2026-19", KlantNaam = "Test Quadro",
            FactuurDatum = new DateTime(2026, 10, 28), VervalDatum = new DateTime(2026, 11, 27),
            TotaalInclBtw = 154.86m, VoorschotBedrag = 50m, Status = status,
            Lijnen = { new FactuurLijn { Omschrijving = "Standaardkader C2 | titel:Trouwfoto", Aantal = 1, Sortering = 1 } }
        };
        db.Facturen.Add(bon);
        await db.SaveChangesAsync();
        return bon.Id;
    }

    private static FactuurExportService Sut(SqliteTestDatabase scope)
        => new(scope.Factory, new[] { new PdfFactuurExporter() });

    [Theory]
    [InlineData(FactuurStatus.Draft, FactuurStatus.Geexporteerd)]
    [InlineData(FactuurStatus.KlaarVoorExport, FactuurStatus.Geexporteerd)]
    [InlineData(FactuurStatus.Geexporteerd, FactuurStatus.Geexporteerd)]
    [InlineData(FactuurStatus.Betaald, FactuurStatus.Betaald)]
    [InlineData(FactuurStatus.Geannuleerd, FactuurStatus.Geannuleerd)]
    public async Task Afdrukken_lukt_in_elke_status(FactuurStatus voor, FactuurStatus na)
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await MaakBonAsync(scope, voor);

        var result = await Sut(scope).AfdrukkenAsync(id, _map);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Path.Combine(_map, "Bestelbon-2026-19.pdf"), result.BestandPad);
        Assert.True(File.Exists(result.BestandPad));

        await using var db = await scope.Factory.CreateDbContextAsync();
        var bon = await db.Facturen.FindAsync(id);
        Assert.Equal(na, bon!.Status);
        Assert.Equal(result.BestandPad, bon.ExportPad);
    }

    [Fact]
    public async Task Opnieuw_afdrukken_overschrijft_dezelfde_pdf()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await MaakBonAsync(scope, FactuurStatus.Betaald);

        var eerste = await Sut(scope).AfdrukkenAsync(id, _map);
        var tweede = await Sut(scope).AfdrukkenAsync(id, _map);

        Assert.True(tweede.Success, tweede.Message);
        Assert.Equal(eerste.BestandPad, tweede.BestandPad);
        Assert.Single(Directory.GetFiles(_map));
    }

    [Fact]
    public void Vaste_map_is_Documenten_QuadroApp_Bestelbonnen()
        => Assert.EndsWith(Path.Combine("QuadroApp", "Bestelbonnen"), FactuurExportService.BestelbonnenMap);
}
