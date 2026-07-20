using Avalonia.Controls;
using Avalonia.Input;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class OffertesLijstView : UserControl
{
    public OffertesLijstView()
    {
        InitializeComponent();
        // DataContext komt via DataTemplate/Navigation (geen new VM hier)
    }

    // Dubbelklik op een rij ? open de geselecteerde offerte
    private void OffertesList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is OffertesLijstViewModel vm &&
            vm.OpenCommand?.CanExecute(null) == true)
        {
            vm.OpenCommand.Execute(null);
        }
    }
}
