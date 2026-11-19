using Avalonia.Controls;
using Avalonia.Interactivity;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class StartklaarWindow : Window
{
    public StartklaarWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is StartklaarViewModel vm)
                await vm.LadenCommand.ExecuteAsync(null);
        };
    }

    private void Sluiten_Click(object? sender, RoutedEventArgs e) => Close();
}
