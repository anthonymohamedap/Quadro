using Avalonia.Controls;

namespace QuadroApp.Views.Offerte;

/// <summary>US-91 — Regel / inlijsting details, afwerkingen en de afwerkingscode.</summary>
public partial class OfferteRegelEditor : UserControl
{
    public OfferteRegelEditor()
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
