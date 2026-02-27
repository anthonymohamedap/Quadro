using Avalonia.Controls;

namespace QuadroApp.Views;

public partial class OfferteView : UserControl
{
    public OfferteView()
    {
        InitializeComponent();
        // DataContext komt via DataTemplate / NavigationService (geen new VM hier)
    }
}
