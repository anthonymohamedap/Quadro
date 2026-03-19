using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace QuadroApp.ViewModels;

public partial class PlanningTijdDialogViewModel : ObservableObject
{
    [ObservableProperty] private string contextLabel = "";
    [ObservableProperty] private DateTimeOffset? geplandeDatum;

    // decimal voor Avalonia NumericUpDown compatibiliteit
    [ObservableProperty] private decimal vanUur = 8;
    [ObservableProperty] private decimal vanMinuut = 0;
    [ObservableProperty] private decimal totUur = 10;
    [ObservableProperty] private decimal totMinuut = 0;

    public Action<bool>? RequestClose { get; set; }
    public bool Confirmed { get; private set; }

    public int DuurMinuten
    {
        get
        {
            int van = (int)VanUur * 60 + (int)VanMinuut;
            int tot = (int)TotUur * 60 + (int)TotMinuut;
            return Math.Max(0, tot - van);
        }
    }

    public string DuurLabel =>
        DuurMinuten > 0
            ? $"{DuurMinuten / 60}u {DuurMinuten % 60:D2}m ({DuurMinuten} min)"
            : "Ongeldige tijdspanne";

    public bool IsGeldig => DuurMinuten > 0 && GeplandeDatum.HasValue;

    partial void OnVanUurChanged(decimal value) => Refresh();
    partial void OnVanMinuutChanged(decimal value) => Refresh();
    partial void OnTotUurChanged(decimal value) => Refresh();
    partial void OnTotMinuutChanged(decimal value) => Refresh();
    partial void OnGeplandeDatumChanged(DateTimeOffset? value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(DuurMinuten));
        OnPropertyChanged(nameof(DuurLabel));
        OnPropertyChanged(nameof(IsGeldig));
        BevestigCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Geeft de volledige Van-DateTime terug (datum + tijdstip).</summary>
    public DateTime GetVanDateTime() =>
        (GeplandeDatum?.DateTime.Date ?? DateTime.Today)
            .AddHours((double)VanUur)
            .AddMinutes((double)VanMinuut);

    [RelayCommand(CanExecute = nameof(IsGeldig))]
    private void Bevestig()
    {
        Confirmed = true;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Annuleer() => RequestClose?.Invoke(false);
}