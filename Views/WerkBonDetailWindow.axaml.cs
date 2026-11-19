using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuadroApp.ViewModels;
using System;

namespace QuadroApp.Views;

public partial class WerkBonDetailWindow : Window
{
    public WerkBonDetailWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is WerkBonDetailViewModel vm)
            vm.RequestClose = Close;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
