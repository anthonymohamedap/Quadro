using QuadroApp.Model.DB;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface IAfwerkingenService
    {
        Task<List<AfwerkingsGroep>> GetGroepenAsync();
        Task<List<Leverancier>> GetLeveranciersAsync();
        Task<List<AfwerkingsOptie>> GetOptiesAsync(int? groepId);

        Task SaveOptieAsync(AfwerkingsOptie optie);
        Task<AfwerkingsOptie> CreateNieuweOptieAsync(int groepId);
        Task DeleteOptieAsync(AfwerkingsOptie optie);
    }

}
