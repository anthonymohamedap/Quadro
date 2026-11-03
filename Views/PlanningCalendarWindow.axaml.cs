using Avalonia.Controls;
using Avalonia.Interactivity;

namespace QuadroApp.Views;

/// <summary>
/// Planningsvenster dat de oude schermen (offerte, werkbonnen, offertelijst) nog openen.
/// US-92: de inhoud is <see cref="Planning.PlanningKalender"/>, ook gebruikt als pagina en in stap 2.
/// </summary>
public partial class PlanningCalendarWindow : Window
{
    public PlanningCalendarWindow()
    {
        InitializeComponent();
    }

    private void SluitButton_Click(object? sender, RoutedEventArgs e) => Close();
}
