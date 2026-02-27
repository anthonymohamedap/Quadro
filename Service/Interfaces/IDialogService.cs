using QuadroApp.Model.Import;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IDialogService
    {
        Task<bool> ShowImportPreviewAsync(
            ObservableCollection<TypeLijstPreviewRow> previewRows,
            ObservableCollection<ImportIssue> issues);

        Task ShowErrorAsync(string title, string message);
        Task<bool> ConfirmAsync(string title, string message);
        Task<bool> ShowKlantImportPreviewAsync(
    ObservableCollection<KlantPreviewRow> previewRows,
    ObservableCollection<ImportIssue> issues);

        Task<bool> ShowAfwerkingImportPreviewAsync(
    ObservableCollection<AfwerkingsOptiePreviewRow> previewRows,
    ObservableCollection<ImportIssue> issues);

    }
}
