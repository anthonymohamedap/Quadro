namespace QuadroApp.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.Import;
using System;
using System.Collections.ObjectModel;
using System.Linq;

public partial class KlantExcelPreviewViewModel : ObservableObject
{
    public ObservableCollection<KlantPreviewRow> Rows { get; }
    public ObservableCollection<ImportIssue> Issues { get; }

    public bool CanImport => Rows.Any(r => r.IsValid);

    private readonly Action<bool> _close;

    public KlantExcelPreviewViewModel(
        ObservableCollection<KlantPreviewRow> rows,
        ObservableCollection<ImportIssue> issues,
        Action<bool> close)
    {
        Rows = rows;
        Issues = issues;
        _close = close;
    }

    [RelayCommand]
    private void Confirm() => _close(true);

    [RelayCommand]
    private void Cancel() => _close(false);
}
