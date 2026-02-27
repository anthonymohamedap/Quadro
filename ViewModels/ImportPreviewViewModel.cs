using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.Import;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace QuadroApp.ViewModels
{
    public partial class ImportPreviewViewModel : ObservableObject
    {
        public ObservableCollection<TypeLijstPreviewRow> Rows { get; }
        public ObservableCollection<ImportIssue> Issues { get; }

        private readonly Action<bool> _close;

        public ImportPreviewViewModel(
            ObservableCollection<TypeLijstPreviewRow> rows,
            ObservableCollection<ImportIssue> issues,
            Action<bool> close)
        {
            Rows = rows;
            Issues = issues;
            _close = close;

            ConfirmCommand = new RelayCommand(Confirm, () => CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
        }

        public IRelayCommand ConfirmCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public int ValidCount => Rows.Count(r => r.IsValid);
        public int TotalCount => Rows.Count;

        public bool CanConfirm => ValidCount > 0;

        public bool HasWarnings => Issues.Count > 0;

        public string SummaryText => $"Totaal: {TotalCount} rijen — Geldig: {ValidCount} — Issues: {Issues.Count}";
        public string WarningText => "Er zijn issues gevonden. Ongeldige rijen worden overgeslagen.";

        private void Confirm() => _close(true);
        private void Cancel() => _close(false);

        // Als Rows/Issues ooit veranderen, kan je deze oproepen:
        public void Refresh()
        {
            OnPropertyChanged(nameof(ValidCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(WarningText));
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    // Handige UI tekst: Insert/Update/Skip
    public static class TypeLijstPreviewRowUi
    {
        public static string ActionText(this TypeLijstPreviewRow r)
            => r.WillInsert ? "INSERT"
             : r.WillUpdate ? "UPDATE"
             : "SKIP";
    }
}
