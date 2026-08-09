using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using QuadroApp.Model;
using QuadroApp.Model.DB;
using QuadroApp.ViewModels;
using System;
using System.Linq;

namespace QuadroApp.Views;

public partial class PlanningCalendarWindow : Window
{
    public PlanningCalendarWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // US-49 — drag & drop: regels slepen naar een dagtegel.
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    // ───────── DRAG & DROP (US-50: nieuwe Avalonia DataTransfer-API) ─────────
    // App-eigen formaat voor de gesleepte planregel; blijft binnen de app.
    // Identifier mag alleen letters/cijfers/punt/streepje bevatten (geen '/').
    // De waarde is de RegelId als string (DataFormat<string>).
    private static readonly DataFormat<string> PlanRegelFormat =
        DataFormat.CreateStringApplicationFormat("quadro.planregel");

    // Sleep-start op een regel (op het label, zodat de checkbox klikbaar blijft).
    private async void Regel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not RegelPlanItem regel) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PlanRegelFormat, regel.RegelId.ToString()));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetValue(PlanRegelFormat) is not null
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not PlanningCalendarViewModel vm) return;
        if (e.DataTransfer.TryGetValue(PlanRegelFormat) is not { } raw ||
            !int.TryParse(raw, out var regelId)) return;

        var tile = (e.Source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .Select(x => x.DataContext)
            .OfType<DayTile>()
            .FirstOrDefault();
        if (tile is null) return;

        await vm.PlanRegelOpDatumAsync(regelId, tile.Date);
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

    // ───────── SLUITEN KNOP ─────────

    private void SluitButton_Click(object? sender, RoutedEventArgs e) => Close();

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
