using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>Eén rij in het rechten-overzicht ("wat mag welke rol").</summary>
public sealed record RechtRij(string Actie, bool Admin, bool Medewerker)
{
    public string AdminTeken => Admin ? "✓" : "✗";
    public string MedewerkerTeken => Medewerker ? "✓" : "✗";
}

/// <summary>Gebruikersbeheer (admin-only): lijst, aanmaken, (de)activeren.</summary>
public partial class GebruikersBeheerViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public ObservableCollection<Gebruiker> Gebruikers { get; } = new();

    /// <summary>
    /// Rechten-overzicht, rechtstreeks opgebouwd uit <see cref="RolPermissies"/>
    /// zodat het scherm nooit kan afwijken van de werkelijke autorisatie.
    /// </summary>
    public IReadOnlyList<RechtRij> RechtenOverzicht { get; } = BouwRechtenOverzicht();

    private static IReadOnlyList<RechtRij> BouwRechtenOverzicht()
    {
        var omschrijvingen = new Dictionary<Permissie, string>
        {
            [Permissie.Factureren] = "Factureren / bestelbonnen maken",
            [Permissie.PrijzenWijzigen] = "Prijzen wijzigen (lijsten & afwerkingen)",
            [Permissie.LijstVerwijderen] = "Lijsten archiveren/verwijderen",
            [Permissie.LeverancierVerwijderen] = "Leveranciers verwijderen",
            [Permissie.GebruikersBeheren] = "Gebruikers beheren",
            [Permissie.GdprBeheer] = "GDPR: klantexport & anonimisering",
            [Permissie.ArchiefVerwijderen] = "Archief: offertes permanent verwijderen",
            [Permissie.KlantVerwijderen] = "Klanten archiveren"
        };

        var rijen = new List<RechtRij>
        {
            // Basiswerk valt niet onder een permissie — altijd toegestaan.
            new("Offertes, werkbonnen, planning en klanten (dagelijks werk)", true, true)
        };

        rijen.AddRange(Enum.GetValues<Permissie>().Select(p => new RechtRij(
            omschrijvingen.TryGetValue(p, out var tekst) ? tekst : p.ToString(),
            RolPermissies.Heeft(GebruikersRol.Admin, p),
            RolPermissies.Heeft(GebruikersRol.Medewerker, p))));

        return rijen;
    }

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
