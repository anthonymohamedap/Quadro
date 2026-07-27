using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class AuditLogWindow : Window
{
    public AuditLogWindow()
    {
        InitializeComponent();

        Opened += async (_, _) =>
        {
            if (DataContext is AuditLogViewModel vm)
                await vm.InitializeAsync();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
