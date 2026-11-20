using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuadroApp.Views.Planning;

/// <summary>Venster voor dagdetail of weekdetail van de planning (vroeger onderaan de planning).</summary>
public partial class PlanningDetailVenster : Window
{
    public PlanningDetailVenster()
    {
        InitializeComponent();
    }

    public PlanningDetailVenster(string titel, Control inhoud, object? dataContext) : this()
    {
        Title = titel;
        DataContext = dataContext;
        inhoud.DataContext = dataContext;
        Inhoud.Content = inhoud;
    }

    private void Sluiten_Click(object? sender, RoutedEventArgs e) => Close();
}
