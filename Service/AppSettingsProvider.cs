using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Model;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class AppSettingsProvider : IAppSettingsProvider
{
    private const decimal DefaultPrijsPerMeter = 0m;
    private const decimal DefaultWinstFactor = 0m;
    private const decimal DefaultAfvalPercentage = 0m;
    private const string LastExportFolderKey = "LastExportFolder";
    private const string LastExportPresetKey = "LastExportPreset";
    private const string LastExportDatasetKey = "LastExportDataset";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public AppSettingsProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<decimal> GetUurloon() =>
        await ReadDecimalAsync("Uurloon", 60);

    public async Task<decimal> GetDefaultPrijsPerMeterAsync() =>
        await ReadDecimalAsync("DefaultPrijsPerMeter", DefaultPrijsPerMeter);

    public async Task<decimal> GetDefaultWinstFactorAsync() =>
        await ReadDecimalAsync("DefaultWinstFactor", DefaultWinstFactor);

    public async Task<decimal> GetDefaultAfvalPercentageAsync() =>
        await ReadDecimalAsync("DefaultAfvalPercentage", DefaultAfvalPercentage);

    public async Task SavePricingSettingsAsync(
        decimal uurloon,
        decimal defaultPrijsPerMeter,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        SaveDecimal(db, "Uurloon", uurloon);
        SaveDecimal(db, "DefaultPrijsPerMeter", defaultPrijsPerMeter);
        SaveDecimal(db, "DefaultWinstFactor", defaultWinstFactor);
        SaveDecimal(db, "DefaultAfvalPercentage", defaultAfvalPercentage);

        await db.SaveChangesAsync();
    }

    public Task<string?> GetLastExportFolderAsync() =>
        ReadStringAsync(LastExportFolderKey);

    public Task<string?> GetLastExportPresetAsync() =>
        ReadStringAsync(LastExportPresetKey);

    public async Task<ExcelExportDataset?> GetLastExportDatasetAsync()
    {
        var datasetText = await ReadStringAsync(LastExportDatasetKey);
        if (string.IsNullOrWhiteSpace(datasetText))
            return null;

        return Enum.TryParse<ExcelExportDataset>(datasetText, true, out var dataset)
            ? dataset
            : null;
    }

    public async Task SaveLastExportFolderAsync(string folder)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        SaveString(db, LastExportFolderKey, folder);
        await db.SaveChangesAsync();
    }

    public async Task SaveLastExportSelectionAsync(ExcelExportDataset dataset, string? presetSleutel)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        SaveString(db, LastExportDatasetKey, dataset.ToString());
        SaveString(db, LastExportPresetKey, presetSleutel ?? string.Empty);
        await db.SaveChangesAsync();
    }

    private async Task<decimal> ReadDecimalAsync(string key, decimal fallback)
    {
        var value = await ReadStringAsync(key);

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            return parsed;

        return fallback;
    }

    private async Task<string?> ReadStringAsync(string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Instellingen
            .AsNoTracking()
            .Where(x => x.Sleutel == key)
            .Select(x => x.Waarde)
            .FirstOrDefaultAsync();
    }

    private static void SaveDecimal(AppDbContext db, string key, decimal value)
    {
        var valueText = value.ToString(CultureInfo.InvariantCulture);
        SaveString(db, key, valueText);
    }

    private static void SaveString(AppDbContext db, string key, string value)
    {
        var setting = db.Instellingen.FirstOrDefault(x => x.Sleutel == key);

        if (setting is null)
        {
            db.Instellingen.Add(new Instelling { Sleutel = key, Waarde = value });
            return;
        }

        setting.Waarde = value;
    }
}
