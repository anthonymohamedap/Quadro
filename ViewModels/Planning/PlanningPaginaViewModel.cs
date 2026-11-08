using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Service.Interfaces;
using System;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>
/// US-92 — de planning als pagina in het hoofdvenster, in plaats van een los venster dat achter de
/// app kan verdwijnen. Zelfde kalender en functies als het vroegere venster. Vanuit een werkbon
/// (offerte, Werkbonnen, Verkopen, dossier) opent hij met de inlijstingen van die werkbon, en
/// "Terug" gaat naar het scherm waar je vandaan kwam.
/// </summary>
public partial class PlanningPaginaViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IAsyncInitializable
{
    private readonly INavigationService _nav;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PlanningPaginaViewModel(
        IDbContextFactory<AppDbContext> factory,
        IWerkBonWorkflowService werkBonWorkflow,
        IToastService toast,
        IWorkflowService statusWorkflow,
        INavigationService nav)
    {
        _nav = nav;
        _factory = factory;
        Kalender = new PlanningCalendarViewModel(factory, werkBonWorkflow, toast, statusWorkflow);
    }

    public PlanningCalendarViewModel Kalender { get; }

    /// <summary>US-92 — dezelfde opmaak als stap 2 van het dossier (2 weken/maand, cijfers, dagdetails, weekdetail, blokkeren).</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private WerkbonPlannerViewModel? planner;

    /// <summary>Werkbon waarvan de inlijstingen gepland worden; null = algemene planning (zijbalk).</summary>
    public int? WerkBonId { get; set; }

    /// <summary>Waar "Terug" naartoe gaat; null = beginscherm.</summary>
    public Func<Task>? Terug { get; set; }

    public async Task InitializeAsync()
    {
        if (WerkBonId is int id) await Kalender.InitializeAsync(id);
        else await Kalender.InitializeGlobalAsync();
        var planner = new WerkbonPlannerViewModel(Kalender, _factory, WerkBonId);
        await planner.LaadAsync();
        Planner = planner;
    }

    [RelayCommand]
    private Task GaTerugAsync() => Terug is not null ? Terug() : _nav.NavigateToAsync<HomeViewModel>();

    /// <summary>Opent de planningspagina voor een werkbon; "Terug" voert <paramref name="terug"/> uit.</summary>
    public static Task ToonAsync(
        INavigationService nav, IDbContextFactory<AppDbContext> factory, IWerkBonWorkflowService werkBonWorkflow,
        IToastService toast, IWorkflowService statusWorkflow, int? werkBonId, Func<Task> terug)
    {
        var vm = new PlanningPaginaViewModel(factory, werkBonWorkflow, toast, statusWorkflow, nav)
        {
            WerkBonId = werkBonId,
            Terug = terug
        };
        return nav.NavigateToAsync(vm);   // roept InitializeAsync op
    }
}
