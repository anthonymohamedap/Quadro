using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class WachtwoordWijzigDialog : Window
{
    public WachtwoordWijzigDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is WachtwoordWijzigViewModel vm)
                vm.SluitBijSucces = () => Close(true);
        };
    }

    private void OnAnnuleren(object? sender, RoutedEventArgs e) => Close(false);

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
