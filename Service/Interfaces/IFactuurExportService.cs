using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IFactuurExportService
{
    Task<ExportResult> ExportAsync(int factuurId, ExportFormaat formaat, string exportFolder);
}
