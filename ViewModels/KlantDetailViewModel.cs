using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class KlantDetailViewModel : ObservableObject, IAsyncInitializable, IParameterReceiver<int>
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IOfferteNavigationService _offerteNav;

    private int? _klantId;

    [ObservableProperty] private Klant? klant;
    [ObservableProperty] private ObservableCollection<Offerte> offertes = new();
    [ObservableProperty] private ObservableCollection<WerkBon> werkBonnen = new();
    [ObservableProperty] private Offerte? selectedOfferte;
    [ObservableProperty] private WerkBon? selectedWerkBon;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    public KlantDetailViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IOfferteNavigationService offerteNav)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        _offerteNav = offerteNav ?? throw new ArgumentNullException(nameof(offerteNav));
    }

    public Task ReceiveAsync(int parameter)
    {
        _klantId = parameter;
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        if (_klantId is int klantId)
            await LoadAsync(klantId);
    }

    public async Task LoadAsync(int klantId)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var loadedKlant = await db.Klanten
                .AsNoTracking()
                .Include(k => k.Offertes)
                    .ThenInclude(o => o.WerkBon)
                .FirstOrDefaultAsync(k => k.Id == klantId);

            if (loadedKlant is null)
            {
                Klant = null;
                Offertes = new ObservableCollection<Offerte>();
                WerkBonnen = new ObservableCollection<WerkBon>();
                Foutmelding = "Klant niet gevonden.";
                return;
            }

            Klant = loadedKlant;

            var sortedOffertes = loadedKlant.Offertes
                .OrderByDescending(o => o.Datum)
                .ToList();

            Offertes = new ObservableCollection<Offerte>(sortedOffertes);

            WerkBonnen = new ObservableCollection<WerkBon>(
                sortedOffertes
                    .Where(o => o.WerkBon is not null)
                    .Select(o => o.WerkBon!));

            SelectedOfferte = Offertes.FirstOrDefault();
            SelectedWerkBon = WerkBonnen.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Foutmelding = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenOfferteAsync(Offerte? offerte)
    {
        var target = offerte ?? SelectedOfferte;
        if (target is null)
            return;

        await _offerteNav.OpenOfferteAsync(target.Id);
    }

    [RelayCommand]
    private async Task NieuweOfferteAsync()
    {
        await _offerteNav.NewOfferteAsync();
    }

    [RelayCommand]
    private async Task TerugAsync()
    {
        await _nav.NavigateToAsync<KlantenViewModel>();
    }
}
