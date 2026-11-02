using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using System;

namespace QuadroApp.ViewModels;

public partial class BestelbonGegevensDialogViewModel : ObservableObject
{
    private readonly Bestelbon _bestelbon;

    [ObservableProperty] private DateTimeOffset? bestelbonDatum;
    [ObservableProperty] private DateTimeOffset? vervalDatum;
    [ObservableProperty] private string? opmerking;
    [ObservableProperty] private string? aangenomenDoorInitialen;
    [ObservableProperty] private string? validatieFout;

    public Action<bool>? RequestClose { get; set; }

    public string BestelbonNummer => _bestelbon.NummerLabel;   // US-99: "2026-19" of "voorlopig (offerte 412)"
    public string KlantNaam => _bestelbon.KlantNaam;
    public bool HasValidatieFout => !string.IsNullOrWhiteSpace(ValidatieFout);

    /// <summary>US-81 — niet-blokkerende waarschuwing als er afgehaald wordt vóór de besteldatum.</summary>
    public string? AfhaalWaarschuwing => BestelbonDatum is null
        ? null
        : AfhaalDatumControle.Waarschuwing(BestelbonDatum.Value.Date, AfhaalDatumControle.VroegsteAfhaalDatum(_bestelbon));
    public bool HasAfhaalWaarschuwing => !string.IsNullOrWhiteSpace(AfhaalWaarschuwing);

    public BestelbonGegevensDialogViewModel(Bestelbon bestelbon)
    {
        _bestelbon = bestelbon;
        BestelbonDatum = new DateTimeOffset(bestelbon.BestelbonDatum == default ? DateTime.Today : bestelbon.BestelbonDatum);
        VervalDatum = new DateTimeOffset(bestelbon.VervalDatum == default ? DateTime.Today.AddDays(30) : bestelbon.VervalDatum);
        Opmerking = bestelbon.Opmerking;
        AangenomenDoorInitialen = bestelbon.AangenomenDoorInitialen;
    }

    partial void OnValidatieFoutChanged(string? value) => OnPropertyChanged(nameof(HasValidatieFout));

    partial void OnBestelbonDatumChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(AfhaalWaarschuwing));
        OnPropertyChanged(nameof(HasAfhaalWaarschuwing));
    }

    [RelayCommand]
    private void Bevestig()
    {
        if (BestelbonDatum is null)
        {
            ValidatieFout = "Kies een besteldatum.";
            return;
        }

        if (VervalDatum is null)
        {
            ValidatieFout = "Kies een vervaldatum.";
            return;
        }

        if (VervalDatum.Value.Date < BestelbonDatum.Value.Date)
        {
            ValidatieFout = "De vervaldatum moet op of na de besteldatum liggen.";
            return;
        }

        ValidatieFout = null;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Annuleer() => RequestClose?.Invoke(false);

    public Bestelbon ToBestelbon()
    {
        _bestelbon.BestelbonDatum = (BestelbonDatum ?? DateTimeOffset.Now).Date;
        _bestelbon.VervalDatum = (VervalDatum ?? DateTimeOffset.Now).Date;
        _bestelbon.Opmerking = string.IsNullOrWhiteSpace(Opmerking) ? null : Opmerking.Trim();
        _bestelbon.AangenomenDoorInitialen = string.IsNullOrWhiteSpace(AangenomenDoorInitialen)
            ? null
            : AangenomenDoorInitialen.Trim();
        return _bestelbon;
    }
}
