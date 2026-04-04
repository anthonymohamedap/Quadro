using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class FactuurPreviewViewModel : ObservableObject
{
    private readonly int _factuurId;
    private readonly IFactuurWorkflowService _workflow;
    private readonly IFactuurExportService _exportService;
    private readonly IFilePickerService _filePickerService;
    private readonly IToastService _toast;

    [ObservableProperty] private Factuur factuur;
    [ObservableProperty] private string? previewPad;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<FactuurLijn> Lijnen { get; } = new();

    public Action? RequestClose { get; set; }

    public string Titel => $"{Factuur.DocumentType} {Factuur.FactuurNummer}";
    public string StatusTekst => $"Status: {Factuur.Status}";

    public FactuurPreviewViewModel(
        int factuurId,
        Factuur factuur,
        IFactuurWorkflowService workflow,
        IFactuurExportService exportService,
        IFilePickerService filePickerService,
        IToastService toast)
    {
        _factuurId = factuurId;
        _workflow = workflow;
        _exportService = exportService;
        _filePickerService = filePickerService;
        _toast = toast;
        this.factuur = factuur;
        SyncLijnen();
    }

    public async Task InitializeAsync()
    {
        await ReloadFactuurAsync();
        await EnsurePreviewAsync();
    }

    partial void OnFactuurChanged(Factuur value)
    {
        SyncLijnen();
        OnPropertyChanged(nameof(Titel));
        OnPropertyChanged(nameof(StatusTekst));
    }

    [RelayCommand]
    private async Task OpenPreviewAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            await EnsurePreviewAsync();

            if (string.IsNullOrWhiteSpace(PreviewPad) || !File.Exists(PreviewPad))
                throw new InvalidOperationException("Previewbestand niet gevonden.");

            Process.Start(new ProcessStartInfo
            {
                FileName = PreviewPad,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BewaarBijOfferte()
    {
        _toast.Success("Factuur blijft gekoppeld aan deze offerte.");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task ExporteerAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;

            var exportFolder = await _filePickerService.PickFolderAsync("Kies een map voor de factuur-PDF");
            if (string.IsNullOrWhiteSpace(exportFolder))
            {
                _toast.Info("Export geannuleerd.");
                return;
            }

            ExportResult result;
            if (Factuur.Status is FactuurStatus.Draft)
            {
                await _workflow.MarkeerKlaarVoorExportAsync(Factuur.Id);
                result = await _exportService.ExportAsync(Factuur.Id, ExportFormaat.Pdf, exportFolder);
            }
            else if (Factuur.Status is FactuurStatus.KlaarVoorExport)
            {
                result = await _exportService.ExportAsync(Factuur.Id, ExportFormaat.Pdf, exportFolder);
            }
            else
            {
                result = await _exportService.GeneratePreviewAsync(Factuur.Id, ExportFormaat.Pdf, exportFolder);
            }

            if (result.Success)
                _toast.Success(result.Message);
            else
                _toast.Error(result.Message);

            await ReloadFactuurAsync();
            await EnsurePreviewAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Sluit() => RequestClose?.Invoke();

    private async Task ReloadFactuurAsync()
    {
        Factuur = await _workflow.GetFactuurAsync(_factuurId)
            ?? throw new InvalidOperationException("Factuur niet gevonden.");
    }

    private async Task EnsurePreviewAsync()
    {
        var previewFolder = Path.Combine(AppContext.BaseDirectory, "preview");
        Directory.CreateDirectory(previewFolder);

        var result = await _exportService.GeneratePreviewAsync(Factuur.Id, ExportFormaat.Pdf, previewFolder);
        if (!result.Success)
            throw new InvalidOperationException(result.Message);

        PreviewPad = result.BestandPad;
    }

    private void SyncLijnen()
    {
        Lijnen.Clear();
        foreach (var lijn in Factuur.Lijnen.OrderBy(x => x.Sortering))
            Lijnen.Add(lijn);
    }
}
