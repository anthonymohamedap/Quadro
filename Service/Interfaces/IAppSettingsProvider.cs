using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IAppSettingsProvider
{
    Task<decimal> GetUurloon();
    Task<decimal> GetDefaultPrijsPerMeterAsync();
    Task<decimal> GetDefaultWinstFactorAsync();
    Task<decimal> GetDefaultAfvalPercentageAsync();
    Task<string?> GetLastExportFolderAsync();
    Task<string?> GetLastExportPresetAsync();
    Task<ExcelExportDataset?> GetLastExportDatasetAsync();

    Task SavePricingSettingsAsync(
        decimal uurloon,
        decimal defaultPrijsPerMeter,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage);

    Task SaveLastExportFolderAsync(string folder);
    Task SaveLastExportSelectionAsync(ExcelExportDataset dataset, string? presetSleutel);
}
