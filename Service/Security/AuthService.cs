using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.Service.Security;

/// <summary>
/// US-32 — authenticatie & autorisatie. Singleton: CurrentUser geldt app-breed.
/// Wachtwoorden: PBKDF2 via <see cref="PasswordHasher"/>. Login-fouten zijn
/// bewust generiek ("gebruikersnaam of wachtwoord onjuist") tegen enumeratie.
/// </summary>
public sealed class AuthService : IAuthService
{
    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "quadro";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<AuthService> _logger;

    public Gebruiker? CurrentUser { get; private set; }
    public event EventHandler? CurrentUserChanged;

    public AuthService(IDbContextFactory<AppDbContext> factory, ILogger<AuthService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(string gebruikersNaam, string wachtwoord)
    {
        const string genericError = "Gebruikersnaam of wachtwoord onjuist.";

        if (string.IsNullOrWhiteSpace(gebruikersNaam) || string.IsNullOrEmpty(wachtwoord))
            return genericError;

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Gebruikers
            .FirstOrDefaultAsync(g => g.GebruikersNaam == gebruikersNaam.Trim());

        if (user is null || !PasswordHasher.Verify(wachtwoord, user.WachtwoordHash))
        {
            _logger.LogWarning("[Auth] Mislukte login voor '{User}'.", gebruikersNaam.Trim());
            return genericError;
        }

        if (!user.IsActief)
        {
            _logger.LogWarning("[Auth] Login geweigerd voor inactieve gebruiker '{User}'.", user.GebruikersNaam);
            return "Dit account is gedeactiveerd.";
        }

        user.LaatsteLogin = DateTime.Now;
        await db.SaveChangesAsync();

        CurrentUser = user;
        AuditContext.CurrentUserName = user.GebruikersNaam; // US-36
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("[Auth] '{User}' ingelogd (rol: {Rol}).", user.GebruikersNaam, user.Rol);
        return null;
    }

    public void Logout()
    {
        if (CurrentUser is null) return;
        _logger.LogInformation("[Auth] '{User}' uitgelogd/vergrendeld.", CurrentUser.GebruikersNaam);
        CurrentUser = null;
        AuditContext.Reset(); // US-36
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HeeftPermissie(Permissie permissie) =>
        CurrentUser is { IsActief: true } user && RolPermissies.Heeft(user.Rol, permissie);

    public void VereisPermissie(Permissie permissie)
    {
        if (!HeeftPermissie(permissie))
            throw new OnvoldoendeRechtenException(permissie);
    }

    public async Task<string?> WijzigWachtwoordAsync(string huidigWachtwoord, string nieuwWachtwoord)
    {
        if (CurrentUser is null) return "Niet ingelogd.";
        if (string.IsNullOrWhiteSpace(nieuwWachtwoord) || nieuwWachtwoord.Length < 8)
            return "Nieuw wachtwoord moet minstens 8 tekens lang zijn.";

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Gebruikers.FindAsync(CurrentUser.Id);
        if (user is null) return "Gebruiker niet gevonden.";

        if (!PasswordHasher.Verify(huidigWachtwoord, user.WachtwoordHash))
            return "Huidig wachtwoord is onjuist.";

        user.WachtwoordHash = PasswordHasher.Hash(nieuwWachtwoord);
        user.MoetWachtwoordWijzigen = false;
        await db.SaveChangesAsync();

        CurrentUser = user;
        _logger.LogInformation("[Auth] Wachtwoord gewijzigd voor '{User}'.", user.GebruikersNaam);
        return null;
    }

    public async Task<System.Collections.Generic.List<Gebruiker>> GetGebruikersAsync()
    {
        VereisPermissie(Permissie.GebruikersBeheren);
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Gebruikers.OrderBy(g => g.GebruikersNaam).ToListAsync();
    }

    public async Task<string?> MaakGebruikerAsync(string gebruikersNaam, string volledigeNaam, GebruikersRol rol, string initieelWachtwoord)
    {
        VereisPermissie(Permissie.GebruikersBeheren);

        gebruikersNaam = gebruikersNaam?.Trim() ?? "";
        if (gebruikersNaam.Length < 2) return "Gebruikersnaam moet minstens 2 tekens lang zijn.";
        if (string.IsNullOrWhiteSpace(volledigeNaam)) return "Volledige naam is verplicht.";
        if (string.IsNullOrWhiteSpace(initieelWachtwoord) || initieelWachtwoord.Length < 8)
            return "Initieel wachtwoord moet minstens 8 tekens lang zijn.";

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Gebruikers.AnyAsync(g => g.GebruikersNaam == gebruikersNaam))
            return "Deze gebruikersnaam bestaat al.";

        db.Gebruikers.Add(new Gebruiker
        {
            GebruikersNaam = gebruikersNaam,
            VolledigeNaam = volledigeNaam.Trim(),
            WachtwoordHash = PasswordHasher.Hash(initieelWachtwoord),
            Rol = rol,
            IsActief = true,
            MoetWachtwoordWijzigen = true
        });
        await db.SaveChangesAsync();
        _logger.LogInformation("[Auth] Gebruiker '{User}' aangemaakt (rol: {Rol}) door '{Door}'.",
            gebruikersNaam, rol, CurrentUser?.GebruikersNaam);
        return null;
    }

    public async Task<string?> ZetActiefAsync(int gebruikerId, bool actief)
    {
        VereisPermissie(Permissie.GebruikersBeheren);

        if (!actief && CurrentUser?.Id == gebruikerId)
            return "Je kan je eigen account niet deactiveren.";

        await using var db = await _factory.CreateDbContextAsync();
        var user = await db.Gebruikers.FindAsync(gebruikerId);
        if (user is null) return "Gebruiker niet gevonden.";

        if (!actief && user.Rol == GebruikersRol.Admin)
        {
            var actieveAdmins = await db.Gebruikers.CountAsync(g => g.Rol == GebruikersRol.Admin && g.IsActief);
            if (actieveAdmins <= 1) return "De laatste actieve admin kan niet gedeactiveerd worden.";
        }

        user.IsActief = actief;
        await db.SaveChangesAsync();
        _logger.LogInformation("[Auth] Gebruiker '{User}' {Actie} door '{Door}'.",
            user.GebruikersNaam, actief ? "geactiveerd" : "gedeactiveerd", CurrentUser?.GebruikersNaam);
        return null;
    }

    public async Task SeedDefaultAdminAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Gebruikers.AnyAsync())
            return;

        db.Gebruikers.Add(new Gebruiker
        {
            GebruikersNaam = DefaultAdminUser,
            VolledigeNaam = "Beheerder",
            WachtwoordHash = PasswordHasher.Hash(DefaultAdminPassword),
            Rol = GebruikersRol.Admin,
            IsActief = true,
            MoetWachtwoordWijzigen = true
        });
        await db.SaveChangesAsync();
        _logger.LogWarning("[Auth] Standaard admin aangemaakt ('{User}'). Wijzig het wachtwoord direct na eerste login!",
            DefaultAdminUser);
    }
}
