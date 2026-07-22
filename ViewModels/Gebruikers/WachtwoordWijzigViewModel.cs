using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>Deployment-blocker fix — wachtwoord wijzigen van de ingelogde gebruiker.</summary>
public partial class WachtwoordWijzigViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    /// <summary>Gezet door de dialoog; sluit het venster bij succes.</summary>
    public Action? SluitBijSucces { get; set; }

    [ObservableProperty] private string huidigWachtwoord = "";
    [ObservableProperty] private string nieuwWachtwoord = "";
    [ObservableProperty] private string bevestigWachtwoord = "";
    [ObservableProperty] private string? foutmelding;
    [ObservableProperty] private bool isBezig;

    public bool MoetVerplichtWijzigen => _auth.CurrentUser?.MoetWachtwoordWijzigen == true;

    public WachtwoordWijzigViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private async Task OpslaanAsync()
    {
        Foutmelding = null;

        if (NieuwWachtwoord != BevestigWachtwoord)
        {
            Foutmelding = "Nieuw wachtwoord en bevestiging komen niet overeen.";
            return;
        }

        try
        {
            IsBezig = true;
            var fout = await _auth.WijzigWachtwoordAsync(HuidigWachtwoord, NieuwWachtwoord);
            if (fout is not null)
            {
                Foutmelding = fout;
                return;
            }

            SluitBijSucces?.Invoke();
        }
        finally
        {
            IsBezig = false;
        }
    }
}
