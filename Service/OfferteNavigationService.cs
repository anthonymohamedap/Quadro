
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

    /// <summary>US-95 — de verkoop openen als dossier, op de stap waar hij staat.</summary>
    public Task OpenVerkoopAsync(int offerteId)
        => _nav.NavigateToAsync<QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel, int>(offerteId);

    /// <summary>Fix 08/11 — nieuwe verkoop = dossier met OfferteId 0 (stap 1, nog niet bewaard).</summary>
    public Task NieuweVerkoopAsync()
        => _nav.NavigateToAsync<QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel, int>(0);

    public async Task NewOfferteAsync()
    {
        var vm = _sp.GetRequiredService<OfferteViewModel>();
        await vm.InitializeAsync();
        await vm.NewAsync();
        await _nav.NavigateToAsync(vm);
    }
}
