using QuadroApp.Model.DB;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IKlantDialogService
    {
        Task<Klant?> EditAsync(Klant klant);
    }

}
