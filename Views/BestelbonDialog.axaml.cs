using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuadroApp.Views;

/// <summary>US-95 — bestelbon en betalingen van één verkoop (DataContext = BestelbonnenViewModel).</summary>
public partial class BestelbonDialog : Window
{
    public BestelbonDialog()
    {
        InitializeComponent();
    }

    private void Sluiten_Click(object? sender, RoutedEventArgs e) => Close();
}
