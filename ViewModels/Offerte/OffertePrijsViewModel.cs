using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OffertePrijsViewModel : AsyncViewModelBase
{
    private readonly IPricingService _pricing;
    private readonly IDialogService _dialogs;

    // Delegates geleverd door OfferteViewModel
    private readonly Func<bool, Task<bool>> _runFullValidation;
    private readonly Func<Task> _refreshLijstPrijzen;
    private readonly Func<Offerte> _buildSnapshot;
    private readonly Action<Offerte> _applySnapshot;

    private CancellationTokenSource? _recalcCts;

    [ObservableProperty] private bool isBusy;

    public IAsyncRelayCommand BerekenCommand { get; }

    public OffertePrijsViewModel(
        IPricingService pricing,
        IDialogService dialogs,
        IToastService toast,
        Func<bool, Task<bool>> runFullValidation,
        Func<Task> refreshLijstPrijzen,
        Func<Offerte> buildSnapshot,
        Action<Offerte> applySnapshot)
        : base(toast)
    {
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _runFullValidation = runFullValidation;
        _refreshLijstPrijzen = refreshLijstPrijzen;
        _buildSnapshot = buildSnapshot;
        _applySnapshot = applySnapshot;

        BerekenCommand = new AsyncRelayCommand(() => BerekenAsync(showFeedback: true));
    }

    /// <summary>
    /// Debounced trigger: wordt aangeroepen vanuit OfferteViewModel wanneer
    /// SelectedRegel of een afwerking wijzigt. Niet direct aanroepen vanuit UI.
    /// </summary>
    public void TriggerRecalc() => RunAsync(QueueRecalcAsync);

    /// <summary>
    /// Directe (niet-debounced) berekening zonder UI-feedback.
    /// Gebruikt door OfferteWorkflowViewModel vóór factuur aanmaken.
    /// </summary>
    public Task BerekenSilentAsync() => BerekenAsync(showFeedback: false);

    private async Task QueueRecalcAsync()
    {
        _recalcCts?.Cancel();
        _recalcCts = new CancellationTokenSource();
        var token = _recalcCts.Token;

        try
        {
            await Task.Delay(350, token);
            if (token.IsCancellationRequested) return;
            await BerekenAsync(showFeedback: false);
        }
        catch (TaskCanceledException) { }
    }

    private async Task BerekenAsync(bool showFeedback)
    {
        if (IsBusy) return;

        var ok = await _runFullValidation(showFeedback);
        if (!ok) return;

        try
        {
            IsBusy = true;
            await _refreshLijstPrijzen();
            var snapshot = _buildSnapshot();
            await _pricing.BerekenAsync(snapshot);
            _applySnapshot(snapshot);

            if (showFeedback)
                Toast.Success("Berekening uitgevoerd");
        }
        catch (Exception ex)
        {
            if (showFeedback)
                await _dialogs.ShowErrorAsync("Berekenen mislukt", ex.GetBaseException().Message);
            else
                Toast.Error(ex.GetBaseException().Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
