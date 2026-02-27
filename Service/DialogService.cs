using Avalonia.Controls;
using Avalonia.Threading;
using QuadroApp.Model.Import;
using QuadroApp.Service.Interfaces;
using QuadroApp.ViewModels;
using QuadroApp.Views;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
namespace QuadroApp.Service
{
    public sealed class DialogService : IDialogService
    {
        private readonly IWindowProvider _windowProvider;

        public DialogService(IWindowProvider windowProvider)
        {
            _windowProvider = windowProvider;
        }

        public async Task<bool> ShowImportPreviewAsync(
            ObservableCollection<TypeLijstPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null)
                return false;

            bool result = false;

            // 1️⃣ Maak window
            var window = new ImportPreviewWindow();

            // 2️⃣ Sluit-actie (wordt door ViewModel aangeroepen)
            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            // 3️⃣ Maak ViewModel MET close-action
            var vm = new ImportPreviewViewModel(
                previewRows,
                issues,
                Close
            );

            window.DataContext = vm;

            // 4️⃣ Toon dialog (Avalonia-correct)
            await window.ShowDialog<bool>(owner);

            return result;
        }
        public async Task<bool> ShowKlantImportPreviewAsync(
            ObservableCollection<KlantPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null)
                return false;

            bool result = false;

            var window = new KlantImportPreviewWindow();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            window.DataContext = new KlantExcelPreviewViewModel(
                previewRows,
                issues,
                Close
            );

            await window.ShowDialog<bool>(owner);
            return result;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = _windowProvider.GetMainWindow();
                if (owner == null) return;

                var window = new Window
                {
                    Title = title,
                    Width = 520,
                    Height = 220,
                    Content = new TextBlock
                    {
                        Text = message,
                        Margin = new Avalonia.Thickness(16),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };

                await window.ShowDialog(owner);
            });
        }

        public async Task<bool> ConfirmAsync(string title, string message)
        {
            // Simpele confirm voor nu (geen aparte dialog)
            await Task.CompletedTask;
            return true;
        }
        public async Task<bool> ShowAfwerkingImportPreviewAsync(
    ObservableCollection<AfwerkingsOptiePreviewRow> previewRows,
    ObservableCollection<ImportIssue> issues)
        {
            var owner = _windowProvider.GetMainWindow();
            if (owner == null) return false;

            bool result = false;

            var window = new AfwerkingImportPreviewWindow();

            void Close(bool confirmed)
            {
                result = confirmed;
                window.Close(confirmed);
            }

            window.DataContext = new AfwerkingExcelPreviewViewModel(
                previewRows,
                Close
            );

            await window.ShowDialog<bool>(owner);
            return result;
        }

    }
}
