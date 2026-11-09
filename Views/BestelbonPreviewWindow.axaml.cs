using Avalonia.Controls;

namespace QuadroApp.Views;

/// <summary>
/// Voorbeeld van de bestelbon. Feedback Veerle: de PDF ging vroeger bij het openen van dit venster
/// automatisch open in een ander programma (bv. Edge op volledig scherm), waardoor het leek alsof je
/// uit Quadro was en niet terug kon. Nu opent de PDF enkel met de knop «PDF openen».
/// </summary>
public partial class BestelbonPreviewWindow : Window
{
    public BestelbonPreviewWindow()
    {
        InitializeComponent();
    }
}
