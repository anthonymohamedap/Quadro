using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using System.Globalization;
using System.Threading.Tasks;

namespace QuadroApp.Service.Pricing;

public sealed class PricingSettingsProvider : IPricingSettingsProvider
{
    private const decimal DefaultUurloon = 45m;
    private const decimal DefaultBtwPercent = 21m;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PricingSettingsProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<decimal> GetUurloonAsync() =>
        await GetDecimalSettingAsync("Uurloon", DefaultUurloon);

    public async Task<decimal> GetBtwPercentAsync() =>
        await GetDecimalSettingAsync("BtwPercent", DefaultBtwPercent);

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

