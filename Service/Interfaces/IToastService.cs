using QuadroApp.Service.Toast;

namespace QuadroApp.Service.Interfaces
{
    public interface IToastService
    {
        void Show(string message, ToastType type, int durationMs = 3000);

        void Success(string message);
        void Error(string message);
        void Warning(string message);
        void Info(string message);
    }
}
