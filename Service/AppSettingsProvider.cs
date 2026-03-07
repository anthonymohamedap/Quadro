using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class AppSettingsProvider : IAppSettingsProvider
{
    private const decimal DefaultStaaflijstWinstFactor = 3.5m;
    private const decimal DefaultStaaflijstAfvalPercentage = 20m;
    private const decimal DefaultWinstFactor = 0m;
    private const decimal DefaultAfvalPercentage = 0m;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public AppSettingsProvider(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<decimal> GetStaaflijstWinstFactorAsync() =>
        await ReadDecimalAsync("StaaflijstWinstFactor", DefaultStaaflijstWinstFactor);

    public async Task<decimal> GetStaaflijstAfvalPercentageAsync() =>
        await ReadDecimalAsync("StaaflijstAfvalPercentage", DefaultStaaflijstAfvalPercentage);

    public async Task<decimal> GetDefaultWinstFactorAsync() =>
        await ReadDecimalAsync("DefaultWinstFactor", DefaultWinstFactor);

    public async Task<decimal> GetDefaultAfvalPercentageAsync() =>
        await ReadDecimalAsync("DefaultAfvalPercentage", DefaultAfvalPercentage);

    public async Task SavePricingSettingsAsync(
        decimal staaflijstWinstFactor,
        decimal staaflijstAfvalPercentage,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        SaveDecimal(db, "StaaflijstWinstFactor", staaflijstWinstFactor);
        SaveDecimal(db, "StaaflijstAfvalPercentage", staaflijstAfvalPercentage);
        SaveDecimal(db, "DefaultWinstFactor", defaultWinstFactor);
        SaveDecimal(db, "DefaultAfvalPercentage", defaultAfvalPercentage);

        await db.SaveChangesAsync();
    }

    private async Task<decimal> ReadDecimalAsync(string key, decimal fallback)
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

    private static void SaveDecimal(AppDbContext db, string key, decimal value)
    {
        var valueText = value.ToString(CultureInfo.InvariantCulture);
        var setting = db.Instellingen.FirstOrDefault(x => x.Sleutel == key);

        if (setting is null)
        {
            db.Instellingen.Add(new Instelling { Sleutel = key, Waarde = valueText });
            return;
        }

        setting.Waarde = valueText;
    }
}
