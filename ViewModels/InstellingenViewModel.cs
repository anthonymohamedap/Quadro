using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service.Interfaces;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class InstellingenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IAppSettingsProvider _settings;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;

    [ObservableProperty] private decimal staaflijstWinstFactor;
    [ObservableProperty] private decimal staaflijstAfvalPercentage;
    [ObservableProperty] private decimal defaultWinstFactor;
    [ObservableProperty] private decimal defaultAfvalPercentage;

    [ObservableProperty] private bool isBusy;

    public InstellingenViewModel(
        IAppSettingsProvider settings,
        IDialogService dialogs,
        IToastService toast)
    {
        _settings = settings;
        _dialogs = dialogs;
        _toast = toast;
    }

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StaaflijstWinstFactor = await _settings.GetStaaflijstWinstFactorAsync();
            StaaflijstAfvalPercentage = await _settings.GetStaaflijstAfvalPercentageAsync();
            DefaultWinstFactor = await _settings.GetDefaultWinstFactorAsync();
            DefaultAfvalPercentage = await _settings.GetDefaultAfvalPercentageAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (StaaflijstWinstFactor < 0 || StaaflijstAfvalPercentage < 0 || DefaultWinstFactor < 0 || DefaultAfvalPercentage < 0)
        {
            await _dialogs.ShowErrorAsync("Ongeldige instellingen", "Waarden moeten groter dan of gelijk aan 0 zijn.");
            return;
        }

        await _settings.SavePricingSettingsAsync(
            StaaflijstWinstFactor,
            StaaflijstAfvalPercentage,
            DefaultWinstFactor,
            DefaultAfvalPercentage);

        _toast.Success("Prijsinstellingen opgeslagen");
    }
}
