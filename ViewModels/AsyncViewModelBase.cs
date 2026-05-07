using CommunityToolkit.Mvvm.ComponentModel;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public abstract class AsyncViewModelBase : ObservableObject
{
    protected readonly IToastService Toast;

    protected AsyncViewModelBase(IToastService toast)
    {
        Toast = toast ?? throw new ArgumentNullException(nameof(toast));
    }

    /// <summary>
    /// Start een async taak veilig vanuit een synchrone context (property-setter, OnXChanged).
    /// Exceptions worden via Toast gerapporteerd in plaats van stilletjes genegeerd.
    /// </summary>
    protected void RunAsync(Func<Task> taskFactory)
    {
        taskFactory().ContinueWith(
            t => Toast.Error(t.Exception!.GetBaseException().Message),
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
