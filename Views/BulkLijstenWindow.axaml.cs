using Avalonia.Controls;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;
using System.Linq;

namespace QuadroApp.Views;

public partial class BulkLijstenWindow : Window
{
    public BulkLijstenWindow()
    {
        InitializeComponent();
    }

    private void BulkLijstenList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not BulkLijstenViewModel vm || sender is not ListBox listBox)
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
