using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestAppSettingsProvider : IAppSettingsProvider
{
    public decimal Uurloon { get; set; } = 60m;
    public decimal DefaultPrijsPerMeter { get; set; }
    public decimal DefaultWinstFactor { get; set; }
    public decimal DefaultAfvalPercentage { get; set; }
    public string? LastExportFolder { get; set; }
    public string? LastExportPreset { get; set; }
    public ExcelExportDataset? LastExportDataset { get; set; }

    public Task<decimal> GetUurloon() => Task.FromResult(Uurloon);
    public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(DefaultPrijsPerMeter);
    public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(DefaultWinstFactor);
    public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(DefaultAfvalPercentage);
    public Task<string?> GetLastExportFolderAsync() => Task.FromResult(LastExportFolder);
    public Task<string?> GetLastExportPresetAsync() => Task.FromResult(LastExportPreset);
    public Task<ExcelExportDataset?> GetLastExportDatasetAsync() => Task.FromResult(LastExportDataset);

    public Task SavePricingSettingsAsync(
        decimal uurloon,
        decimal defaultPrijsPerMeter,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage)
    {
        Uurloon = uurloon;
        DefaultPrijsPerMeter = defaultPrijsPerMeter;
        DefaultWinstFactor = defaultWinstFactor;
        DefaultAfvalPercentage = defaultAfvalPercentage;
        return Task.CompletedTask;
    }

    public Task SaveLastExportFolderAsync(string folder)
    {
        LastExportFolder = folder;
        return Task.CompletedTask;
    }

    public Task SaveLastExportSelectionAsync(ExcelExportDataset dataset, string? presetSleutel)
    {
        LastExportDataset = dataset;
        LastExportPreset = presetSleutel;
        return Task.CompletedTask;
    }
}
