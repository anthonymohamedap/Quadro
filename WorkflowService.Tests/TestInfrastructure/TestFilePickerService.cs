using QuadroApp.Service.Import;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestFilePickerService : IFilePickerService
{
    private readonly Queue<string?> _fileResults = [];
    private readonly Queue<string?> _folderResults = [];

    public int PickCalls { get; private set; }
    public int PickFolderCalls { get; private set; }
    public string? NextResult { get; set; }
    public string? NextFolderResult { get; set; }

    public void EnqueueResult(string? path) => _fileResults.Enqueue(path);
    public void EnqueueFolderResult(string? path) => _folderResults.Enqueue(path);

    public Task<string?> PickExcelFileAsync()
    {
        PickCalls++;

        if (_fileResults.Count > 0)
        {
            return Task.FromResult(_fileResults.Dequeue());
        }

        return Task.FromResult(NextResult);
    }

    public Task<string?> PickFolderAsync(string title)
    {
        PickFolderCalls++;

        if (_folderResults.Count > 0)
        {
            return Task.FromResult(_folderResults.Dequeue());
        }

        return Task.FromResult(NextFolderResult);
    }
}
