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
        }

        private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
