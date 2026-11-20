using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>
/// Import van lijsten op PostgreSQL: een getal dat te groot is voor zijn kolom (bv. een voorraad van
/// 999.999.999.999.996 m uit het oude systeem) liet de hele import mislukken met "numeric field overflow",
/// en daarna verborg een tweede fout ("gelijktijdig gewijzigd") de echte oorzaak.
/// </summary>
public class ImportGetalGrenzenTests
{
    private static MemoryStream LijstenExcel(params (string Code, string Stock)[] rijen)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Lijsten");
            string[] h = { "Code", "Leverancier", "Levcode", "Type", "Breedte", "Stock", "Minstock", "Inventariskost" };
            for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
            for (var r = 0; r < rijen.Length; r++)
            {
                ws.Cell(r + 2, 1).Value = rijen[r].Code;
                ws.Cell(r + 2, 2).Value = "ICO";
                ws.Cell(r + 2, 3).Value = "L" + r;
                ws.Cell(r + 2, 4).Value = "HOU";
                ws.Cell(r + 2, 5).Value = 30;
                ws.Cell(r + 2, 6).Value = rijen[r].Stock;
            }
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static TypeLijstImportDefinition Definitie(ImportService svc)
        => new(svc, new TypeLijstExcelMap(), new TypeLijstImportValidator(), new TypeLijstImportCommitter());

    [Fact]
    public void Grenzen_komen_uit_de_precisie_van_het_model()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        var grenzen = ImportService.GetalGrenzen(db, typeof(TypeLijst)).ToDictionary(g => g.Eigenschap.Name, g => g.Max);
        Assert.Equal(99_999_999.99m, grenzen[nameof(TypeLijst.VoorraadMeter)]);
        Assert.Equal(999.99m, grenzen[nameof(TypeLijst.AfvalPercentage)]);
        Assert.Equal(999.999m, grenzen[nameof(TypeLijst.WinstFactor)]);
    }

    [Fact]
    public async Task Te_groot_getal_is_een_fout_in_het_voorbeeld_de_rest_wordt_geimporteerd()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var svc = new ImportService(scope.Factory, new ClosedXmlExcelParser(), NullLogger<ImportService>.Instance);
        var def = Definitie(svc);

        await using var bestand = LijstenExcel(("406/9N", "12,5"), ("112005/8E", "999999999999996"), ("286/5O", "-220000000000001"));
        var voorbeeld = await def.DryRunAsync(bestand, CancellationToken.None);

        Assert.Equal(1, voorbeeld.Rows.Count(r => r.IsValid));
        var fout = voorbeeld.Rows.Where(r => !r.IsValid).SelectMany(r => r.Issues).ToList();
        Assert.Equal(2, fout.Count(i => i.ColumnName == nameof(TypeLijst.VoorraadMeter) && i.Message.StartsWith("Getal te groot")));

        var ontvangst = await def.CommitAsync(voorbeeld, CancellationToken.None);
        Assert.Equal(1, ontvangst.Inserted);
        await using var db = await scope.Factory.CreateDbContextAsync();
        Assert.Equal(12.5m, (await db.TypeLijsten.SingleAsync()).VoorraadMeter);
    }

    private sealed class FoutCommitter : IImportCommitter<TypeLijst>
    {
        public Task<(int inserted, int updated, int skipped)> CommitAsync(IReadOnlyList<ImportRowResult<TypeLijst>> validRows, AppDbContext db, CancellationToken ct)
            => throw new InvalidOperationException("echte oorzaak");
    }

    [Fact]
    public async Task Mislukte_import_toont_de_echte_fout_en_staat_in_het_logboek()
    {
        await using var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var svc = new ImportService(scope.Factory, new ClosedXmlExcelParser(), NullLogger<ImportService>.Instance);
        await using var bestand = LijstenExcel(("406/9N", "1"));
        var voorbeeld = await svc.DryRunAsync(bestand, new TypeLijstExcelMap(), new TypeLijstImportValidator(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CommitAsync(voorbeeld, new FoutCommitter(), CancellationToken.None));
        Assert.Equal("echte oorzaak", ex.Message);

        await using var db = await scope.Factory.CreateDbContextAsync();
        var sessie = await db.ImportSessions.SingleAsync();
        Assert.Equal("Failed", sessie.Status);
        Assert.Equal("echte oorzaak", sessie.ErrorMessage);
        Assert.Equal(0, await db.TypeLijsten.CountAsync());
    }
}
