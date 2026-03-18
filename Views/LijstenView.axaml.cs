using Avalonia.Controls;
using System.Linq;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class LijstenView : UserControl
{
    public LijstenView()
    {
        InitializeComponent();
    }

    private void LijstenList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not LijstenViewModel vm || sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.SelectedItems is null)
        {
            return;
        }

        vm.UpdateSelectedLijsten(listBox.SelectedItems.OfType<TypeLijst>());
    }

}
