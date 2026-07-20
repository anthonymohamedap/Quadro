using Avalonia.Controls;
using Avalonia.Input;
using QuadroApp.ViewModels;

namespace QuadroApp.Views
{
    public partial class KlantenView : UserControl
    {
        public KlantenView()
        {
            InitializeComponent();
        }

        private void KlantenList_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is KlantenViewModel vm &&
                vm.OpenKlantDetailCommand.CanExecute(vm.SelectedKlant))
            {
                vm.OpenKlantDetailCommand.Execute(vm.SelectedKlant);
            }
        }
    }
}
