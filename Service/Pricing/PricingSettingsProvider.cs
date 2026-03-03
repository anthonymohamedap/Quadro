using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service.Pricing;

public sealed class PricingSettingsProvider : IPricingSettingsProvider
{
    private const decimal DefaultUurloon = 45m;
    private const decimal DefaultBtwPercent = 21m;
    private const decimal DefaultStaaflijstWinstFactor = 3.5m;
    private const decimal DefaultStaaflijstAfvalPercentage = 20m;
    private const decimal DefaultWinstFactor = 0m;
    private const decimal DefaultAfvalPercentage = 0m;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PricingSettingsProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<decimal> GetUurloonAsync() =>
        await GetDecimalSettingAsync("Uurloon", DefaultUurloon);

    public async Task<decimal> GetBtwPercentAsync() =>
        await GetDecimalSettingAsync("BtwPercent", DefaultBtwPercent);

    public async Task<decimal> GetStaaflijstWinstFactorAsync() =>
        await GetDecimalSettingAsync("StaaflijstWinstFactor", DefaultStaaflijstWinstFactor);

    public async Task<decimal> GetStaaflijstAfvalPercentageAsync() =>
        await GetDecimalSettingAsync("StaaflijstAfvalPercentage", DefaultStaaflijstAfvalPercentage);

    public async Task<decimal> GetDefaultWinstFactorAsync() =>
        await GetDecimalSettingAsync("DefaultWinstFactor", DefaultWinstFactor);

    public async Task<decimal> GetDefaultAfvalPercentageAsync() =>
        await GetDecimalSettingAsync("DefaultAfvalPercentage", DefaultAfvalPercentage);

    private async Task<decimal> GetDecimalSettingAsync(string key, decimal fallback)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var value = await db.Instellingen
            .AsNoTracking()
            .Where(x => x.Sleutel == key)
            .Select(x => x.Waarde)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            return parsed;

        return fallback;
    }
}
