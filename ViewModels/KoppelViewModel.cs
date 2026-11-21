using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using QuadroApp.Service.Security;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>
/// Eerste start op een nieuwe laptop: de installer kent het serveradres (appsettings.json naast de app,
/// met Password=__SECRET__), maar het databasewachtwoord nog niet. Dit scherm vraagt enkel dat wachtwoord,
/// test de verbinding en bewaart het pas daarna, versleuteld, via <see cref="SecretStore"/>.
/// </summary>
public partial class KoppelViewModel : ObservableObject
{
    private readonly string _connectionStringMetPlaceholder;
    private readonly string _dataDir;
    private readonly Func<string, CancellationToken, Task> _testVerbinding;

    public KoppelViewModel(string connectionStringMetPlaceholder, string dataDir,
        Func<string, CancellationToken, Task>? testVerbinding = null)
    {
        _connectionStringMetPlaceholder = connectionStringMetPlaceholder;
        _dataDir = dataDir;
        _testVerbinding = testVerbinding ?? OpenPostgresAsync;
        Server = new NpgsqlConnectionStringBuilder(connectionStringMetPlaceholder.Replace(SecretStore.Placeholder, "x")).Host ?? "";
    }

    /// <summary>Getoond ter info ("verbinden met …"); geen geheim.</summary>
    public string Server { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KoppelenCommand))]
    private string wachtwoord = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KoppelenCommand))]
    private bool isBezig;

    [ObservableProperty] private string? foutmelding;

    /// <summary>Wordt aangeroepen na een geslaagde koppeling; de app start dan gewoon verder.</summary>
    public Action? Gekoppeld { get; set; }

    private bool KanKoppelen() => !IsBezig && !string.IsNullOrWhiteSpace(Wachtwoord);

    [RelayCommand(CanExecute = nameof(KanKoppelen))]
    private async Task KoppelenAsync()
    {
        Foutmelding = null;
        IsBezig = true;
        try
        {
            var cs = _connectionStringMetPlaceholder.Replace(
                $"Password={SecretStore.Placeholder}", $"Password={Wachtwoord}", StringComparison.OrdinalIgnoreCase);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _testVerbinding(cs, cts.Token);

            // Pas bewaren als de verbinding echt werkt: anders zou de app met een fout wachtwoord blijven starten.
            SecretStore.StorePassword(Wachtwoord, _dataDir);
            Wachtwoord = "";
            Gekoppeld?.Invoke();
        }
        catch (Exception ex)
        {
            Foutmelding = Vertaal(ex);
        }
        finally
        {
            IsBezig = false;
        }
    }

    /// <summary>Duidelijke tekst voor Veerle i.p.v. de technische fout.</summary>
    public static string Vertaal(Exception ex) => ex switch
    {
        PostgresException { SqlState: "28P01" or "28000" } => "Het wachtwoord klopt niet. Probeer het opnieuw.",
        OperationCanceledException or TimeoutException =>
            "De server antwoordt niet. Controleer of deze laptop internet heeft en probeer opnieuw.",
        NpgsqlException { InnerException: SocketException or TimeoutException } or SocketException =>
            "Geen verbinding met de server. Controleer of deze laptop internet heeft en probeer opnieuw.",
        _ => $"Koppelen lukt niet: {ex.GetBaseException().Message}"
    };

    private static async Task OpenPostgresAsync(string connectionString, CancellationToken ct)
    {
        await using var con = new NpgsqlConnection(connectionString);
        await con.OpenAsync(ct);
    }
}
