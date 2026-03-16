using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;
using System;

namespace QuadroApp.Views;

public partial class PlanningCalendarWindow : Window
{
    public PlanningCalendarWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ───────── DIALOG DELEGATE INJECTEREN ─────────

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not PlanningCalendarViewModel vm) return;

        vm.ShowTijdDialogAsync = async dialogVm =>
        {
            var dialog = new PlanningTijdDialog { DataContext = dialogVm };
            dialogVm.RequestClose = ok => dialog.Close(ok);
            return await dialog.ShowDialog<bool>(this);
        };
    }

    // ───────── DAG TILE KLIK ─────────

    private void DayTile_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not PlanningCalendarViewModel vm) return;
        if (sender is Border b && b.DataContext is DayTile d)
            vm.SelectedDate = d.Date;
    }

    // ───────── CONTEXT MENU HANDLERS ─────────

    private async void OnHerplanMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.DataContext is not WerkTaak taak) return;
        if (DataContext is not PlanningCalendarViewModel vm) return;

        await vm.HerplanTaakCommand.ExecuteAsync(taak);
    }

    private async void OnVerwijderMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.DataContext is not WerkTaak taak) return;
        if (DataContext is not PlanningCalendarViewModel vm) return;

        await vm.VerwijderTaakCommand.ExecuteAsync(taak);
    }
}
