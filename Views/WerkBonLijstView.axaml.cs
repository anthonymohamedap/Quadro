using Avalonia;
using Avalonia.Controls;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class WerkBonLijstView : UserControl
{
    public WerkBonLijstView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += OnAttached;
    }

    private async void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is WerkBonLijstViewModel vm)
            await vm.LoadAsync();
    }
}