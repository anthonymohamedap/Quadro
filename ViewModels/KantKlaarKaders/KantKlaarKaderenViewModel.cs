using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>US-58 — klein, zelfstandig beheerscherm voor kant-en-klare kaders (naam + afmeting +
/// stukprijs). Bewust géén kopie van de volledige complexiteit van <see cref="LijstenViewModel"/>
/// (leverancier-koppeling, Excel-import, bulk-prijsupdate) — dat past niet bij dit simpele model.</summary>
public partial class KantKlaarKaderenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<KantKlaarKader> kaders = new();
    [ObservableProperty] private ObservableCollection<KantKlaarKader> gefilterdeKaders = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private KantKlaarKader? geselecteerdeKader;

    [ObservableProperty] private bool isDetailOpen;
    [ObservableProperty] private string zoekterm = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    public int AantalKaders => GefilterdeKaders?.Count ?? 0;

    public Action? OnTerug { get; set; }

    public KantKlaarKaderenViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        INavigationService nav,
        IDialogService dialogs,
        IToastService toast)
    {
        _dbFactory = dbFactory;
        _nav = nav;
        _dialogs = dialogs;
        _toast = toast;
    }

    public async Task InitializeAsync() => await LoadAsync();

    partial void OnZoektermChanged(string value) => ApplyFilter();

    partial void OnGeselecteerdeKaderChanged(KantKlaarKader? value) => IsDetailOpen = value is not null;

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        if (OnTerug is not null)
            OnTerug();
        else
            await _nav.NavigateToAsync<HomeViewModel>();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var data = await db.KantKlaarKaders
                .AsNoTracking()
                .OrderBy(k => k.Naam)
                .ToListAsync();

            Kaders = new ObservableCollection<KantKlaarKader>(data);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Foutmelding = $"Fout bij laden kant-en-klare kaders: {ex.Message}";
            await _dialogs.ShowErrorAsync("Laden mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void Nieuw()
    {
        GeselecteerdeKader = new KantKlaarKader();
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (GeselecteerdeKader is null) return;

        GeselecteerdeKader.Naam = (GeselecteerdeKader.Naam ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(GeselecteerdeKader.Naam))
        {
            _toast.Error("Naam is verplicht.");
            return;
        }
        if (GeselecteerdeKader.PrijsPerStukExcl < 0m)
        {
            _toast.Error("Prijs per stuk kan niet negatief zijn.");
            return;
        }

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (GeselecteerdeKader.Id == 0)
            {
                db.KantKlaarKaders.Add(GeselecteerdeKader);
            }
            else
            {
                db.KantKlaarKaders.Attach(GeselecteerdeKader);
                db.Entry(GeselecteerdeKader).State = EntityState.Modified;
            }

            await db.SaveChangesAsync();

            _toast.Success("Kant-en-klaar kader opgeslagen.");
            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeKader = null;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Foutmelding = $"Opslaan mislukt: {msg}";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete() => GeselecteerdeKader is not null && GeselecteerdeKader.Id != 0;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (GeselecteerdeKader is null) return;

        var ok = await _dialogs.ConfirmAsync(
            "Kant-en-klaar kader archiveren",
            $"Ben je zeker dat je '{GeselecteerdeKader.Naam}' wil archiveren?\n\n" +
            "Het kader wordt verborgen maar bestaande offerteregels blijven intact.");

        if (!ok) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var dbKader = await db.KantKlaarKaders.FindAsync(GeselecteerdeKader.Id);
            if (dbKader is null) return;

            dbKader.IsGearchiveerd = true;
            await db.SaveChangesAsync();

            await LoadAsync();

            IsDetailOpen = false;
            GeselecteerdeKader = null;
        }
        catch (Exception ex)
        {
            Foutmelding = $"Archiveren mislukt: {ex.Message}";
            await _dialogs.ShowErrorAsync("Archiveren mislukt", Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        if (Kaders.Count == 0)
        {
            GefilterdeKaders = new ObservableCollection<KantKlaarKader>();
            OnPropertyChanged(nameof(AantalKaders));
            return;
        }

        if (string.IsNullOrWhiteSpace(Zoekterm))
        {
            GefilterdeKaders = new ObservableCollection<KantKlaarKader>(Kaders);
            OnPropertyChanged(nameof(AantalKaders));
            return;
        }

        var term = Zoekterm.Trim().ToLowerInvariant();
        var filtered = Kaders.Where(k => (k.Naam ?? string.Empty).ToLowerInvariant().Contains(term));

        GefilterdeKaders = new ObservableCollection<KantKlaarKader>(filtered);
        OnPropertyChanged(nameof(AantalKaders));
    }
}
