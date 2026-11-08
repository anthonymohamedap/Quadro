
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

    /// <summary>US-95: het oude offertescherm is weg; een offerte openen = de verkoop in het dossier.</summary>
    public Task OpenOfferteAsync(int offerteId) => OpenVerkoopAsync(offerteId);

    /// <summary>US-95 — de verkoop openen als dossier, op de stap waar hij staat.</summary>
    public Task OpenVerkoopAsync(int offerteId)
        => _nav.NavigateToAsync<QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel, int>(offerteId);

    /// <summary>Fix 08/11 — nieuwe verkoop = dossier met OfferteId 0 (stap 1, nog niet bewaard).</summary>
    public Task NieuweVerkoopAsync()
        => _nav.NavigateToAsync<QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel, int>(0);

    /// <summary>US-95: nieuwe offerte = nieuwe verkoop in het dossier.</summary>
    public Task NewOfferteAsync() => NieuweVerkoopAsync();
}
