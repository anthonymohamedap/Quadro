using Avalonia.Controls;

namespace QuadroApp.Views;

/// <summary>Eerste start op een nieuwe laptop: databasewachtwoord vragen (zie <see cref="ViewModels.KoppelViewModel"/>).</summary>
public partial class KoppelWindow : Window
{
    public KoppelWindow()
    {
        InitializeComponent();
        Opened += (_, _) => WachtwoordVak.Focus();
    }
}
