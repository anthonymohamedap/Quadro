using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Huskui.Avalonia.Controls;
using Huskui.Avalonia.Models;
using QuadroApp.Model.Toast;
using QuadroApp.Service.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.Service.Toast
{
    public class ToastService : IToastService
    {
        private readonly ObservableCollection<ToastMessage> _messages = new();

        public ReadOnlyObservableCollection<ToastMessage> Messages { get; }

        public ToastService()
        {
            Messages = new ReadOnlyObservableCollection<ToastMessage>(_messages);
        }

        private AppWindow? GetAppWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow as AppWindow;
            return null;
        }

        public void Show(string message, ToastType type, int durationMs = 3000)
        {
            // 1. Huskui growl on the main AppWindow
            var appWindow = GetAppWindow();
            if (appWindow != null)
            {
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

            // 2. Messages collection — used by custom overlays (e.g. PlanningCalendarWindow).
            //    Always dispatched on the UI thread; auto-removed after durationMs.
            var msg = new ToastMessage(message, type);

            Dispatcher.UIThread.Post(() => _messages.Add(msg));

            _ = Task.Delay(durationMs).ContinueWith(_ =>
                Dispatcher.UIThread.Post(() =>
                {
                    msg.IsVisible = false;
                    _messages.Remove(msg);
                }));
        }

        public void Success(string message) => Show(message, ToastType.Success);
        public void Error(string message)   => Show(message, ToastType.Error);
        public void Warning(string message) => Show(message, ToastType.Warning);
        public void Info(string message)    => Show(message, ToastType.Info);
    }
}
