using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using QuadroApp.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

public sealed class ExportCenterViewModelTests
{
    [Fact]
    public async Task InitializeAsync_HersteltOpgeslagenPresetEnMap()
    {
        var settings = new TestAppSettingsProvider
        {
            LastExportFolder = Path.Combine(Path.GetTempPath(), "QuadroExportSettings"),
            LastExportDataset = ExcelExportDataset.Offertes,
            LastExportPreset = "offertebundel"
        };

        var vm = CreateViewModel(settings, out _, out _);

        await vm.InitializeAsync();

        Assert.Equal(Path.GetFullPath(settings.LastExportFolder), vm.ExportFolder);
        Assert.Equal("offertebundel", vm.SelectedPreset?.Sleutel);
        Assert.Equal(ExcelExportDataset.Offertes, vm.SelectedDataset?.Dataset);
        Assert.Contains("offertebundel", vm.SelectedPreset?.Sleutel);
        Assert.Equal(2, vm.BeschikbareEntiteiten.Count);
    }

    [Fact]
    public async Task AanpassenSelectieNaPreset_MarkeertHandmatigeSelectie()
    {
        var settings = new TestAppSettingsProvider
        {
            LastExportDataset = ExcelExportDataset.Offertes,
            LastExportPreset = "offertebundel"
        };

        var vm = CreateViewModel(settings, out _, out _);
        await vm.InitializeAsync();

        vm.BeschikbareKolommen[0].IsGeselecteerd = false;

        Assert.Contains("handmatig aangepast", vm.GeselecteerdePresetBeschrijving);
    }

    [Fact]
    public async Task ExporteerEnOpenCommands_GebruikenInstellingenEnPathOpener()
    {
        var settings = new TestAppSettingsProvider();
        var vm = CreateViewModel(settings, out var service, out var opener);
        await vm.InitializeAsync();

        var exportDirectory = Path.Combine(Path.GetTempPath(), "QuadroExportVm", Path.GetRandomFileName());
        Directory.CreateDirectory(exportDirectory);
        vm.ExportFolder = exportDirectory;

        var exportPath = Path.Combine(exportDirectory, "test-export.xlsx");
        await File.WriteAllTextAsync(exportPath, "dummy");
        service.NextExportResult = ExportResult.Ok(exportPath, "Export gelukt.");

        try
        {
            vm.BeschikbareEntiteiten[1].IsGeselecteerd = true;
            await vm.ExporteerConfiguratieCommand.ExecuteAsync(null);

            Assert.Equal(exportPath, vm.LastExportPath);
            Assert.Equal(Path.GetFullPath(exportDirectory), settings.LastExportFolder);
            Assert.Equal([2], service.LastAanvraag!.EntiteitIds);

            vm.OpenLastExportCommand.Execute(null);
            vm.OpenExportFolderCommand.Execute(null);

            Assert.Contains(exportPath, opener.OpenedFiles);
            Assert.Contains(Path.GetFullPath(exportDirectory), opener.OpenedFolders);
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GeenGeselecteerdeEntiteiten_BetekentVolledigeDataset()
    {
        var settings = new TestAppSettingsProvider();
        var vm = CreateViewModel(settings, out var service, out _);

        await vm.InitializeAsync();
        await vm.ExporteerConfiguratieCommand.ExecuteAsync(null);

        Assert.NotNull(service.LastAanvraag);
        Assert.Empty(service.LastAanvraag!.EntiteitIds);
        Assert.Contains("Alle 2 records", vm.GeselecteerdeEntiteitenTekst);
    }

    private static ExportCenterViewModel CreateViewModel(
        TestAppSettingsProvider settings,
        out FakeCentralExcelExportService service,
        out TestPathOpener opener)
    {
        service = new FakeCentralExcelExportService();
        opener = new TestPathOpener();

        return new ExportCenterViewModel(
            new TestNavigationService(),
            service,
            new TestFilePickerService(),
            opener,
            settings,
            new TestToastService());
    }

    private sealed class FakeCentralExcelExportService : ICentralExcelExportService
    {
        public ExportResult NextExportResult { get; set; } = ExportResult.Ok(Path.Combine(Path.GetTempPath(), "default-export.xlsx"));
        public ExportAanvraag? LastAanvraag { get; private set; }

        public Task<ExportResult> ExportAsync(ExcelExportDataset dataset, string exportFolder) =>
            Task.FromResult(NextExportResult);

        public Task<ExportResult> ExportAsync(ExportAanvraag aanvraag, string exportFolder)
        {
            LastAanvraag = aanvraag;
            return Task.FromResult(NextExportResult);
        }

        public Task<IReadOnlyList<ExportDatasetOptie>> GetBeschikbareDatasetsAsync() =>
            Task.FromResult<IReadOnlyList<ExportDatasetOptie>>(
            [
                new ExportDatasetOptie
                {
                    Dataset = ExcelExportDataset.Lijsten,
                    Naam = "Lijsten",
                    Beschrijving = "Voorraaddata"
                },
                new ExportDatasetOptie
                {
                    Dataset = ExcelExportDataset.Offertes,
                    Naam = "Offertes",
                    Beschrijving = "Offertekoppen"
                }
            ]);

        public Task<IReadOnlyList<ExportPresetOptie>> GetStandaardPresetsAsync() =>
            Task.FromResult<IReadOnlyList<ExportPresetOptie>>(
            [
                new ExportPresetOptie
                {
                    Sleutel = "voorraadoverzicht",
                    Naam = "Voorraadoverzicht",
                    Beschrijving = "Voorraadpreset",
                    Dataset = ExcelExportDataset.Lijsten
                },
                new ExportPresetOptie
                {
                    Sleutel = "offertebundel",
                    Naam = "Offertebundel",
                    Beschrijving = "Offertes met regels en werkbon.",
                    Dataset = ExcelExportDataset.Offertes
                }
            ]);

        public Task<ExportConfiguratie> MaakConfiguratieAsync(ExcelExportDataset dataset, string? presetSleutel = null)
        {
            var configuratie = new ExportConfiguratie
            {
                Dataset = dataset,
                Titel = dataset.ToString(),
                Beschrijving = dataset == ExcelExportDataset.Offertes ? "Offerte-export" : "Lijsten-export",
                Entiteiten = new ObservableCollection<ExportEntiteitOptie>
                {
                    new() { Id = 1, Label = dataset == ExcelExportDataset.Offertes ? "Offerte 1" : "ART-001", Beschrijving = "Eerste record" },
                    new() { Id = 2, Label = dataset == ExcelExportDataset.Offertes ? "Offerte 2" : "ART-002", Beschrijving = "Tweede record" }
                },
                Kolommen = new ObservableCollection<ExportKolomOptie>
                {
                    new() { Sleutel = "id", Label = "Id", Groep = "Basis", IsGeselecteerd = true },
                    new() { Sleutel = "status", Label = "Status", Groep = "Basis", IsGeselecteerd = dataset == ExcelExportDataset.Offertes }
                },
                Relaties = new ObservableCollection<ExportRelatieOptie>
                {
                    new()
                    {
                        Sleutel = "werkbon",
                        Label = "Werkbon",
                        Beschrijving = "Werkbon-relatie",
                        WerkbladNaam = "Offertes - Werkbon",
                        IsGeselecteerd = presetSleutel == "offertebundel",
                        Kolommen = new ObservableCollection<ExportKolomOptie>
                        {
                            new() { Sleutel = "status", Label = "Status", Groep = "Basis", IsGeselecteerd = presetSleutel == "offertebundel" }
                        }
                    }
                }
            };

            return Task.FromResult(configuratie);
        }
    }
}
