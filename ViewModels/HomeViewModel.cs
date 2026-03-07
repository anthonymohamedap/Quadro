using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using QuadroApp.Views;
using System;
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
        private readonly IServiceProvider _services;

        public IAsyncRelayCommand OpenKlantenCommand { get; }
        public IAsyncRelayCommand OpenLijstenCommand { get; }
        public IAsyncRelayCommand OpenPlanningCommand { get; }
        public IAsyncRelayCommand OpenOfferteCommand { get; }
        public IAsyncRelayCommand OpenOffertesLijstCommand { get; }
        public IAsyncRelayCommand OpenWerkBonCommand { get; }
        public IAsyncRelayCommand OpenAfwerkingsOptiesCommand { get; }
        public IAsyncRelayCommand OpenLeveranciersCommand { get; }
        public IAsyncRelayCommand OpenFacturenCommand { get; }
        public IAsyncRelayCommand OpenInstellingenCommand { get; }

        public HomeViewModel(
            INavigationService nav,
            IDbContextFactory<AppDbContext> factory,
            IWerkBonWorkflowService workflow,
            IToastService toast,
            IWorkflowService statusWorkflow,
            IServiceProvider services)
        {
            _nav = nav;
            _factory = factory;
            _workflow = workflow;
            _toast = toast;
            _statusWorkflow = statusWorkflow;
            _services = services;

            OpenPlanningCommand = new AsyncRelayCommand(OpenPlanningAsync);
            OpenInstellingenCommand = new AsyncRelayCommand(OpenInstellingenAsync);

            OpenWerkBonCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<WerkBonLijstViewModel>());
            OpenKlantenCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<KlantenViewModel>());
            OpenAfwerkingsOptiesCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<AfwerkingenViewModel>());
            OpenLijstenCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LijstenViewModel>());
            OpenLeveranciersCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<LeveranciersViewModel>());
            OpenOfferteCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<OfferteViewModel>());
            OpenOffertesLijstCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<OffertesLijstViewModel>());
            OpenFacturenCommand = new AsyncRelayCommand(() => _nav.NavigateToAsync<FacturenViewModel>());
        }

        private async Task OpenPlanningAsync()
        {
            var vm = new PlanningCalendarViewModel(_factory, _workflow, _toast, _statusWorkflow);
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

        private async Task OpenInstellingenAsync()
        {
            var vm = _services.GetRequiredService<InstellingenViewModel>();
            await vm.LoadAsync();

            var window = new InstellingenWindow
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
