using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Huskui.Avalonia.Controls;
using Huskui.Avalonia.Models;
using QuadroApp.Service.Interfaces;

namespace QuadroApp.Service.Toast
{
    public class ToastService : IToastService
    {
        private AppWindow? GetAppWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow as AppWindow;
            return null;
        }

        public void Show(string message, ToastType type, int durationMs = 3000)
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            var level = type switch
            {
                ToastType.Success => GrowlLevel.Success,
                ToastType.Error   => GrowlLevel.Danger,
                ToastType.Warning => GrowlLevel.Warning,
                _                 => GrowlLevel.Information
            };

            appWindow.PopGrowl(new GrowlItem
            {
                Level   = level,
                Content = message
            });
        }

        public void Success(string message) => Show(message, ToastType.Success);
        public void Error(string message)   => Show(message, ToastType.Error);
        public void Warning(string message) => Show(message, ToastType.Warning);
        public void Info(string message)    => Show(message, ToastType.Info);
    }
}
