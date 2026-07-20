using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{

    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }

}
