using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using System;

namespace QuadroApp.ViewModels;

public partial class FactuurInfoDialogViewModel : ObservableObject
{
    private readonly Factuur _factuur;

    [ObservableProperty] private DateTimeOffset? factuurDatum;
    [ObservableProperty] private DateTimeOffset? vervalDatum;
    [ObservableProperty] private string? opmerking;
    [ObservableProperty] private string? aangenomenDoorInitialen;
    [ObservableProperty] private string? validatieFout;

    public Action<bool>? RequestClose { get; set; }

    public string FactuurNummer => _factuur.FactuurNummer;
    public string KlantNaam => _factuur.KlantNaam;
    public bool HasValidatieFout => !string.IsNullOrWhiteSpace(ValidatieFout);

    public FactuurInfoDialogViewModel(Factuur factuur)
    {
        _factuur = factuur;
        FactuurDatum = new DateTimeOffset(factuur.FactuurDatum == default ? DateTime.Today : factuur.FactuurDatum);
        VervalDatum = new DateTimeOffset(factuur.VervalDatum == default ? DateTime.Today.AddDays(30) : factuur.VervalDatum);
        Opmerking = factuur.Opmerking;
        AangenomenDoorInitialen = factuur.AangenomenDoorInitialen;
    }

    partial void OnValidatieFoutChanged(string? value) => OnPropertyChanged(nameof(HasValidatieFout));

    [RelayCommand]
    private void Bevestig()
    {
        if (FactuurDatum is null)
        {
            ValidatieFout = "Kies een factuurdatum.";
            return;
        }

        if (VervalDatum is null)
        {
            ValidatieFout = "Kies een vervaldatum.";
            return;
        }

        if (VervalDatum.Value.Date < FactuurDatum.Value.Date)
        {
            ValidatieFout = "De vervaldatum moet op of na de factuurdatum liggen.";
            return;
        }

        ValidatieFout = null;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Annuleer() => RequestClose?.Invoke(false);

    public Factuur ToFactuur()
    {
        _factuur.FactuurDatum = (FactuurDatum ?? DateTimeOffset.Now).Date;
        _factuur.VervalDatum = (VervalDatum ?? DateTimeOffset.Now).Date;
        _factuur.Opmerking = string.IsNullOrWhiteSpace(Opmerking) ? null : Opmerking.Trim();
        _factuur.AangenomenDoorInitialen = string.IsNullOrWhiteSpace(AangenomenDoorInitialen)
            ? null
            : AangenomenDoorInitialen.Trim();
        return _factuur;
    }
}
