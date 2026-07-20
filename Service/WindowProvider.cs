using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.Service
{
    public sealed class WindowProvider : IWindowProvider
    {
        public Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;

            return null;
        }
    }
}
