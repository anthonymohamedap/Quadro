using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IFactuurExportService
{
    Task<ExportResult> GeneratePreviewAsync(int factuurId, ExportFormaat formaat, string exportFolder);
    Task<ExportResult> ExportAsync(int factuurId, ExportFormaat formaat, string exportFolder);
}
