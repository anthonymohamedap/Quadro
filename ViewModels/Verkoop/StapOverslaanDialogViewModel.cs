using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Verkoop;
using System;
using System.Collections.Generic;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>
/// US-93 — "⤼ Stap overslaan" (stap 2 of 3): rechtstreeks naar Klaar om af te halen, met verplichte
/// reden (mock: Standaardkader uit voorraad / Klant neemt meteen mee / Andere…). Gevolgen zoals
/// "Klaar om af te halen" (voorraad verbruikt, bestelbon).
/// </summary>
public partial class StapOverslaanDialogViewModel : ObservableObject
{
    public const string Andere = "Andere…";

    public StapOverslaanDialogViewModel(VerkoopStap huidig)
    {
        Wat = huidig == VerkoopStap.Werkbon
            ? "Werkbon en In productie → meteen Klaar om af te halen"
            : "In productie → meteen Klaar om af te halen";
    }

    public string Wat { get; }
    public string Gevolgen => "Zelfde gevolgen als \"Klaar om af te halen\": gereserveerde stukken worden verbruikt en de bestelbon wordt aangemaakt of bijgewerkt. De overgeslagen stappen staan doorgestreept.";

    public IReadOnlyList<string> Redenen { get; } = new[] { "Standaardkader uit voorraad", "Klant neemt meteen mee", Andere };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAndere), nameof(KanBevestigen))]
    [NotifyCanExecuteChangedFor(nameof(BevestigCommand))]
    private string gekozenReden = "Standaardkader uit voorraad";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanBevestigen))]
    [NotifyCanExecuteChangedFor(nameof(BevestigCommand))]
    private string? andereReden;

    public bool IsAndere => GekozenReden == Andere;
    public bool KanBevestigen => !IsAndere || !string.IsNullOrWhiteSpace(AndereReden);
    public string Reden => IsAndere ? (AndereReden ?? "").Trim() : GekozenReden;

    public Action<bool>? RequestClose { get; set; }

    [RelayCommand(CanExecute = nameof(KanBevestigen))]
    private void Bevestig() => RequestClose?.Invoke(true);

    [RelayCommand]
    private void Sluit() => RequestClose?.Invoke(false);
}
