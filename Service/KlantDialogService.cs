using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System.Threading.Tasks;

public sealed class KlantDialogService : IKlantDialogService
{
    public async Task<Klant?> EditAsync(Klant klant)
    {
        var dlg = new KlantDialog(klant);

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner =
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;

            if (owner is not null)
                return await dlg.ShowDialog<Klant?>(owner);

            // Fallback zonder owner: non-modal tonen en wachten tot closed
            var tcs = new TaskCompletionSource<Klant?>();
            dlg.Closed += (_, __) => tcs.TrySetResult(dlg.Result);
            dlg.Show();
            return await tcs.Task;
        });
    }
}