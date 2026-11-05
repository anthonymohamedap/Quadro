using Avalonia.Controls;
using Avalonia.Interactivity;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;

namespace QuadroApp.Views.Planning;

/// <summary>
/// US-92 — dagdetails van de planning: gepland/beschikbaar en alle taken van de gekozen dag (ook van
/// andere verkopen), met herplannen en verwijderen via het contextmenu (zoals voorheen).
/// </summary>
public partial class PlanningDagDetails : UserControl
{
    public PlanningDagDetails()
    {
        InitializeComponent();
    }

    private async void OnHerplanMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WerkTaak taak }) return;
        if (DataContext is not PlanningCalendarViewModel vm) return;
        await vm.HerplanTaakCommand.ExecuteAsync(taak);
    }

    private async void OnVerwijderMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WerkTaak taak }) return;
        if (DataContext is not PlanningCalendarViewModel vm) return;
        await vm.VerwijderTaakCommand.ExecuteAsync(taak);
    }
}
