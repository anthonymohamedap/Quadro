using System;
using System.Threading.Tasks;
using QuadroApp.Model.DB;
using QuadroApp.Service.Security;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-32 — authenticatie, huidige gebruiker en autorisatie.</summary>
public interface IAuthService
{
    /// <summary>De ingelogde gebruiker; null wanneer (nog) niet ingelogd of vergrendeld.</summary>
    Gebruiker? CurrentUser { get; }

    /// <summary>Raised na succesvol in- of uitloggen (ook bij vergrendeling).</summary>
    event EventHandler? CurrentUserChanged;

    /// <summary>Valideert credentials. Bij succes wordt CurrentUser gezet. Null = ok, anders foutmelding.</summary>
    Task<string?> LoginAsync(string gebruikersNaam, string wachtwoord);

    /// <summary>Logt uit / vergrendelt: CurrentUser wordt null.</summary>
    void Logout();

    bool HeeftPermissie(Permissie permissie);

    /// <summary>Gooit <see cref="OnvoldoendeRechtenException"/> wanneer de permissie ontbreekt.</summary>
    void VereisPermissie(Permissie permissie);

    /// <summary>Wijzigt het wachtwoord van de ingelogde gebruiker. Null = ok, anders foutmelding.</summary>
    Task<string?> WijzigWachtwoordAsync(string huidigWachtwoord, string nieuwWachtwoord);

    /// <summary>Maakt de standaard admin aan wanneer er nog geen gebruikers bestaan (idempotent).</summary>
    Task SeedDefaultAdminAsync();
}
