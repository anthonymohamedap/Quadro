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
    [InlineData(ExcelExportDataset.Offertes, "Offertes", "Concept")]
    public async Task ExportAsync_MaaktExcelBestandMetVerwachteData(
        ExcelExportDataset dataset,
        string expectedWorksheet,
        string expectedCellValue)
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);

        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(dataset, exportDirectory);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.BestandPad));

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, expectedWorksheet, expectedCellValue);
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task MaakConfiguratieAsync_LaadNederlandseVoorraadPreset()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);

        var configuratie = await sut.MaakConfiguratieAsync(ExcelExportDataset.Lijsten, "voorraadoverzicht");

        Assert.Equal("Lijsten", configuratie.Titel);
        Assert.Contains(configuratie.Kolommen, x => x.Label == "Artikelnummer" && x.IsGeselecteerd);
        Assert.Contains(configuratie.Relaties, x => x.Sleutel == "voorraad-alerts");
        Assert.Contains(configuratie.Entiteiten, x => x.Label == "ART-001");
    }

    [Fact]
    public async Task MaakConfiguratieAsync_LaadOffertebundelPresetMetBeideRelaties()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = new CentralExcelExportService(sqlite.Factory);

        var configuratie = await sut.MaakConfiguratieAsync(ExcelExportDataset.Offertes, "offertebundel");

        Assert.Contains(configuratie.Relaties, x => x.Sleutel == "offerte-regels" && x.IsGeselecteerd);
        Assert.Contains(configuratie.Relaties, x => x.Sleutel == "werkbon" && x.IsGeselecteerd);
    }

    [Fact]
    public async Task ExportAsync_KlantenMetOffertes_MaaktRelatieWerkblad()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(new ExportAanvraag
            {
                Dataset = ExcelExportDataset.Klanten,
                KolomSleutels = ["id", "voornaam", "achternaam"],
                Relaties =
                [
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "offertes",
                        KolomSleutels = ["klant", "offerteId", "status"]
                    }
                ]
            }, exportDirectory);

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, "Klanten - Offertes", "Concept");
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_LijstenMetVoorraadMutaties_MaaktRelatieWerkblad()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(new ExportAanvraag
            {
                Dataset = ExcelExportDataset.Lijsten,
                KolomSleutels = ["artikelnummer", "status"],
                Relaties =
                [
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "voorraad-mutaties",
                        KolomSleutels = ["artikelnummer", "mutatieType", "referentie"]
                    }
                ]
            }, exportDirectory);

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, "Lijsten - Mutaties", "Reservatie");
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_MetRelaties_MaaktExtraWerkbladenVoorLeveranciers()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);

        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(new ExportAanvraag
            {
                Dataset = ExcelExportDataset.Leveranciers,
                PresetSleutel = "leverancier-voorraadbundel",
                KolomSleutels = ["id", "code", "aantalLijsten"],
                Relaties =
                [
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "type-lijsten",
                        KolomSleutels = ["leverancier", "artikelnummer", "status"]
                    },
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "bestellingen",
                        KolomSleutels = ["leverancier", "bestelNummer", "status"]
                    },
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "afwerkingen",
                        KolomSleutels = ["leverancier", "naam", "kleur"]
                    }
                ]
            }, exportDirectory);

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, "Leveranciers - Lijsten", "ART-001");
            AssertWorksheetContains(workbook, "Leveranciers - Bestellingen", "BEST-001");
            AssertWorksheetContains(workbook, "Leveranciers - Afwerkingen", "Mat");
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_OffertesMetRegelsEnWerkbon_MaaktBeideRelatieWerkbladen()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(new ExportAanvraag
            {
                Dataset = ExcelExportDataset.Offertes,
                KolomSleutels = ["offerteId", "status", "klant"],
                Relaties =
                [
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "offerte-regels",
                        KolomSleutels = ["titel", "typeLijst", "totaalIncl"]
                    },
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "werkbon",
                        KolomSleutels = ["status", "totaalPrijsIncl"]
                    }
                ]
            }, exportDirectory);

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, "Offertes - Regels", "Poster");
            AssertWorksheetContains(workbook, "Offertes - Werkbon", "Gepland");
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_GooitExceptionZonderHoofdvelden()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ExportAsync(new ExportAanvraag
                {
                    Dataset = ExcelExportDataset.Klanten,
                    KolomSleutels = []
                }, exportDirectory));

            Assert.Contains("minstens één veld", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    [Fact]
    public async Task ExportAsync_MetGeselecteerdeEntiteiten_FiltertHoofdbladEnRelaties()
    {
        await using var sqlite = await DbFactoryBuilder.CreateSqliteAsync();
        await SeedExportDataAsync(sqlite.Factory);
        var tweedeKlantId = await SeedTweedeKlantMetOfferteAsync(sqlite.Factory);
        var sut = new CentralExcelExportService(sqlite.Factory);
        var exportDirectory = CreateExportDirectory();

        try
        {
            var result = await sut.ExportAsync(new ExportAanvraag
            {
                Dataset = ExcelExportDataset.Klanten,
                EntiteitIds = [tweedeKlantId],
                KolomSleutels = ["voornaam", "achternaam", "gemeente"],
                Relaties =
                [
                    new ExportRelatieAanvraag
                    {
                        Sleutel = "offertes",
                        KolomSleutels = ["klant", "offerteId", "status"]
                    }
                ]
            }, exportDirectory);

            using var workbook = new XLWorkbook(result.BestandPad);
            AssertWorksheetContains(workbook, "Klanten", "Piet");
            AssertWorksheetDoesNotContain(workbook, "Klanten", "Jan");
            AssertWorksheetContains(workbook, "Klanten - Offertes", "Peters Piet");
            AssertWorksheetDoesNotContain(workbook, "Klanten - Offertes", "Jansen Jan");
        }
        finally
        {
            DeleteDirectory(exportDirectory);
        }
    }

    private static void AssertWorksheetContains(XLWorkbook workbook, string worksheetName, string expectedCellValue)
    {
        var worksheet = workbook.Worksheet(worksheetName);
        Assert.NotNull(worksheet);
        Assert.Contains(expectedCellValue, worksheet.RangeUsed()!.CellsUsed().Select(c => c.GetFormattedString()));
    }

    private static void AssertWorksheetDoesNotContain(XLWorkbook workbook, string worksheetName, string unexpectedCellValue)
    {
        var worksheet = workbook.Worksheet(worksheetName);
        Assert.NotNull(worksheet);
        Assert.DoesNotContain(unexpectedCellValue, worksheet.RangeUsed()!.CellsUsed().Select(c => c.GetFormattedString()));
    }

    private static string CreateExportDirectory() =>
        Path.Combine(Path.GetTempPath(), "QuadroAppTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string exportDirectory)
    {
        if (Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, recursive: true);
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

        await db.SaveChangesAsync();
        var klant = await db.Klanten.FirstAsync();

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
            Kleur = "Wit",
            KostprijsPerM2 = 12m,
            WinstMarge = 1.5m,
            AfvalPercentage = 2m,
            LeverancierId = leverancier.Id
        });

        db.Offertes.Add(new Offerte
        {
            KlantId = klant.Id,
            Datum = new DateTime(2026, 3, 20),
            Status = OfferteStatus.Concept,
            TotaalInclBtw = 150m,
            GeplandeDatum = new DateTime(2026, 3, 25),
            DeadlineDatum = new DateTime(2026, 3, 27),
            GeschatteMinuten = 90
        });

        await db.SaveChangesAsync();

        var offerte = await db.Offertes.FirstAsync();
        var lijst = await db.TypeLijsten.FirstAsync();
        var afwerking = await db.AfwerkingsOpties.FirstAsync();

        db.OfferteRegels.Add(new OfferteRegel
        {
            OfferteId = offerte.Id,
            Titel = "Poster",
            AantalStuks = 1,
            BreedteCm = 30,
            HoogteCm = 40,
            TypeLijstId = lijst.Id,
            GlasId = afwerking.Id,
            TotaalInclBtw = 150m
        });

        db.LeverancierBestellingen.Add(new LeverancierBestelling
        {
            LeverancierId = leverancier.Id,
            BestelNummer = "BEST-001",
            Status = LeverancierBestellingStatus.Besteld,
            BesteldOp = new DateTime(2026, 3, 21)
        });

        db.WerkBonnen.Add(new WerkBon
        {
            OfferteId = offerte.Id,
            Status = WerkBonStatus.Gepland,
            TotaalPrijsIncl = 150m
        });

        db.VoorraadAlerts.Add(new VoorraadAlert
        {
            TypeLijstId = lijst.Id,
            AlertType = VoorraadAlertType.LowStock,
            Status = VoorraadAlertStatus.Open,
            Bericht = "Bijna op voorraad"
        });

        db.VoorraadMutaties.Add(new VoorraadMutatie
        {
            TypeLijstId = lijst.Id,
            MutatieType = VoorraadMutatieType.Reserve,
            AantalMeter = 4m,
            MutatieDatum = new DateTime(2026, 3, 22, 9, 30, 0),
            Referentie = "Reservatie"
        });

        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedTweedeKlantMetOfferteAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        var klant = new Klant
        {
            Voornaam = "Piet",
            Achternaam = "Peters",
            Gemeente = "Brugge"
        };

        db.Klanten.Add(klant);
        await db.SaveChangesAsync();

        db.Offertes.Add(new Offerte
        {
            KlantId = klant.Id,
            Datum = new DateTime(2026, 4, 1),
            Status = OfferteStatus.Concept,
            TotaalInclBtw = 99m
        });

        await db.SaveChangesAsync();
        return klant.Id;
    }
}
