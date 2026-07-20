using QuadroApp.Service.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface ICentralExcelExportService
{
    Task<ExportResult> ExportAsync(ExcelExportDataset dataset, string exportFolder);
    Task<ExportResult> ExportAsync(ExportAanvraag aanvraag, string exportFolder);
    Task<IReadOnlyList<ExportDatasetOptie>> GetBeschikbareDatasetsAsync();
    Task<IReadOnlyList<ExportPresetOptie>> GetStandaardPresetsAsync();
    Task<ExportConfiguratie> MaakConfiguratieAsync(ExcelExportDataset dataset, string? presetSleutel = null);
}
