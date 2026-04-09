using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.ViewModels;

namespace QuadroApp.Views;

public partial class LijstenWindow : Window
{
    public LijstenWindow()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<LijstenViewModel>();
        vm.OnTerug = () => Close();
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }
}
