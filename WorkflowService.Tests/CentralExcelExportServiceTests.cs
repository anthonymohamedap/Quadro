using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Model;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public sealed class CentralExcelExportServiceTests
{
    [Theory]
    [InlineData(ExcelExportDataset.Klanten, "Klanten", "Jan")]
    [InlineData(ExcelExportDataset.Lijsten, "Lijsten", "ART-001")]
    [InlineData(ExcelExportDataset.Afwerkingen, "Afwerkingen", "Mat")]
    [InlineData(ExcelExportDataset.Leveranciers, "Leveranciers", "LEV")]
    public async Task ExportAsync_MaaktExcelBestandMetVerwachteData(
        ExcelExportDataset dataset,
        string expectedWorksheet,
        string expectedCellValue)
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);

        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = Path.Combine(Path.GetTempPath(), "QuadroAppTests", Guid.NewGuid().ToString("N"));

        try
        {
            var result = await sut.ExportAsync(dataset, exportDirectory);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.BestandPad));

            using var workbook = new XLWorkbook(result.BestandPad);
            var worksheet = workbook.Worksheet(expectedWorksheet);
            Assert.NotNull(worksheet);
            Assert.Contains(expectedCellValue, worksheet.RangeUsed()!.CellsUsed().Select(c => c.GetFormattedString()));
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }
    }

    private static async Task SeedExportDataAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (await db.Klanten.AnyAsync())
        {
            return;
        }

        var leverancier = new Leverancier { Naam = "LEV" };
        var groep = new AfwerkingsGroep { Code = 'G', Naam = "Glas" };

        db.Leveranciers.Add(leverancier);
        db.AfwerkingsGroepen.Add(groep);
        await db.SaveChangesAsync();

        db.Klanten.Add(new Klant
        {
            Voornaam = "Jan",
            Achternaam = "Jansen",
            Email = "jan@example.com",
            Gemeente = "Gent"
        });

        db.TypeLijsten.Add(new TypeLijst
        {
            Artikelnummer = "ART-001",
            Levcode = "L001",
            LeverancierId = leverancier.Id,
            BreedteCm = 120,
            Soort = "PVC",
            PrijsPerMeter = 10.5m,
            LaatsteUpdate = new DateTime(2026, 3, 19, 10, 0, 0)
        });

        db.AfwerkingsOpties.Add(new AfwerkingsOptie
        {
            AfwerkingsGroepId = groep.Id,
            Naam = "Mat",
            Volgnummer = 'A',
            KostprijsPerM2 = 12m,
            WinstMarge = 1.5m,
            AfvalPercentage = 2m,
            LeverancierId = leverancier.Id
        });

        await db.SaveChangesAsync();
    }
}
