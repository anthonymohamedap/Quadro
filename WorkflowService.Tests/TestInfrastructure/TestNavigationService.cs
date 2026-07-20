using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;

namespace WorkflowService.Tests.TestInfrastructure;

public sealed class TestNavigationService : INavigationService
{
    public object? CurrentViewModel { get; private set; }
    public event Action<object?>? CurrentViewModelChanged;

    public Type? LastViewModelType { get; private set; }
    public object? LastParameter { get; private set; }
    public int? LastKlantId { get; private set; }

    public Task NavigateToAsync<TViewModel>() where TViewModel : class
    {
        LastViewModelType = typeof(TViewModel);
        CurrentViewModel = typeof(TViewModel);
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToAsync(object viewModel)
    {
        CurrentViewModel = viewModel;
        LastViewModelType = viewModel.GetType();
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToAsync<TViewModel, TParam>(TParam parameter) where TViewModel : class
    {
        LastViewModelType = typeof(TViewModel);
        LastParameter = parameter;
        CurrentViewModel = typeof(TViewModel);
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToKlantDetailAsync(int klantId)
    {
        LastKlantId = klantId;
        CurrentViewModel = klantId;
        CurrentViewModelChanged?.Invoke(CurrentViewModel);
        return Task.CompletedTask;
    }
}
