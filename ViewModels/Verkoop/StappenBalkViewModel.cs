using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Verkoop;
using System.Collections.ObjectModel;
using System.Linq;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>US-83 — één stap in de stappenbalk.</summary>
public sealed record StapWeergave(int Nummer, string Titel, string Uitleg, StapToestand Toestand)
{
    public bool IsKlaar => Toestand == StapToestand.Klaar;
    public bool IsHuidig => Toestand == StapToestand.Huidig;
    public bool IsNogTeDoen => Toestand == StapToestand.NogTeDoen;

    /// <summary>Tekst in het bolletje: een vinkje als de stap klaar is, anders het nummer.</summary>
    public string Bolletje => IsKlaar ? "✓" : Nummer.ToString();
}

/// <summary>
/// US-83 — de stappenbalk Offerte → Bestelling → In productie → Klaar om af te halen →
/// Afgehaald &amp; betaald, met één knop voor de volgende stap. Herbruikbaar: de offerte
/// gebruikt hem nu, het verkoopdossier (US-90) later.
/// </summary>
public partial class StappenBalkViewModel : ObservableObject
{
    public ObservableCollection<StapWeergave> Stappen { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeannuleerd), nameof(StatusTekst))]
    private VerkoopStapInfo? info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVolgendeStapZichtbaar))]
    private string? volgendeStapTekst;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVolgendeStapZichtbaar))]
    private IAsyncRelayCommand? volgendeStapCommand;

    public bool IsGeannuleerd => Info?.IsGeannuleerd == true;

    public bool IsVolgendeStapZichtbaar => VolgendeStapCommand is not null && !string.IsNullOrWhiteSpace(VolgendeStapTekst);

    public string StatusTekst => Info switch
    {
        null => string.Empty,
        { IsGeannuleerd: true } => "Geannuleerd",
        { IsAfgerond: true } => "Afgehaald en volledig betaald",
        { IsAfgehaaldNogTeBetalen: true } i => $"Afgehaald — nog € {i.NogTeBetalen:0.00} te betalen",
        var i => $"Stap {(int)i.Huidig} van 5 · {VerkoopStapResolver.Titel(i.Huidig)}"
    };

    public void Toon(VerkoopStapInfo info, string? volgendeStapTekst, IAsyncRelayCommand? volgendeStapCommand)
    {
        Info = info;
        VolgendeStapTekst = info.IsGeannuleerd ? null : volgendeStapTekst;
        VolgendeStapCommand = info.IsGeannuleerd ? null : volgendeStapCommand;

        var nieuw = VerkoopStapResolver.AlleStappen
            .Select(s => new StapWeergave((int)s, VerkoopStapResolver.Titel(s), VerkoopStapResolver.Uitleg(s),
                info.IsGeannuleerd ? StapToestand.NogTeDoen : info.ToestandVan(s)))
            .ToList();

        if (!Stappen.SequenceEqual(nieuw))
        {
            Stappen.Clear();
            foreach (var s in nieuw) Stappen.Add(s);
        }
    }
}
