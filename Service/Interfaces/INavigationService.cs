using System;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces
{
    public interface INavigationService
    {
        object? CurrentViewModel { get; }
        event Action<object?>? CurrentViewModelChanged;

        Task NavigateToAsync<TViewModel>()
            where TViewModel : class;

        Task NavigateToAsync(object viewModel);

        Task NavigateToAsync<TViewModel, TParam>(TParam parameter)
            where TViewModel : class;

        Task NavigateToKlantDetailAsync(int klantId);
    }
}
