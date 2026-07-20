using Avalonia.Markup.Xaml;
using Huskui.Avalonia.Controls;

namespace QuadroApp
{
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
