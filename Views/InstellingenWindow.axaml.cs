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

            var auditKnop = this.FindControl<Button>("AuditLogKnop");
            if (auditKnop is not null)
                auditKnop.IsVisible = auth.HeeftPermissie(Permissie.AuditInzien);

            var reconKnop = this.FindControl<Button>("StatusReconciliatieKnop");
            if (reconKnop is not null)
                reconKnop.IsVisible = auth.HeeftPermissie(Permissie.GebruikersBeheren);
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

    private async void AuditLog_Click(object? sender, RoutedEventArgs e)
    {
        var venster = new AuditLogWindow
        {
            DataContext = App.Services.GetRequiredService<AuditLogViewModel>()
        };
        await venster.ShowDialog(this);
    }

    // US-48 — eenmalige/herhaalbare reconciliatie van bestaande offertestatussen.
    private async void StatusReconciliatie_Click(object? sender, RoutedEventArgs e)
    {
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        var toast = App.Services.GetRequiredService<IToastService>();
        var recon = App.Services.GetRequiredService<IOfferteStatusReconciliatieService>();

        var ok = await dialogs.ConfirmAsync(
            "Offertestatussen herberekenen",
            "Alle offertestatussen worden bijgewerkt op basis van hun werkbon en bestelbon " +
            "(alleen vooruit; geannuleerde offertes blijven ongemoeid). Doorgaan?");
        if (!ok) return;

        try
        {
            var aantal = await recon.ReconcilieerAlleAsync();
            toast.Success(aantal == 0
                ? "Alle offertestatussen waren al correct."
                : $"{aantal} offerte(s) bijgewerkt naar de juiste status.");
        }
        catch (System.Exception ex)
        {
            toast.Error($"Herberekenen mislukt: {ex.Message}");
        }
    }
}
