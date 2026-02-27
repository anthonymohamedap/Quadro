using Avalonia.Controls;

namespace QuadroApp.Views
{
    public partial class WeekWerkLijstWindow : Window
    {
        public WeekWerkLijstWindow()
        {
            InitializeComponent();
        }
        private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}