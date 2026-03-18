using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IAppSettingsProvider
{
    Task<decimal> GetUurloon();
    Task<decimal> GetDefaultPrijsPerMeterAsync();
    Task<decimal> GetDefaultWinstFactorAsync();
    Task<decimal> GetDefaultAfvalPercentageAsync();

    Task SavePricingSettingsAsync(
        decimal uurloon,
        decimal defaultPrijsPerMeter,
        decimal defaultWinstFactor,
        decimal defaultAfvalPercentage);
}
