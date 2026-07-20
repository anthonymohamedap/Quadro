using QuadroApp.Service.Interfaces;
using System.Collections.Generic;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestPathOpener : IPathOpener
{
    public List<string> OpenedFiles { get; } = [];
    public List<string> OpenedFolders { get; } = [];

    public void OpenFile(string path) => OpenedFiles.Add(path);

    public void OpenFolder(string folder) => OpenedFolders.Add(folder);
}
