using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using QuadroApp.ViewModels.Gebruikers;

namespace QuadroApp.Views;

public partial class GebruikersBeheerWindow : Window
{
    public GebruikersBeheerWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is GebruikersBeheerViewModel vm)
                await vm.InitializeAsync();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
