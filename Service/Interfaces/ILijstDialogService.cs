using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface ILijstDialogService
    {
        Task<TypeLijst?> EditAsync(TypeLijst lijst);
    }
}
