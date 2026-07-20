using QuadroApp.Service.Interfaces;
using System.Diagnostics;
using System.IO;

namespace QuadroApp.Service;

public sealed class PathOpener : IPathOpener
{
    public void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new FileNotFoundException("Bestandspad ontbreekt.", path);

        if (!File.Exists(path))
            throw new FileNotFoundException("Bestand niet gevonden.", path);

        Open(path);
    }

    public void OpenFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new DirectoryNotFoundException("Exportmap ontbreekt.");

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Map niet gevonden: {folder}");

        Open(folder);
    }

    private static void Open(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
