using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels.Gebruikers;

/// <summary>
/// Venster "Startklaar maken" (start op 1 januari 2027): toont wat er weggaat en wat blijft, en voert het pas
/// uit nadat de beheerder het woord STARTKLAAR intypt. Zie <see cref="StartklaarService"/>.
/// </summary>
public partial class StartklaarViewModel : ObservableObject
{
    public const string Bevestigwoord = "STARTKLAAR";

    private readonly IStartklaarService _service;
    private readonly IToastService _toast;

    public ObservableCollection<StartklaarRegel> Weg { get; } = new();
    public ObservableCollection<StartklaarRegel> Blijft { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanUitvoeren))]
    [NotifyCanExecuteChangedFor(nameof(UitvoerenCommand))]
    private string bevestiging = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanUitvoeren))]
    [NotifyCanExecuteChangedFor(nameof(UitvoerenCommand))]
    private bool isBezig;

    [ObservableProperty] private int totaalWeg;
    [ObservableProperty] private string? klaarTekst;
    [ObservableProperty] private string? foutmelding;

    public bool KanUitvoeren =>
        !IsBezig && string.Equals(Bevestiging?.Trim(), Bevestigwoord, StringComparison.Ordinal);

    public StartklaarViewModel(IStartklaarService service, IToastService toast)
    {
        _service = service;
        _toast = toast;
    }

    [RelayCommand]
    public async Task LadenAsync()
    {
        try
        {
            Foutmelding = null;
            var overzicht = await _service.TelAsync();
            Weg.Clear();
            foreach (var r in overzicht.Weg) Weg.Add(r);
            Blijft.Clear();
            foreach (var r in overzicht.Blijft) Blijft.Add(r);
            TotaalWeg = overzicht.TotaalWeg;
        }
        catch (Exception ex)
        {
            Foutmelding = $"Tellen mislukt: {ex.GetBaseException().Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(KanUitvoeren))]
    private async Task UitvoerenAsync()
    {
        try
        {
            IsBezig = true;
            Foutmelding = null;
            var voor = await _service.VoerUitAsync();
            KlaarTekst = $"Klaar: {voor.TotaalWeg} testgegevens verwijderd. Het eerste offertenummer wordt 1.";
            _toast.Success("Startklaar gemaakt.");
            Bevestiging = string.Empty;
            await LadenAsync();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Mislukt, er is niets gewijzigd: {ex.GetBaseException().Message}";
        }
        finally
        {
            IsBezig = false;
        }
    }
}
