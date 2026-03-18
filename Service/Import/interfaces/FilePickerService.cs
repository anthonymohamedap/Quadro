using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickExcelFileAsync()
    {
        var storageProvider = ResolveStorageProvider();
        if (storageProvider is null || !storageProvider.CanOpen)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecteer Excel-bestand",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Excel-bestanden")
                {
                    Patterns = new[] { "*.xlsx", "*.xls" },
                    MimeTypes = new[]
                    {
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "application/vnd.ms-excel"
                    }
                },
                FilePickerFileTypes.All
            }
        });

        var selectedFile = files.Count > 0 ? files[0] : null;
        if (selectedFile is null)
        {
            return null;
        }

        return selectedFile.TryGetLocalPath()
            ?? selectedFile.Path?.LocalPath
            ?? selectedFile.Name;
    }

    private static IStorageProvider? ResolveStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var owner = desktop.MainWindow;
        if (owner?.StorageProvider is { } windowStorageProvider)
        {
            return windowStorageProvider;
        }

        var topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        return topLevel?.StorageProvider;
    }
}
