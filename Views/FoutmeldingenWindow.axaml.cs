using Avalonia.Controls;
using Avalonia.Interactivity;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class FoutmeldingenWindow : Window
{
    public FoutmeldingenWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is FoutmeldingenViewModel vm)
                await vm.LadenCommand.ExecuteAsync(null);
        };
    }

    private void Sluiten_Click(object? sender, RoutedEventArgs e) => Close();
}
