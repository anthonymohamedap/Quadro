using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Model.DB;
using QuadroApp.Service.Security;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-32 — wachtwoord-hashing.</summary>
public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_RoundTrips()
    {
        var hash = PasswordHasher.Hash("geheim!123");
        Assert.True(PasswordHasher.Verify("geheim!123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = PasswordHasher.Hash("geheim!123");
        Assert.False(PasswordHasher.Verify("fout", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_DifferentHashes()
    {
        Assert.NotEqual(PasswordHasher.Hash("x"), PasswordHasher.Hash("x")); // unieke salt
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.2")]
    [InlineData("notanumber.AAAA.BBBB")]
    public void Verify_MalformedStoredHash_FailsGracefully(string stored)
    {
        Assert.False(PasswordHasher.Verify("x", stored));
    }
}

/// <summary>US-32 — login, rollen en autorisatie.</summary>
/// <remarks>Zelfde collection als AuditTrailTests wegens statische AuditContext.</remarks>
[Collection("StaticAuthState")]
public class AuthServiceTests
{
    private static AuthService CreateSut(SqliteTestDatabase db) =>
        new(db.Factory, NullLogger<AuthService>.Instance);

    private static async Task<Gebruiker> AddUserAsync(SqliteTestDatabase db,
        string naam, string wachtwoord, GebruikersRol rol, bool actief = true)
    {
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var user = new Gebruiker
        {
            GebruikersNaam = naam,
            VolledigeNaam = naam,
            WachtwoordHash = PasswordHasher.Hash(wachtwoord),
            Rol = rol,
            IsActief = actief
        };
        ctx.Gebruikers.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Login_CorrectCredentials_SetsCurrentUser()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "veerle", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);

        var fout = await sut.LoginAsync("veerle", "wachtwoord1");

        Assert.Null(fout);
        Assert.NotNull(sut.CurrentUser);
        Assert.Equal("veerle", sut.CurrentUser!.GebruikersNaam);
        Assert.NotNull(sut.CurrentUser.LaatsteLogin);
    }

    [Fact]
    public async Task Login_WrongPassword_GenericError()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "veerle", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);

        var fout = await sut.LoginAsync("veerle", "verkeerd");

        Assert.NotNull(fout);
        Assert.Null(sut.CurrentUser);
    }

    [Fact]
    public async Task Login_UnknownUser_SameGenericError_AsWrongPassword()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "veerle", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);

        var foutOnbekend = await sut.LoginAsync("bestaatniet", "x");
        var foutWachtwoord = await sut.LoginAsync("veerle", "verkeerd");

        Assert.Equal(foutWachtwoord, foutOnbekend); // geen account-enumeratie
    }

    [Fact]
    public async Task Login_InactiveUser_IsRejected()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "oudcollega", "wachtwoord1", GebruikersRol.Medewerker, actief: false);
        var sut = CreateSut(db);

        var fout = await sut.LoginAsync("oudcollega", "wachtwoord1");

        Assert.NotNull(fout);
        Assert.Null(sut.CurrentUser);
    }

    [Fact]
    public async Task Permissies_AdminMagAlles_MedewerkerBeperkt()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "admin", "wachtwoord1", GebruikersRol.Admin);
        await AddUserAsync(db, "mede", "wachtwoord2", GebruikersRol.Medewerker);
        var sut = CreateSut(db);

        await sut.LoginAsync("admin", "wachtwoord1");
        Assert.True(sut.HeeftPermissie(Permissie.LeverancierVerwijderen));
        Assert.True(sut.HeeftPermissie(Permissie.PrijzenWijzigen));

        await sut.LoginAsync("mede", "wachtwoord2");
        Assert.False(sut.HeeftPermissie(Permissie.LeverancierVerwijderen));
        Assert.False(sut.HeeftPermissie(Permissie.PrijzenWijzigen));
        Assert.True(sut.HeeftPermissie(Permissie.Factureren)); // dagelijkse workflow blijft

        Assert.Throws<OnvoldoendeRechtenException>(() => sut.VereisPermissie(Permissie.PrijzenWijzigen));
    }

    [Fact]
    public async Task Logout_ClearsCurrentUser_AndPermissies()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "admin", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);
        await sut.LoginAsync("admin", "wachtwoord1");

        sut.Logout();

        Assert.Null(sut.CurrentUser);
        Assert.False(sut.HeeftPermissie(Permissie.Factureren));
    }

    [Fact]
    public async Task SeedDefaultAdmin_CreatesAdminOnce()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var sut = CreateSut(db);

        await sut.SeedDefaultAdminAsync();
        await sut.SeedDefaultAdminAsync(); // idempotent

        await using var ctx = await db.Factory.CreateDbContextAsync();
        var admin = Assert.Single(ctx.Gebruikers);
        Assert.Equal(AuthService.DefaultAdminUser, admin.GebruikersNaam);
        Assert.Equal(GebruikersRol.Admin, admin.Rol);
        Assert.True(admin.MoetWachtwoordWijzigen);

        var fout = await sut.LoginAsync(AuthService.DefaultAdminUser, AuthService.DefaultAdminPassword);
        Assert.Null(fout);
    }

    [Fact]
    public async Task MaakGebruiker_ValidatiesEnUniekheid()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "admin", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);
        await sut.LoginAsync("admin", "wachtwoord1");

        Assert.NotNull(await sut.MaakGebruikerAsync("x", "Te Kort", GebruikersRol.Medewerker, "wachtwoord9"));  // naam te kort
        Assert.NotNull(await sut.MaakGebruikerAsync("veerle", "", GebruikersRol.Medewerker, "wachtwoord9"));    // naam verplicht
        Assert.NotNull(await sut.MaakGebruikerAsync("veerle", "Veerle", GebruikersRol.Medewerker, "kort"));     // ww te kort
        Assert.Null(await sut.MaakGebruikerAsync("veerle", "Veerle", GebruikersRol.Medewerker, "wachtwoord9"));
        Assert.NotNull(await sut.MaakGebruikerAsync("veerle", "Dubbel", GebruikersRol.Medewerker, "wachtwoord9")); // uniek

        // Nieuwe gebruiker moet wachtwoord wijzigen bij eerste login
        var gebruikers = await sut.GetGebruikersAsync();
        var veerle = Assert.Single(gebruikers, g => g.GebruikersNaam == "veerle");
        Assert.True(veerle.MoetWachtwoordWijzigen);
    }

    [Fact]
    public async Task MaakGebruiker_ZonderPermissie_Weigert()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "mede", "wachtwoord1", GebruikersRol.Medewerker);
        var sut = CreateSut(db);
        await sut.LoginAsync("mede", "wachtwoord1");

        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(
            () => sut.MaakGebruikerAsync("nieuw", "Nieuw", GebruikersRol.Medewerker, "wachtwoord9"));
        await Assert.ThrowsAsync<OnvoldoendeRechtenException>(() => sut.GetGebruikersAsync());
    }

    [Fact]
    public async Task ZetActief_BeschermtZelfEnLaatsteAdmin()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        var admin = await AddUserAsync(db, "admin", "wachtwoord1", GebruikersRol.Admin);
        var mede = await AddUserAsync(db, "mede", "wachtwoord2", GebruikersRol.Medewerker);
        var sut = CreateSut(db);
        await sut.LoginAsync("admin", "wachtwoord1");

        Assert.NotNull(await sut.ZetActiefAsync(admin.Id, false)); // zelf-deactivatie geblokkeerd (tevens laatste admin)
        Assert.Null(await sut.ZetActiefAsync(mede.Id, false));     // medewerker deactiveren ok
        Assert.Null(await sut.ZetActiefAsync(mede.Id, true));      // en weer activeren

        // mede kan niet meer inloggen wanneer inactief
        await sut.ZetActiefAsync(mede.Id, false);
        var sut2 = CreateSut(db);
        Assert.NotNull(await sut2.LoginAsync("mede", "wachtwoord2"));
    }

    [Fact]
    public async Task WijzigWachtwoord_HappyPath_AndWrongCurrent()
    {
        await using var db = await DbFactoryBuilder.CreateSqliteAsync();
        await AddUserAsync(db, "admin", "wachtwoord1", GebruikersRol.Admin);
        var sut = CreateSut(db);
        await sut.LoginAsync("admin", "wachtwoord1");

        Assert.NotNull(await sut.WijzigWachtwoordAsync("fout", "nieuwwachtwoord"));   // verkeerd huidig
        Assert.NotNull(await sut.WijzigWachtwoordAsync("wachtwoord1", "kort"));       // te kort
        Assert.Null(await sut.WijzigWachtwoordAsync("wachtwoord1", "nieuwwachtwoord"));

        sut.Logout();
        Assert.Null(await sut.LoginAsync("admin", "nieuwwachtwoord"));
    }
}
