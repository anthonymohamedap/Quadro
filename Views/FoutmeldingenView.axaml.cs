using Avalonia.Controls;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class FoutmeldingenView : UserControl
{
    public FoutmeldingenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => KoppelKlembord();
        AttachedToVisualTree += (_, _) => KoppelKlembord();
    }

    private void KoppelKlembord()
    {
        if (DataContext is FoutmeldingenViewModel vm)
            vm.Klembord = async tekst =>
            {
                var klembord = TopLevel.GetTopLevel(this)?.Clipboard;
                if (klembord is not null) await klembord.SetTextAsync(tekst);
            };
    }
}
