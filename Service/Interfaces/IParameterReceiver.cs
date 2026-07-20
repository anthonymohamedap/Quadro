using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IParameterReceiver<T>
    {
        Task ReceiveAsync(T parameter);
    }
}
