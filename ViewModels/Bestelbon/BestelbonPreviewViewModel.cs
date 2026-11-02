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

public partial class BestelbonPreviewViewModel : ObservableObject
{
    private readonly int _bestelbonId;
    private readonly IBestelbonWorkflowService _workflow;
    private readonly IBestelbonExportService _exportService;
    private readonly IFilePickerService _filePickerService;
    private readonly IToastService _toast;

    [ObservableProperty] private Bestelbon bestelbon;
    [ObservableProperty] private string? previewPad;
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<BestelbonLijn> Lijnen { get; } = new();

    public Action? RequestClose { get; set; }

    public string Titel => $"Bestelbon {Bestelbon.NummerLabel}";   // US-85 (P1): nooit "Bestelbon"
    public string StatusTekst => $"Status: {Bestelbon.Status.Label()}";

    public BestelbonPreviewViewModel(
        int bestelbonId,
        Bestelbon bestelbon,
        IBestelbonWorkflowService workflow,
        IBestelbonExportService exportService,
        IFilePickerService filePickerService,
        IToastService toast)
    {
        _bestelbonId = bestelbonId;
        _workflow = workflow;
        _exportService = exportService;
        _filePickerService = filePickerService;
        _toast = toast;
        this.bestelbon = bestelbon;
        SyncLijnen();
    }

    public async Task InitializeAsync()
    {
        await ReloadBestelbonAsync();
        await EnsurePreviewAsync();
    }

    partial void OnBestelbonChanged(Bestelbon value)
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
        _toast.Success("Bestelbon blijft gekoppeld aan deze offerte.");
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

            var exportFolder = await _filePickerService.PickFolderAsync("Kies een map voor de bestelbon-PDF");
            if (string.IsNullOrWhiteSpace(exportFolder))
            {
                _toast.Info("Export geannuleerd.");
                return;
            }

            ExportResult result;
            if (Bestelbon.Status is BestelbonStatus.Draft && !Bestelbon.IsVoorlopig)
            {
                await _workflow.MarkeerKlaarVoorExportAsync(Bestelbon.Id);
                result = await _exportService.ExportAsync(Bestelbon.Id, ExportFormaat.Pdf, exportFolder);
            }
            else if (Bestelbon.Status is BestelbonStatus.KlaarVoorExport)
            {
                result = await _exportService.ExportAsync(Bestelbon.Id, ExportFormaat.Pdf, exportFolder);
            }
            else
            {
                result = await _exportService.GeneratePreviewAsync(Bestelbon.Id, ExportFormaat.Pdf, exportFolder);
            }

            if (result.Success)
                _toast.Success(result.Message);
            else
                _toast.Error(result.Message);

            await ReloadBestelbonAsync();
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

    private async Task ReloadBestelbonAsync()
    {
        Bestelbon = await _workflow.GetBestelbonAsync(_bestelbonId)
            ?? throw new InvalidOperationException("Bestelbon niet gevonden.");
    }

    private async Task EnsurePreviewAsync()
    {
        // AppContext.BaseDirectory can be read-only on installed apps → use user temp folder
        var previewFolder = Path.Combine(Path.GetTempPath(), "QuadroPreview");
        Directory.CreateDirectory(previewFolder);

        var result = await _exportService.GeneratePreviewAsync(Bestelbon.Id, ExportFormaat.Pdf, previewFolder);
        if (!result.Success)
            throw new InvalidOperationException(result.Message);

        PreviewPad = result.BestandPad;
    }

    private void SyncLijnen()
    {
        Lijnen.Clear();
        // US-26: de "Meerprijs"-regel wordt niet getoond (zoals op de bestelbon-PDF).
        // De regel blijft wél in Bestelbon.Lijnen zodat het bedrag in het totaal verrekend blijft.
        foreach (var lijn in Bestelbon.Lijnen
                     .Where(l => !l.Omschrijving.Equals("Meerprijs", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Sortering))
            Lijnen.Add(lijn);
    }
}
