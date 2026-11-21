using Npgsql;
using QuadroApp.Service.Security;
using QuadroApp.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>Eerste start op een nieuwe laptop: enkel het databasewachtwoord vragen, testen en pas dan bewaren.</summary>
public sealed class KoppelViewModelTests : IDisposable
{
    private const string Cs = "Host=db.voorbeeld.test;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "QuadroKoppel_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* opruimen mag falen */ }
    }

    [Fact]
    public void Toont_de_server_maar_niet_het_wachtwoord()
    {
        var vm = new KoppelViewModel(Cs, _dir, (_, _) => Task.CompletedTask);
        Assert.Equal("db.voorbeeld.test", vm.Server);
        Assert.False(vm.KoppelenCommand.CanExecute(null));   // leeg wachtwoord: knop uit
    }

    [Fact]
    public async Task Juist_wachtwoord_wordt_getest_bewaard_en_start_de_app()
    {
        string? geteste = null;
        var gekoppeld = false;
        var vm = new KoppelViewModel(Cs, _dir, (cs, _) => { geteste = cs; return Task.CompletedTask; })
        {
            Gekoppeld = () => gekoppeld = true
        };
        vm.Wachtwoord = "geheim123";

        await vm.KoppelenCommand.ExecuteAsync(null);

        Assert.Contains("Password=geheim123", geteste);
        Assert.True(gekoppeld);
        Assert.Null(vm.Foutmelding);
        Assert.Equal("", vm.Wachtwoord);   // niet in het geheugen van het scherm laten staan
        Assert.True(File.Exists(Path.Combine(_dir, SecretStore.SecretFileName)));
        if (Environment.GetEnvironmentVariable(SecretStore.PasswordEnvVar) is null)
            Assert.Equal("geheim123", SecretStore.ResolvePassword(_dir));
    }

    [Fact]
    public async Task Fout_wachtwoord_geeft_duidelijke_tekst_en_bewaart_niets()
    {
        var gekoppeld = false;
        var vm = new KoppelViewModel(Cs, _dir,
            (_, _) => throw new PostgresException("password authentication failed", "FATAL", "FATAL", "28P01"))
        {
            Gekoppeld = () => gekoppeld = true
        };
        vm.Wachtwoord = "fout";

        await vm.KoppelenCommand.ExecuteAsync(null);

        Assert.Equal("Het wachtwoord klopt niet. Probeer het opnieuw.", vm.Foutmelding);
        Assert.False(gekoppeld);
        Assert.False(vm.IsBezig);
        Assert.False(File.Exists(Path.Combine(_dir, SecretStore.SecretFileName)));
    }

    [Fact]
    public async Task Geen_antwoord_van_de_server_zegt_internet_controleren()
    {
        var vm = new KoppelViewModel(Cs, _dir, (_, _) => throw new OperationCanceledException()) { Wachtwoord = "x" };
        await vm.KoppelenCommand.ExecuteAsync(null);
        Assert.Contains("internet", vm.Foutmelding);
    }
}
