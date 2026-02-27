using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.Import;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace QuadroApp.ViewModels;

public sealed partial class AfwerkingExcelPreviewViewModel : ObservableObject
{
    public ObservableCollection<AfwerkingsOptiePreviewRow> Rows { get; }

    public bool CanImport => Rows.Any(r => r.IsValid);

    private readonly Action<bool> _close;

    public AfwerkingExcelPreviewViewModel(
        ObservableCollection<AfwerkingsOptiePreviewRow> rows,
        Action<bool> close)
    {
        Rows = rows;
        _close = close;
    }

    [RelayCommand]
    private void Confirm() => _close(true);

    [RelayCommand]
    private void Cancel() => _close(false);
}
