using System.Threading.Tasks;

namespace QuadroApp.Service.Import
{

    public interface IFilePickerService
    {
        Task<string?> PickExcelFileAsync();
    }
}
