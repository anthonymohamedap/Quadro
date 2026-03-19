using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class ExportCenterViewModel : ObservableObject, IAsyncInitializable
{
    private readonly INavigationService _nav;
    private readonly ICentralExcelExportService _exportService;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChooseExportFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportKlantenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportLijstenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportAfwerkingenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportLeveranciersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportAllesCommand))]
    private bool isBusy;

    [ObservableProperty] private string exportFolder = GetDefaultExportFolder();
    [ObservableProperty] private string statusMessage = "Kies een exportactie om een Excel-bestand aan te maken.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastExportPath))]
    private string? lastExportPath;

    public ExportCenterViewModel(
        INavigationService nav,
        ICentralExcelExportService exportService,
        IFilePickerService filePicker,
        IToastService toast)
    {
        _nav = nav;
        _exportService = exportService;
        _filePicker = filePicker;
        _toast = toast;
    }

    public Task InitializeAsync()
    {
        ExportFolder = EnsureExportFolder(ExportFolder);
        return Task.CompletedTask;
    }

    private bool CanExport() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ChooseExportFolderAsync()
    {
        var selectedFolder = await _filePicker.PickFolderAsync("Selecteer exportmap");
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            StatusMessage = "Mapselectie geannuleerd.";
            return;
        }

        ExportFolder = EnsureExportFolder(selectedFolder);
        StatusMessage = $"Exportmap ingesteld op: {ExportFolder}";
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportKlantenAsync() => ExportSingleAsync(ExcelExportDataset.Klanten);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportLijstenAsync() => ExportSingleAsync(ExcelExportDataset.Lijsten);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAfwerkingenAsync() => ExportSingleAsync(ExcelExportDataset.Afwerkingen);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportLeveranciersAsync() => ExportSingleAsync(ExcelExportDataset.Leveranciers);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAllesAsync()
    {
        try
        {
            IsBusy = true;
            ExportFolder = EnsureExportFolder(ExportFolder);
            StatusMessage = "Alle Excel-exporten worden aangemaakt...";

            foreach (var dataset in new[]
            {
                ExcelExportDataset.Klanten,
                ExcelExportDataset.Lijsten,
                ExcelExportDataset.Afwerkingen,
                ExcelExportDataset.Leveranciers
            })
            {
                var result = await _exportService.ExportAsync(dataset, ExportFolder);
                LastExportPath = result.BestandPad;
            }

            StatusMessage = $"Alle exporten aangemaakt in {ExportFolder}";
            _toast.Success("Alle Excel-exporten zijn voltooid.");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = $"Export mislukt: {message}";
            _toast.Error(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<HomeViewModel>();

    private async Task ExportSingleAsync(ExcelExportDataset dataset)
    {
        try
        {
            IsBusy = true;
            ExportFolder = EnsureExportFolder(ExportFolder);
            StatusMessage = $"{GetDisplayName(dataset)} worden geëxporteerd...";

            var result = await _exportService.ExportAsync(dataset, ExportFolder);
            LastExportPath = result.BestandPad;
            StatusMessage = $"{result.Message} Bestand: {result.BestandPad}";
            _toast.Success($"{GetDisplayName(dataset)} geëxporteerd.");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = $"Export mislukt: {message}";
            _toast.Error(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetDefaultExportFolder()
    {
        return Path.Combine(AppContext.BaseDirectory, "exports");
    }

    public bool HasLastExportPath => !string.IsNullOrWhiteSpace(LastExportPath);

    private static string EnsureExportFolder(string folder)
    {
        var normalized = Path.GetFullPath(folder);
        Directory.CreateDirectory(normalized);
        return normalized;
    }

    private static string GetDisplayName(ExcelExportDataset dataset) => dataset switch
    {
        ExcelExportDataset.Klanten => "Klanten",
        ExcelExportDataset.Lijsten => "Lijsten",
        ExcelExportDataset.Afwerkingen => "Afwerkingen",
        ExcelExportDataset.Leveranciers => "Leveranciers",
        _ => dataset.ToString()
    };
}
