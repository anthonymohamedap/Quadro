using Avalonia.Controls;
using Avalonia.Input;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class KlantDetailView : UserControl
{
    public KlantDetailView()
    {
        InitializeComponent();
    }

    private void OffertesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is KlantDetailViewModel vm &&
            vm.OpenOfferteCommand.CanExecute(vm.SelectedOfferte))
        {
            vm.OpenOfferteCommand.Execute(vm.SelectedOfferte);
        }
    }
}
