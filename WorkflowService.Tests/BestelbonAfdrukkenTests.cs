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

    private static async Task<int> MaakBonAsync(SqliteTestDatabase scope, BestelbonStatus status)
    {
        await using var db = await scope.Factory.CreateDbContextAsync();
        var bon = new Bestelbon
        {
            Jaar = 2026, VolgNr = 19, BestelbonNummer = "2026-19", KlantNaam = "Test Quadro",
            BestelbonDatum = new DateTime(2026, 10, 28), VervalDatum = new DateTime(2026, 11, 27),
            TotaalInclBtw = 154.86m, VoorschotBedrag = 50m, Status = status,
            Lijnen = { new BestelbonLijn { Omschrijving = "Standaardkader C2 | titel:Trouwfoto", Aantal = 1, Sortering = 1 } }
        };
        db.Bestelbonnen.Add(bon);
        await db.SaveChangesAsync();
        return bon.Id;
    }

    private static BestelbonExportService Sut(SqliteTestDatabase scope)
        => new(scope.Factory, new[] { new PdfBestelbonExporter() });

    [Theory]
    [InlineData(BestelbonStatus.Draft, BestelbonStatus.Geexporteerd)]
    [InlineData(BestelbonStatus.KlaarVoorExport, BestelbonStatus.Geexporteerd)]
    [InlineData(BestelbonStatus.Geexporteerd, BestelbonStatus.Geexporteerd)]
    [InlineData(BestelbonStatus.Betaald, BestelbonStatus.Betaald)]
    [InlineData(BestelbonStatus.Geannuleerd, BestelbonStatus.Geannuleerd)]
    public async Task Afdrukken_lukt_in_elke_status(BestelbonStatus voor, BestelbonStatus na)
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await MaakBonAsync(scope, voor);

        var result = await Sut(scope).AfdrukkenAsync(id, _map);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Path.Combine(_map, "Bestelbon-2026-19.pdf"), result.BestandPad);
        Assert.True(File.Exists(result.BestandPad));

        await using var db = await scope.Factory.CreateDbContextAsync();
        var bon = await db.Bestelbonnen.FindAsync(id);
        Assert.Equal(na, bon!.Status);
        Assert.Equal(result.BestandPad, bon.ExportPad);
    }

    [Fact]
    public async Task Opnieuw_afdrukken_overschrijft_dezelfde_pdf()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var id = await MaakBonAsync(scope, BestelbonStatus.Betaald);

        var eerste = await Sut(scope).AfdrukkenAsync(id, _map);
        var tweede = await Sut(scope).AfdrukkenAsync(id, _map);

        Assert.True(tweede.Success, tweede.Message);
        Assert.Equal(eerste.BestandPad, tweede.BestandPad);
        Assert.Single(Directory.GetFiles(_map));
    }

    [Fact]
    public void Vaste_map_is_Documenten_QuadroApp_Bestelbonnen()
        => Assert.EndsWith(Path.Combine("QuadroApp", "Bestelbonnen"), BestelbonExportService.BestelbonnenMap);
}
