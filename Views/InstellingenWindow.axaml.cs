using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class InstellingenWindow : Window
{
    public InstellingenWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            // Gebruikersbeheer alleen zichtbaar voor beheerders
            var auth = App.Services.GetRequiredService<IAuthService>();
            var knop = this.FindControl<Button>("GebruikersBeheerKnop");
            if (knop is not null)
                knop.IsVisible = auth.HeeftPermissie(Permissie.GebruikersBeheren);
        };
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void WachtwoordWijzigen_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new WachtwoordWijzigDialog
        {
            DataContext = App.Services.GetRequiredService<WachtwoordWijzigViewModel>()
        };
        await dialog.ShowDialog(this);
    }

    private async void GebruikersBeheer_Click(object? sender, RoutedEventArgs e)
    {
        var venster = new GebruikersBeheerWindow
        {
            DataContext = App.Services.GetRequiredService<GebruikersBeheerViewModel>()
        };
        await venster.ShowDialog(this);
    }
}
