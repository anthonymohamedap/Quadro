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

            // Login-focus: staat de gebruikersnaam al voor-ingevuld (onthouden van vorige
            // sessie), spring dan meteen naar het wachtwoordveld; anders naar de naam.
            Opened += (_, _) =>
            {
                if (Vm is null || !Vm.IsLocked) return;
                if (!string.IsNullOrWhiteSpace(Vm.LoginGebruikersnaam))
                    PwdBox?.Focus();
                else
                    UserBox?.Focus();
            };

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
