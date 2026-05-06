using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Toast;
using QuadroApp.Service.Interfaces;
using System;
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

        public void Show(string message, ToastType type, int durationMs = 2000)
        {
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

        public void Info(string message, string actionLabel, Action onAction, int durationMs = 30_000)
        {
            var msg = new ToastMessage(message, ToastType.Info)
            {
                ActionLabel  = actionLabel,
                ActionCommand = new RelayCommand(onAction)
            };

            Dispatcher.UIThread.Post(() => _messages.Add(msg));

            _ = Task.Delay(durationMs).ContinueWith(_ =>
                Dispatcher.UIThread.Post(() =>
                {
                    msg.IsVisible = false;
                    _messages.Remove(msg);
                }));
        }
    }
}
