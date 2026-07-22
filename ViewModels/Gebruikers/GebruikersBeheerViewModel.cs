using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>Gebruikersbeheer (admin-only): lijst, aanmaken, (de)activeren.</summary>
public partial class GebruikersBeheerViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public ObservableCollection<Gebruiker> Gebruikers { get; } = new();

    // Formulier nieuwe gebruiker
    [ObservableProperty] private string nieuweGebruikersnaam = "";
    [ObservableProperty] private string nieuweVolledigeNaam = "";
    [ObservableProperty] private string initieelWachtwoord = "";
    [ObservableProperty] private bool nieuweIsAdmin;
    [ObservableProperty] private string? foutmelding;
    [ObservableProperty] private string? succesmelding;
    [ObservableProperty] private Gebruiker? geselecteerdeGebruiker;

    public GebruikersBeheerViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    public async Task InitializeAsync() => await HerlaadAsync();

    private async Task HerlaadAsync()
    {
        Gebruikers.Clear();
        foreach (var g in await _auth.GetGebruikersAsync())
            Gebruikers.Add(g);
    }

    [RelayCommand]
    private async Task MaakGebruikerAsync()
    {
        Foutmelding = null;
        Succesmelding = null;

        var rol = NieuweIsAdmin ? GebruikersRol.Admin : GebruikersRol.Medewerker;
        var fout = await _auth.MaakGebruikerAsync(NieuweGebruikersnaam, NieuweVolledigeNaam, rol, InitieelWachtwoord);
        if (fout is not null)
        {
            Foutmelding = fout;
            return;
        }

        Succesmelding = $"Gebruiker '{NieuweGebruikersnaam.Trim()}' aangemaakt. " +
                        "Het initiële wachtwoord moet bij de eerste login gewijzigd worden.";
        NieuweGebruikersnaam = "";
        NieuweVolledigeNaam = "";
        InitieelWachtwoord = "";
        NieuweIsAdmin = false;
        await HerlaadAsync();
    }

    [RelayCommand]
    private async Task ToggleActiefAsync(Gebruiker? gebruiker)
    {
        if (gebruiker is null) return;
        Foutmelding = null;
        Succesmelding = null;

        var fout = await _auth.ZetActiefAsync(gebruiker.Id, !gebruiker.IsActief);
        if (fout is not null)
        {
            Foutmelding = fout;
            return;
        }
        await HerlaadAsync();
    }
}
