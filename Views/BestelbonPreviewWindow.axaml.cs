using Avalonia.Controls;
using QuadroApp.ViewModels;
using System;

namespace QuadroApp.Views;

public partial class BestelbonPreviewWindow : Window
{
    private bool _previewOpened;

    public BestelbonPreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_previewOpened || DataContext is not BestelbonPreviewViewModel vm)
            return;

        _previewOpened = true;
        await vm.OpenPreviewCommand.ExecuteAsync(null);
    }
}
