using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System.Threading.Tasks;

namespace QuadroApp.Services;

public sealed class LijstDialogService : ILijstDialogService
{
    public async Task<TypeLijst?> EditAsync(TypeLijst lijst)
    {
        if (lijst is null)
        {
            return null;
        }

        var dialog = new LijstDialog(lijst);

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return await ShowWithoutOwnerAsync(dialog);
            }

            var owner = desktop.MainWindow;
            if (owner is null)
            {
                return await ShowWithoutOwnerAsync(dialog);
            }

            return await dialog.ShowDialog<TypeLijst?>(owner);
        });
    }

    private static async Task<TypeLijst?> ShowWithoutOwnerAsync(LijstDialog dialog)
    {
        var tcs = new TaskCompletionSource<TypeLijst?>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.Result);
        dialog.Show();
        return await tcs.Task;
    }
}
