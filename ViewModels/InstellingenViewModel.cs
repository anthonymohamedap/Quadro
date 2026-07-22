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

    [ObservableProperty] private decimal defaultWinstFactor;
    [ObservableProperty] private decimal defaultAfvalPercentage;
    [ObservableProperty] private decimal defaultPrijsPerMeter;
    [ObservableProperty] private decimal uurloon;

    [ObservableProperty] private bool isBusy;

    private readonly QuadroApp.Service.Interfaces.IAuthService _auth;

    public bool MagPrijzenWijzigen { get; }

    public InstellingenViewModel(
        IAppSettingsProvider settings,
        IDialogService dialogs,
        IToastService toast,
        QuadroApp.Service.Interfaces.IAuthService auth)
    {
        _settings = settings;
        _dialogs = dialogs;
        _toast = toast;
        _auth = auth;
        MagPrijzenWijzigen = _auth.HeeftPermissie(QuadroApp.Service.Security.Permissie.PrijzenWijzigen);
    }

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Uurloon = await _settings.GetUurloon();
            DefaultPrijsPerMeter = await _settings.GetDefaultPrijsPerMeterAsync();
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
        // US-32: globale prijsinstellingen zijn rol-afhankelijk
        if (!_auth.HeeftPermissie(QuadroApp.Service.Security.Permissie.PrijzenWijzigen))
        {
            _toast.Warning("Onvoldoende rechten om prijsinstellingen te wijzigen.");
            return;
        }

        if (DefaultPrijsPerMeter < 0 || DefaultWinstFactor < 0 || DefaultAfvalPercentage < 0)
        {
            await _dialogs.ShowErrorAsync("Ongeldige instellingen", "Waarden moeten groter dan of gelijk aan 0 zijn.");
            return;
        }

        await _settings.SavePricingSettingsAsync(
Uurloon,
            DefaultPrijsPerMeter,
            DefaultWinstFactor,
            DefaultAfvalPercentage);

        _toast.Success("Prijsinstellingen opgeslagen");
    }
}
