using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Foutlog;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Security;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>Eén foutmelding voor weergave (tijdstip in lokale tijd).</summary>
public partial class FoutmeldingRij : ObservableObject
{
    public FoutmeldingRij(FoutLog log)
    {
        Log = log;
        isBekeken = log.IsBekeken;
        var melding = (log.Melding ?? "").ReplaceLineEndings(" ");
        KorteMelding = melding.Length > 120 ? melding[..120] + "…" : melding;
    }

    public FoutLog Log { get; }
    public int Id => Log.Id;
    public string Tijdstip => DateTime.SpecifyKind(Log.Tijdstip, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Computer => Log.Computernaam ?? "";
    public string Scherm => Log.Scherm ?? "";
    public string Bron => Log.Bron;
    public string KorteMelding { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNieuw))] private bool isBekeken;
    public bool IsNieuw => !IsBekeken;
}

/// <summary>Beheerdersscherm: foutmeldingen (crashes) van alle computers.</summary>
public partial class FoutmeldingenViewModel : ObservableObject
{
    private readonly IFoutLogService _service;

    public ObservableCollection<FoutmeldingRij> Rijen { get; } = new();

    [ObservableProperty] private bool enkelNietBekeken;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DetailTekst)), NotifyPropertyChangedFor(nameof(HeeftSelectie))]
    [NotifyCanExecuteChangedFor(nameof(MarkeerBekekenCommand), nameof(KopieerCommand))]
    private FoutmeldingRij? geselecteerd;
    [ObservableProperty] private string? foutmelding;
    [ObservableProperty] private bool isBezig;

    /// <summary>De view zet dit naar TopLevel.Clipboard.SetTextAsync.</summary>
    public Func<string, Task>? Klembord { get; set; }

    private int _laadTeller;

    public FoutmeldingenViewModel(IFoutLogService service) => _service = service;

    public static bool IsZichtbaarVoor(IAuthService auth) => auth.HeeftPermissie(Permissie.AuditInzien);

    public bool HeeftSelectie => Geselecteerd is not null;
    public bool IsLeeg => Rijen.Count == 0;
    public string LeegTekst => EnkelNietBekeken ? "Geen niet-bekeken foutmeldingen." : "Geen foutmeldingen.";

    public string DetailTekst => Geselecteerd is null ? "Kies links een foutmelding." : BouwDetail(Geselecteerd.Log);

    partial void OnEnkelNietBekekenChanged(bool value)
    {
        OnPropertyChanged(nameof(LeegTekst));
        _ = LadenAsync();
    }

    [RelayCommand]
    private async Task LadenAsync()
    {
        // Een tragere, oudere lading mag een nieuwere (na snel wisselen van filter) nooit overschrijven.
        var beurt = Interlocked.Increment(ref _laadTeller);
        IsBezig = true;
        Foutmelding = null;
        try
        {
            var vorige = Geselecteerd?.Id;
            var lijst = await _service.LijstAsync(EnkelNietBekeken);
            if (beurt != Volatile.Read(ref _laadTeller)) return;
            Rijen.Clear();
            foreach (var l in lijst) Rijen.Add(new FoutmeldingRij(l));
            Geselecteerd = vorige is null ? null : Rijen.FirstOrDefault(r => r.Id == vorige);
        }
        catch (Exception ex)
        {
            if (beurt == Volatile.Read(ref _laadTeller))
                Foutmelding = $"Foutmeldingen laden mislukt: {ex.Message}";
        }
        finally
        {
            if (beurt == Volatile.Read(ref _laadTeller))
            {
                IsBezig = false;
                OnPropertyChanged(nameof(IsLeeg));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HeeftSelectie))]
    private async Task KopieerAsync()
    {
        try
        {
            if (Klembord is not null) await Klembord(DetailTekst);
        }
        catch (Exception ex)
        {
            Foutmelding = $"Kopiëren mislukt: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(KanMarkeren))]
    private async Task MarkeerBekekenAsync()
    {
        var rij = Geselecteerd;
        if (rij is null) return;
        try
        {
            Foutmelding = null;
            await _service.MarkeerBekekenAsync(rij.Id);
            rij.IsBekeken = true;
            if (EnkelNietBekeken)
            {
                Rijen.Remove(rij);
                Geselecteerd = null;
                OnPropertyChanged(nameof(IsLeeg));
            }
            MarkeerBekekenCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Markeren mislukt: {ex.Message}";
        }
    }

    private bool KanMarkeren() => Geselecteerd is { IsBekeken: false };

    private static string BouwDetail(FoutLog l)
    {
        var tijd = DateTime.SpecifyKind(l.Tijdstip, DateTimeKind.Utc).ToLocalTime();
        var sb = new StringBuilder();
        sb.AppendLine($"Tijdstip:    {tijd:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Bron:        {l.Bron}");
        sb.AppendLine($"Computer:    {l.Computernaam}");
        sb.AppendLine($"Gebruiker:   {l.Gebruiker}");
        sb.AppendLine($"App-versie:  {l.AppVersie}");
        sb.AppendLine($"Scherm:      {l.Scherm}");
        sb.AppendLine();
        sb.AppendLine("Laatste klikken");
        sb.AppendLine(string.IsNullOrWhiteSpace(l.LaatsteKlikken) ? "(geen)" : l.LaatsteKlikken);
        sb.AppendLine();
        sb.AppendLine("Stacktrace");
        sb.Append(l.Stacktrace);
        return sb.ToString();
    }
}
