using QuadroApp.Model.Toast;
using QuadroApp.Service.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
namespace QuadroApp.Service.Toast
{


    public class ToastService : IToastService
    {
        public ObservableCollection<ToastMessage> Messages { get; } = new();

        public void Show(string message, ToastType type, int durationMs = 3000)
        {
            var toast = new ToastMessage(message, type);
            Messages.Add(toast);

            _ = RemoveLaterAsync(toast, durationMs);
        }

        public void Success(string message) => Show(message, ToastType.Success);
        public void Error(string message) => Show(message, ToastType.Error);
        public void Warning(string message) => Show(message, ToastType.Warning);
        public void Info(string message) => Show(message, ToastType.Info);

        private async Task RemoveLaterAsync(ToastMessage toast, int delay)
        {
            await Task.Delay(delay);
            toast.IsVisible = false;
            await Task.Delay(300); // animatie buffer
            Messages.Remove(toast);
        }
    }
}
