using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System.Threading.Tasks;

namespace QuadroApp.Services;

public sealed class LijstDialogService : ILijstDialogService
{
    public async Task<TypeLijst?> EditAsync(TypeLijst lijst)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var owner = desktop.MainWindow;
        if (owner is null) return null;

        var dialog = new LijstDialog(lijst);
        return await dialog.ShowDialog<TypeLijst?>(owner);
    }
}