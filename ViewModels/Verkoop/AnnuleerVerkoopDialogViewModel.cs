using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using System;
using System.Collections.Generic;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>
/// US-90 — menu ⋯ "Verkoop annuleren…": verplichte reden en, als er al iets ontvangen werd,
/// de keuze voorschot terugbetalen of houden (beslist 02/11). De gevolgen zijn die van vandaag.
/// </summary>
public partial class AnnuleerVerkoopDialogViewModel : ObservableObject
{
    public AnnuleerVerkoopDialogViewModel(int verkoopNummer, decimal ontvangen, string? bestelbonNummer)
    {
        Titel = $"Verkoop {verkoopNummer} annuleren?";
        Ontvangen = ontvangen;
        BestelbonTekst = bestelbonNummer is null
            ? "geen bestelbon met nummer"
            : $"{bestelbonNummer} → Geannuleerd; het nummer blijft";
    }

    public string Titel { get; }
    public decimal Ontvangen { get; }
    public bool HeeftOntvangen => Ontvangen > 0m;
    public string OntvangenTekst => $"€ {Ontvangen:#,##0.00} ontvangen — terugbetalen of houden";
    public string BestelbonTekst { get; }

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BevestigCommand))]
    [NotifyPropertyChangedFor(nameof(KanBevestigen))]
    private string? reden;

    [ObservableProperty] private bool terugbetalen = true;

    public IReadOnlyList<Betaalwijze> Betaalwijzen { get; } =
        new[] { Betaalwijze.Bancontact, Betaalwijze.Cash, Betaalwijze.CreditCard, Betaalwijze.Overschrijving };

    [ObservableProperty] private Betaalwijze betaalwijze = Betaalwijze.Bancontact;

    public bool KanBevestigen => !string.IsNullOrWhiteSpace(Reden);

    [RelayCommand(CanExecute = nameof(KanBevestigen))]
    private void Bevestig() => RequestClose?.Invoke(true);

    [RelayCommand]
    private void Sluit() => RequestClose?.Invoke(false);

    public AnnuleerOpdracht ToOpdracht() => new(
        Reden!.Trim(),
        HeeftOntvangen && Terugbetalen ? VoorschotAfhandeling.Terugbetalen : VoorschotAfhandeling.Houden,
        Betaalwijze);
}
