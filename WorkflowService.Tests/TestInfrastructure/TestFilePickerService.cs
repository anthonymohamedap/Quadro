using QuadroApp.Service.Import;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestFilePickerService : IFilePickerService
{
    private readonly Queue<string?> _results = [];

    public int PickCalls { get; private set; }
    public string? NextResult { get; set; }

    public void EnqueueResult(string? path) => _results.Enqueue(path);

    public Task<string?> PickExcelFileAsync()
    {
        PickCalls++;

        if (_results.Count > 0)
        {
            return Task.FromResult(_results.Dequeue());
        }

        return Task.FromResult(NextResult);
    }
}
