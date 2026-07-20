using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuadroApp.Views;

public partial class InstellingenWindow : Window
{
    public InstellingenWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
