using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuadroApp.Views;

/// <summary>US-95 — lijsten bestellen voor een werkbon (DataContext = WerkBonDetailViewModel).</summary>
public partial class LijstenBestellenDialog : Window
{
    public LijstenBestellenDialog()
    {
        InitializeComponent();
    }

    private void Sluiten_Click(object? sender, RoutedEventArgs e) => Close();
}
