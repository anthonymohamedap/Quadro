using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuadroApp.Model.Import;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class ImportPreviewViewModel : ObservableObject
{
    private readonly IImportPreviewDefinition _definition;
    private readonly QuadroApp.Service.Import.IFilePickerService _filePicker;
    private readonly IToastService _toastService;
    private readonly ILogger<ImportPreviewViewModel> _logger;
    private readonly Action<bool> _close;

    private CancellationTokenSource? _cts;
    private ImportResult<object>? _preview;

    [ObservableProperty] private string? selectedFilePath;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string progressText = "Klaar.";
    [ObservableProperty] private PreviewRowItem? selectedRow;

    public ObservableCollection<PreviewRowItem> Rows { get; } = new();
    public ObservableCollection<ImportRowIssue> SelectedRowIssues { get; } = new();
    public ObservableCollection<KeyValuePair<string, string?>> SelectedRowValues { get; } = new();

    public string EntityName => _definition?.EntityName ?? "Import";

    // Legacy compatibility
    public ObservableCollection<ImportIssue> Issues { get; } = new();
    public int ValidCount => 0;
    public int TotalCount => 0;

    public bool CanConfirm => _preview is not null && _preview.Summary.ValidRows > 0;
    public bool HasWarnings => WarningRows > 0 || InvalidRows > 0;
    public string SummaryText => $"Totaal: {TotalRows} — Geldig: {ValidRows} — Ongeldig: {InvalidRows} — Waarschuwingen: {WarningRows}";
    public string WarningText => "Er zijn fouten of waarschuwingen gevonden. Controleer de details per rij.";

    public IRelayCommand ConfirmCommand { get; }

    public int TotalRows => _preview?.Summary.TotalRows ?? 0;
    public int ValidRows => _preview?.Summary.ValidRows ?? 0;
    public int InvalidRows => _preview?.Summary.InvalidRows ?? 0;
    public int WarningRows => _preview?.Summary.WarningRows ?? 0;
    public int PredictedInsert => _preview?.Summary.InsertCount ?? 0;
    public int PredictedUpdate => _preview?.Summary.UpdateCount ?? 0;
    public int PredictedSkipped => _preview?.Summary.SkippedCount ?? 0;

    public ImportPreviewViewModel(
        IImportPreviewDefinition definition,
        QuadroApp.Service.Import.IFilePickerService filePicker,
        IToastService toastService,
        ILogger<ImportPreviewViewModel> logger,
        Action<bool> close)
    {
        _definition = definition;
        _filePicker = filePicker;
        _toastService = toastService;
        _logger = logger;
        _close = close;
        ConfirmCommand = CommitImportCommand;
    }

    public ImportPreviewViewModel(
        ObservableCollection<TypeLijstPreviewRow> rows,
        ObservableCollection<ImportIssue> issues,
        Action<bool> close)
    {
        _definition = null!;
        _filePicker = null!;
        _toastService = null!;
        _logger = null!;
        _close = close;
        ConfirmCommand = new RelayCommand(() => _close(true));

        foreach (var issue in issues)
        {
            Issues.Add(issue);
        }

        foreach (var row in rows)
        {
            Rows.Add(new PreviewRowItem(row.RowNumber, row.IsValid, false, [], new Dictionary<string, string?>
            {
                ["Artikelnummer"] = row.Artikelnummer,
                ["Leverancier"] = row.Leverancier
                ,["Levcode"] = row.Levcode
            }));
        }
    }

    partial void OnSelectedRowChanged(PreviewRowItem? value)
    {
        SelectedRowIssues.Clear();
        SelectedRowValues.Clear();

        if (value is null)
        {
            return;
        }

        foreach (var issue in value.Issues)
        {
            SelectedRowIssues.Add(issue);
        }

        foreach (var pair in value.Values)
        {
            SelectedRowValues.Add(pair);
        }
    }

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        SelectedFilePath = await _filePicker.PickExcelFileAsync();
    }

    [RelayCommand]
    private async Task DryRunAsync()
    {
        if (_filePicker is null || _definition is null || _toastService is null || _logger is null)
        {
            _close(true);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
        {
            _toastService.Warning("Kies eerst een geldig Excel-bestand.");
            return;
        }
    }

        _cts = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            ProgressText = "Voorbeeld wordt geladen...";

            await using var stream = File.OpenRead(SelectedFilePath);
            _preview = await _definition.DryRunAsync(stream, _cts.Token);

            Rows.Clear();
            foreach (var row in _preview.Rows)
            {
                var values = row.Parsed is null
                    ? new Dictionary<string, string?>()
                    : _definition.ToDisplayMap(row.Parsed);

                Rows.Add(new PreviewRowItem(row.RowNumber, row.IsValid, row.HasWarnings, row.Issues, values));
            }

            if (_preview.Summary.InvalidRows > 0)
            {
                _toastService.Warning($"Import bevat {_preview.Summary.InvalidRows} fouten. Pas het bestand aan en probeer opnieuw.");
            }

            ProgressText = "Voorbeeld klaar.";
            RaiseSummaryProperties();
            _logger.LogInformation("Dry run voltooid voor {EntityName}. Totaal={Total}", EntityName, _preview.Summary.TotalRows);
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Voorbeeld geannuleerd.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dry run mislukt voor {EntityName}", EntityName);
            _toastService.Error("Import mislukt. Bekijk de details.");
            ProgressText = "Voorbeeld mislukt.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CommitImportAsync()
    {
        if (_definition is null || _toastService is null || _logger is null)
        {
            _close(false);
            return;
        }

        if (_preview is null)
        {
            _toastService.Warning("Voer eerst een voorbeeldcontrole uit.");
            return;
        }

        _cts = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            ProgressText = "Import wordt opgeslagen...";

            var receipt = await _definition.CommitAsync(_preview, _cts.Token);
            ProgressText = $"Import klaar. Sessie: {receipt.SessionId}";
            _toastService.Success($"Geïmporteerd: toegevoegd {receipt.Inserted}, bijgewerkt {receipt.Updated}, overgeslagen {receipt.Skipped}.");
            _close(true);
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Import geannuleerd.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Commit mislukt voor {EntityName}", EntityName);
            _toastService.Error("Import mislukt. Bekijk de details.");
            ProgressText = "Import mislukt.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _close(false);

    [RelayCommand]
    private void CancelOperation() => _cts?.Cancel();

    private void RaiseSummaryProperties()
    {
        OnPropertyChanged(nameof(TotalRows));
        OnPropertyChanged(nameof(ValidRows));
        OnPropertyChanged(nameof(InvalidRows));
        OnPropertyChanged(nameof(WarningRows));
        OnPropertyChanged(nameof(PredictedInsert));
        OnPropertyChanged(nameof(PredictedUpdate));
        OnPropertyChanged(nameof(PredictedSkipped));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(CanConfirm));
    }
}

public sealed class PreviewRowItem
{
    public PreviewRowItem(int rowNumber, bool isValid, bool hasWarnings, IReadOnlyCollection<ImportRowIssue> issues, IReadOnlyDictionary<string, string?> values)
    {
        RowNumber = rowNumber;
        IsValid = isValid;
        HasWarnings = hasWarnings;
        Issues = issues;
        Values = values;
    }

    public int RowNumber { get; }
    public bool IsValid { get; }
    public bool HasWarnings { get; }
    public IReadOnlyCollection<ImportRowIssue> Issues { get; }
    public IReadOnlyDictionary<string, string?> Values { get; }
    public string StateLabel => IsValid ? (HasWarnings ? "WAARSCHUWING" : "GELDIG") : "FOUTEN";
    public string Title => $"Rij {RowNumber}";
}

public sealed class PreviewRowItem
{
    public PreviewRowItem(int rowNumber, bool isValid, bool hasWarnings, IReadOnlyCollection<ImportRowIssue> issues, IReadOnlyDictionary<string, string?> values)
    {
        RowNumber = rowNumber;
        IsValid = isValid;
        HasWarnings = hasWarnings;
        Issues = issues;
        Values = values;
    }

    public int RowNumber { get; }
    public bool IsValid { get; }
    public bool HasWarnings { get; }
    public IReadOnlyCollection<ImportRowIssue> Issues { get; }
    public IReadOnlyDictionary<string, string?> Values { get; }
    public string StateLabel => IsValid ? (HasWarnings ? "WAARSCHUWING" : "GELDIG") : "FOUTEN";
    public string Title => $"Rij {RowNumber}";
}
