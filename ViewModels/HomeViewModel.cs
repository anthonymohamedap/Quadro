using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels
{
    public sealed class HomeViewModel
    {
        private readonly INavigationService _nav;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IWerkBonWorkflowService _workflow;
        private readonly IToastService _toast;
        private readonly IWorkflowService _statusWorkflow;
        public IAsyncRelayCommand OpenKlantenCommand { get; }
        public IAsyncRelayCommand OpenLijstenCommand { get; }
        public IAsyncRelayCommand OpenPlanningCommand { get; }
        public IAsyncRelayCommand OpenOfferteCommand { get; }
        public IAsyncRelayCommand OpenOffertesLijstCommand { get; }
        public IAsyncRelayCommand OpenWerkBonCommand { get; }
        public IAsyncRelayCommand OpenAfwerkingsOptiesCommand { get; }
        public IAsyncRelayCommand OpenLeveranciersCommand { get; }

        public HomeViewModel(
            INavigationService nav,
            IDbContextFactory<AppDbContext> factory,
            IWerkBonWorkflowService workflow,
            IToastService toast,
            IWorkflowService statusWorkflow)
        {
            _nav = nav;
            _factory = factory;
            _workflow = workflow;

            OpenPlanningCommand = new AsyncRelayCommand(OpenPlanningAsync);

            OpenWerkBonCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<WerkBonLijstViewModel>());
            OpenKlantenCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<KlantenViewModel>());
            OpenAfwerkingsOptiesCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<AfwerkingenViewModel>());
            OpenLijstenCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LijstenViewModel>());
            OpenLeveranciersCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LeveranciersViewModel>());
            OpenOfferteCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<OfferteViewModel>());
            OpenOffertesLijstCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<OffertesLijstViewModel>());
            _toast = toast;
            _statusWorkflow = statusWorkflow;
        }

        private async Task OpenPlanningAsync()
        {
            var vm = new PlanningCalendarViewModel(_factory, _workflow, _toast, _statusWorkflow);

            // ✅ globale mode: geen werkbon gekoppeld
            await vm.InitializeGlobalAsync();

            var window = new PlanningCalendarWindow
            {
                DataContext = vm
            };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null)
                    return;

                await window.ShowDialog(owner);
            }
        }
    }
}