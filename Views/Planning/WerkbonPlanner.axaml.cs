using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using QuadroApp.ViewModels;
using System;
using System.Linq;

namespace QuadroApp.Views.Planning;

/// <summary>US-92 — planner van stap 2: slepen van een inlijsting naar een dag en de tijd-dialoog (herplannen, plan per regel).</summary>
public partial class WerkbonPlanner : UserControl
{
    private static readonly DataFormat<string> PlanRegelFormat =
        DataFormat.CreateStringApplicationFormat("quadro.planregel");

    public WerkbonPlanner()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not WerkbonPlannerViewModel vm) return;
        vm.Kalender.ShowTijdDialogAsync = async dialogVm =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return false;
            var dialog = new PlanningTijdDialog { DataContext = dialogVm };
            dialogVm.RequestClose = ok => dialog.Close(ok);
            return await dialog.ShowDialog<bool>(owner);
        };
    }

    private async void Regel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control c || c.DataContext is not PlannerRegel regel || regel.IsGepland) return;
        if (!e.GetCurrentPoint(c).Properties.IsLeftButtonPressed) return;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PlanRegelFormat, regel.RegelId.ToString()));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetValue(PlanRegelFormat) is not null && DagOnder(e) is { KanPlannen: true }
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not WerkbonPlannerViewModel vm) return;
        if (e.DataTransfer.TryGetValue(PlanRegelFormat) is not { } raw || !int.TryParse(raw, out var regelId)) return;
        if (DagOnder(e) is not { KanPlannen: true } dag) return;
        await vm.PlanRegelOpDagAsync(regelId, dag.Datum);
    }

    private static PlannerDag? DagOnder(DragEventArgs e)
        => (e.Source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .Select(x => x.DataContext)
            .OfType<PlannerDag>()
            .FirstOrDefault();
}
