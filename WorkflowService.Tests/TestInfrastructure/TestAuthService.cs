using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace WorkflowService.Tests.TestInfrastructure;

/// <summary>
/// Test-dubbel voor IAuthService. Standaard mag alles (AlleRechten = true) zodat
/// bestaande tests ongewijzigd blijven; zet AlleRechten=false om weigering te testen.
/// </summary>
public sealed class TestAuthService : IAuthService
{
    public bool AlleRechten { get; set; } = true;
    public Gebruiker? CurrentUser { get; set; }
    public event EventHandler? CurrentUserChanged { add { } remove { } }

    public Task<string?> LoginAsync(string g, string w) => Task.FromResult<string?>(null);
    public void Logout() { }
    public bool HeeftPermissie(Permissie permissie) => AlleRechten;
    public void VereisPermissie(Permissie permissie)
    {
        if (!AlleRechten) throw new OnvoldoendeRechtenException(permissie);
    }
    public Task<string?> WijzigWachtwoordAsync(string h, string n) => Task.FromResult<string?>(null);
    public Task SeedDefaultAdminAsync() => Task.CompletedTask;
    public Task<List<Gebruiker>> GetGebruikersAsync() => Task.FromResult(new List<Gebruiker>());
    public Task<string?> MaakGebruikerAsync(string g, string v, GebruikersRol r, string w) => Task.FromResult<string?>(null);
    public Task<string?> ZetActiefAsync(int id, bool actief) => Task.FromResult<string?>(null);
}
