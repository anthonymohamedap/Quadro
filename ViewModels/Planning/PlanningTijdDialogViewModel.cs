using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace QuadroApp.ViewModels;

public partial class PlanningTijdDialogViewModel : ObservableObject
{
    [ObservableProperty] private string contextLabel = "";
    [ObservableProperty] private DateTimeOffset? geplandeDatum;

    /// <summary>Totaal geschatte werkminuten (read-only, berekend door caller).</summary>
    [ObservableProperty] private int totaalMinuten;

    public Action<bool>? RequestClose { get; set; }
    public bool Confirmed { get; private set; }

    public string DuurLabel =>
        TotaalMinuten > 0
            ? $"{TotaalMinuten / 60}u {TotaalMinuten % 60:D2}m ({TotaalMinuten} min)"
            : "—";

    /// <summary>Hoeveel dagen dit werk nodig heeft (bij 8u/dag).</summary>
    public int AantalDagen => TotaalMinuten <= 0 ? 0 : (int)Math.Ceiling((double)TotaalMinuten / (8 * 60));

    public string SpreadLabel =>
        AantalDagen <= 1
            ? "Past op 1 dag"
            : $"Wordt verdeeld over {AantalDagen} dagen";

    public bool IsGeldig => TotaalMinuten > 0 && GeplandeDatum.HasValue;

    partial void OnTotaalMinutenChanged(int value) => Refresh();
    partial void OnGeplandeDatumChanged(DateTimeOffset? value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(DuurLabel));
        OnPropertyChanged(nameof(AantalDagen));
        OnPropertyChanged(nameof(SpreadLabel));
        OnPropertyChanged(nameof(IsGeldig));
        BevestigCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Startdatum (datum + 09:00 dummy).</summary>
    public DateTime GetStartDatum() =>
        (GeplandeDatum?.DateTime.Date ?? DateTime.Today).AddHours(9);

    [RelayCommand(CanExecute = nameof(IsGeldig))]
    private void Bevestig()
    {
        Confirmed = true;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Annuleer() => RequestClose?.Invoke(false);
}