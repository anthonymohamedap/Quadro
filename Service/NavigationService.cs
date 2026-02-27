using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service
{
    public sealed class NavigationService : INavigationService
    {
        private readonly IServiceProvider _sp;

        public NavigationService(IServiceProvider sp)
            => _sp = sp;

        public object? CurrentViewModel { get; private set; }

        public event Action<object?>? CurrentViewModelChanged;

        public async Task NavigateToAsync<TViewModel>()
            where TViewModel : class
        {
            var vm = _sp.GetRequiredService<TViewModel>();

            await SetAndInitializeAsync(vm);
        }

        public async Task NavigateToAsync<TViewModel, TParam>(TParam param)
            where TViewModel : class
        {
            var vm = _sp.GetRequiredService<TViewModel>();

            // 🔥 parameter injectie
            if (vm is IParameterReceiver<TParam> receiver)
                await receiver.ReceiveAsync(param);

            await SetAndInitializeAsync(vm);
        }

        public async Task NavigateToAsync(object viewModel)
        {
            await SetAndInitializeAsync(viewModel);
        }

        private async Task SetAndInitializeAsync(object vm)
        {
            CurrentViewModel = vm;
            CurrentViewModelChanged?.Invoke(CurrentViewModel);

            if (vm is IAsyncInitializable asyncInit)
                await asyncInit.InitializeAsync();
        }
    }
}