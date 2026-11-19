using Avalonia.Controls;

namespace QuadroApp.Views.Offerte;

/// <summary>US-91 — Inlijstingen (lijst + Toevoegen/Dupliceren/Verwijderen).</summary>
public partial class OfferteRegelsKaart : UserControl
{
    public OfferteRegelsKaart()
    {
        InitializeComponent();
    }

    /// <summary>Kop van de kaart (oud scherm: standaardtekst; dossier: "Inlijsting details" / "Inlijstingen").</summary>
    public string Titel
    {
        get => TitelTekst.Text ?? "";
        set => TitelTekst.Text = value;
    }
}
