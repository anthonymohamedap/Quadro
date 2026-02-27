using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class PlanningCalendarWindow : Window
{
    public PlanningCalendarWindow()
    {
        InitializeComponent();
        this.Background = Avalonia.Media.Brushes.White;

    }
    private async void DayTile_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not PlanningCalendarViewModel vm) return;
        if (sender is Border b && b.DataContext is DayTile d)
        {
            vm.SelectedDate = d.Date;
            await vm.LoadTakenVanDagAsync();
            await vm.LoadWeekRowsAsync(System.Globalization.ISOWeek.GetWeekOfYear(d.Date));
        }
    }

    private void DayTile_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PlanningCalendarViewModel vm) return;
        if (sender is Border b && b.DataContext is DayTile d)
            vm.SelectedDate = d.Date;
    }
}