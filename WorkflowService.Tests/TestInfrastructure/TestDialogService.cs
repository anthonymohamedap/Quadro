using QuadroApp.Model.Import;
using QuadroApp.Service.Import.Enterprise;
using QuadroApp.Service.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public bool UnifiedImportPreviewResult { get; set; } = true;
    public int UnifiedImportPreviewCalls { get; private set; }
    public IImportPreviewDefinition? LastDefinition { get; private set; }
    public string? LastErrorTitle { get; private set; }
    public string? LastErrorMessage { get; private set; }

    [System.Obsolete]
    public Task<bool> ShowImportPreviewAsync(ObservableCollection<TypeLijstPreviewRow> previewRows, ObservableCollection<ImportIssue> issues)
        => Task.FromResult(false);

    public Task ShowErrorAsync(string title, string message)
    {
        LastErrorTitle = title;
        LastErrorMessage = message;
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message)
        => Task.FromResult(ConfirmResult);

    [System.Obsolete]
    public Task<bool> ShowKlantImportPreviewAsync(ObservableCollection<KlantPreviewRow> previewRows, ObservableCollection<ImportIssue> issues)
        => Task.FromResult(false);

    [System.Obsolete]
    public Task<bool> ShowAfwerkingImportPreviewAsync(ObservableCollection<AfwerkingsOptiePreviewRow> previewRows, ObservableCollection<ImportIssue> issues)
        => Task.FromResult(false);

    public Task<bool> ShowUnifiedImportPreviewAsync(IImportPreviewDefinition definition)
    {
        UnifiedImportPreviewCalls++;
        LastDefinition = definition;
        return Task.FromResult(UnifiedImportPreviewResult);
    }
}
