using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Import;


public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickExcelFileAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var owner = desktop.MainWindow;
        if (owner is null) return null;

        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters =
            {
                new FileDialogFilter { Name = "Excel", Extensions = { "xlsx", "xls" } },
                new FileDialogFilter { Name = "All", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(owner);
        return result?.FirstOrDefault();
    }
}
