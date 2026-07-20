using Avalonia.Controls;
using QuadroApp.ViewModels;
using System;

namespace QuadroApp.Views;

public partial class FactuurPreviewWindow : Window
{
    private bool _previewOpened;

    public FactuurPreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_previewOpened || DataContext is not FactuurPreviewViewModel vm)
            return;

        _previewOpened = true;
        await vm.OpenPreviewCommand.ExecuteAsync(null);
    }
}
