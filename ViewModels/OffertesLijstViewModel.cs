using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Validation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

public partial class OffertesLijstViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOfferteNavigationService _offerteNav;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _nav;

    public ObservableCollection<Offerte> Offertes { get; } = new();
    public ObservableCollection<Offerte> FilteredOffertes { get; } = new();

    [ObservableProperty] private Offerte? selectedOfferte;
    [ObservableProperty] private string? zoekterm;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    private readonly IOfferteValidator _validator;
    private readonly IToastService _toast;

    public OffertesLijstViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        IOfferteNavigationService offerteNav,
        IDialogService dialogs,
        INavigationService nav,
        IOfferteValidator validator,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _offerteNav = offerteNav;
        _dialogs = dialogs;
        _nav = nav;
        _validator = validator;
        _toast = toast;
    }


    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string? value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var list = await db.Offertes
    .Include(o => o.Klant)
    .Where(o => o.Status == OfferteStatus.Nieuw)
    .OrderByDescending(o => o.Datum)
    .ToListAsync();

            Offertes.Clear();
            foreach (var o in list) Offertes.Add(o);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        FilteredOffertes.Clear();
        var q = Offertes.AsEnumerable();

        var term = Zoekterm?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            q = q.Where(o =>
                o.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (o.Klant != null &&
                 (($"{o.Klant.Voornaam} {o.Klant.Achternaam}")
                    .Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        foreach (var o in q) FilteredOffertes.Add(o);
    }

    [RelayCommand]
    private async Task NewAsync() => await _offerteNav.NewOfferteAsync();

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (SelectedOfferte is null) return;
        await _offerteNav.OpenOfferteAsync(SelectedOfferte.Id);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedOfferte is null) return;

        var ok = await _dialogs.ConfirmAsync(
            "Offerte verwijderen",
            $"Ben je zeker dat je offerte {SelectedOfferte.Id} wil verwijderen?");

        if (!ok) return;

        // ✅ validate delete (db-safe)
        var vr = await _validator.ValidateDeleteAsync(new Offerte { Id = SelectedOfferte.Id });

        var warn = vr.WarningText();
        if (!string.IsNullOrWhiteSpace(warn))
            _toast.Warning(warn);

        if (!vr.IsValid)
        {
            _toast.Error(vr.ErrorText());
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Offertes.Remove(new Offerte { Id = SelectedOfferte.Id });
        await db.SaveChangesAsync();

        _toast.Success("Offerte verwijderd");
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GaTerugAsync() => await _nav.NavigateToAsync<HomeViewModel>();
}
