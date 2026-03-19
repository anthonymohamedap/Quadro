using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface ICentralExcelExportService
{
    Task<ExportResult> ExportAsync(ExcelExportDataset dataset, string exportFolder);
}
