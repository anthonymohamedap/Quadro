using QuadroApp.Service.Toast;
using System.Collections.ObjectModel;

namespace QuadroApp.Service.Interfaces
{
    public interface IToastService
    {
        /// <summary>
        /// Live collection of active toast messages. Entries are removed automatically
        /// after the duration specified in <see cref="Show"/>. Bind UI overlays to this.
        /// </summary>
        ReadOnlyObservableCollection<ToastMessage> Messages { get; }

        void Show(string message, ToastType type, int durationMs = 3000);

        void Success(string message);
        void Error(string message);
        void Warning(string message);
        void Info(string message);
    }
}
