using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Huskui.Avalonia.Controls;
using QuadroApp.ViewModels;

namespace QuadroApp
{
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            // US-32: elke muis/toets-activiteit reset de auto-lock timer.
            AddHandler(PointerPressedEvent, (_, _) => Vm?.RegistreerActiviteit(), handledEventsToo: true);
            AddHandler(KeyDownEvent, (_, _) => Vm?.RegistreerActiviteit(), handledEventsToo: true);

            // Verplichte wachtwoordwijziging: open de dialoog direct na login.
            DataContextChanged += (_, _) =>
            {
                if (Vm is not null)
                    Vm.WachtwoordWijzigenVereist += async (_, _) =>
                    {
                        var dialog = new Views.WachtwoordWijzigDialog
                        {
                            DataContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                                .GetRequiredService<ViewModels.Gebruikers.WachtwoordWijzigViewModel>(App.Services)
                        };
                        await dialog.ShowDialog(this);
                    };
            };
        }

        private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
