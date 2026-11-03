using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>
/// US-92 — de planning als pagina in het hoofdvenster (zijbalk "Planning"), in plaats van een los
/// venster dat achter de app kan verdwijnen. Zelfde kalender en functies als het venster.
/// </summary>
public partial class PlanningPaginaViewModel : IAsyncInitializable
{
    private readonly INavigationService _nav;

    public PlanningPaginaViewModel(
        IDbContextFactory<AppDbContext> factory,
        IWerkBonWorkflowService werkBonWorkflow,
        IToastService toast,
        IWorkflowService statusWorkflow,
        INavigationService nav)
    {
        _nav = nav;
        Kalender = new PlanningCalendarViewModel(factory, werkBonWorkflow, toast, statusWorkflow);
    }

    public PlanningCalendarViewModel Kalender { get; }

    public Task InitializeAsync() => Kalender.InitializeGlobalAsync();

    [RelayCommand]
    private Task GaTerugAsync() => _nav.NavigateToAsync<HomeViewModel>();
}
