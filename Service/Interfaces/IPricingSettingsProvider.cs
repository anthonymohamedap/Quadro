using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

public interface IPricingSettingsProvider
{
    Task<decimal> GetUurloonAsync();
    Task<decimal> GetBtwPercentAsync();
    Task<decimal> GetStaaflijstWinstFactorAsync();
    Task<decimal> GetStaaflijstAfvalPercentageAsync();
    Task<decimal> GetDefaultWinstFactorAsync();
    Task<decimal> GetDefaultAfvalPercentageAsync();
}
