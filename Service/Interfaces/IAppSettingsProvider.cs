using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IAppSettingsProvider
{
    Task<decimal> GetStaaflijstWinstFactorAsync();
    Task<decimal> GetStaaflijstAfvalPercentageAsync();
    Task<decimal> GetDefaultWinstFactorAsync();
    Task<decimal> GetDefaultAfvalPercentageAsync();

    Task SavePricingSettingsAsync(
        decimal staaflijstWinstFactor,
        decimal staaflijstAfvalPercentage,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage);
}
