
using Microsoft.Extensions.DependencyInjection;
using QuadroApp.Service.Interfaces;
using QuadroApp.ViewModels;
using System;
using System.Threading.Tasks;

namespace QuadroApp.Service;

public sealed class OfferteNavigationService : IOfferteNavigationService
{
    private readonly IServiceProvider _sp;
    private readonly INavigationService _nav;

    public OfferteNavigationService(IServiceProvider sp, INavigationService nav)
    {
        _sp = sp;
        _nav = nav;
    }

    public async Task OpenOfferteAsync(int offerteId)
    {
        var vm = _sp.GetRequiredService<OfferteViewModel>();
        await vm.InitializeAsync();
        await vm.LoadCatalogAsync();
        await vm.LoadAsync(offerteId);
        await _nav.NavigateToAsync(vm); // overload in nav (zie onder)
    }

    public async Task NewOfferteAsync()
    {
        var vm = _sp.GetRequiredService<OfferteViewModel>();
        await vm.InitializeAsync();
        await vm.NewAsync();
        await _nav.NavigateToAsync(vm);
    }
}
