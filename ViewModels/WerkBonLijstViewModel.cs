using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels
{
    public partial class WerkBonLijstViewModel : AsyncViewModelBase
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly INavigationService _nav;
        private readonly IOfferteNavigationService _offerteNav;
        private readonly IWerkBonWorkflowService _workflow;
        private readonly IWorkflowService _statusWorkflow;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<WerkBon> werkBonnen = new();
        [ObservableProperty] private WerkBon? selectedWerkBon;

        [ObservableProperty] private ObservableCollection<WerkTaak> selectedWerkBonTaken = new();

        [ObservableProperty] private string? zoekterm;

        [ObservableProperty] private bool isDetailOpen;

        [ObservableProperty] private DateTimeOffset? geselecteerdeBestelDatum = DateTimeOffset.Now.Date;

        // ── Jaar-filter ───────────────────────────────────────────────────────
        public ObservableCollection<int> BeschikbareJaren { get; } = new();
        [ObservableProperty] private int geselecteerdJaar = 0;

        // Dropdown data
        public ObservableCollection<WerkBonStatus> WerkBonStatusOpties { get; } =
            new ObservableCollection<WerkBonStatus>(Enum.GetValues<WerkBonStatus>());

        // gekozen statuses in UI
        [ObservableProperty] private WerkBonStatus? selectedWerkBonStatus;

        public WerkBonLijstViewModel(
            IDbContextFactory<AppDbContext> factory,
            INavigationService nav,
            IOfferteNavigationService offerteNav,
            IWerkBonWorkflowService workflow,
            IWorkflowService statusWorkflow,
            IToastService toast)
            : base(toast)
        {
            _factory = factory;
            _nav = nav;
            _offerteNav = offerteNav;
            _toast = toast;
            _workflow = workflow;
            _statusWorkflow = statusWorkflow;
        }

        public async Task LoadAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();

            var query = db.WerkBonnen
                .Include(w => w.Offerte).ThenInclude(o => o.Klant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.TypeLijst)
                .AsQueryable();

            // Jaar-filter
            if (GeselecteerdJaar > 0)
                query = query.Where(w => w.AangemaaktOp.Year == GeselecteerdJaar);

            if (!string.IsNullOrWhiteSpace(Zoekterm))
            {
                var t = Zoekterm.Trim().ToLowerInvariant();
                query = query.Where(w =>
                    w.Id.ToString().Contains(t) ||
                    (w.Offerte != null &&
                     w.Offerte.Klant != null &&
                     (w.Offerte.Klant.Achternaam.ToLower().Contains(t) ||
                      w.Offerte.Klant.Voornaam.ToLower().Contains(t))));
            }

            var list = await query
                .OrderByDescending(w => w.AangemaaktOp)
                .ToListAsync();

            // Bouw jaar-dropdown (uit alle werkbonnen, niet gefilterd op jaar)
            var alleJaren = await db.WerkBonnen
                .Select(w => w.AangemaaktOp.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            BeschikbareJaren.Clear();
            BeschikbareJaren.Add(0); // "Alle jaren"
            foreach (var j in alleJaren) BeschikbareJaren.Add(j);

            WerkBonnen = new ObservableCollection<WerkBon>(list);

            // behoud selectie als mogelijk
            if (SelectedWerkBon != null)
                SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == SelectedWerkBon.Id);
        }

        partial void OnZoektermChanged(string? value) => RunAsync(LoadAsync);
        partial void OnGeselecteerdJaarChanged(int value) => RunAsync(LoadAsync);

        partial void OnSelectedWerkBonChanged(WerkBon? value)
        {
            if (value is null)
            {
                IsDetailOpen = false;
                SelectedWerkBonTaken = new ObservableCollection<WerkTaak>();
                SelectedWerkBonStatus = null;
                return;
            }

            IsDetailOpen = true;

            SelectedWerkBonTaken = new ObservableCollection<WerkTaak>(
                (value.Taken ?? Enumerable.Empty<WerkTaak>()).OrderBy(t => t.GeplandVan)
            );

            SelectedWerkBonStatus = value.Status;
            GeselecteerdeBestelDatum = DateTimeOffset.Now.Date;
        }

        [RelayCommand]
        private async Task RefreshAsync() => await LoadAsync();

        [RelayCommand]
        private async Task OpenPlanningAsync()
        {
            if (SelectedWerkBon == null)
                return;

            var vm = new PlanningCalendarViewModel(_factory, _workflow, _toast, _statusWorkflow);
            await vm.InitializeAsync(SelectedWerkBon.Id);

            var window = new QuadroApp.Views.PlanningCalendarWindow
            {
                DataContext = vm
            };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null)
                    return;

                await window.ShowDialog(owner);
            }
        }

        /// <summary>
        /// Save status changes:
        /// - WerkBon.Status aanpassen
        /// - Offerte.Status aanpassen (belangrijk: als terug naar Nieuw => terug zichtbaar in OffertesLijst)
        /// </summary>
        [RelayCommand]
        private async Task SaveStatusAsync()
        {
            if (SelectedWerkBon == null)
                return;

            var wasAfgewerkt = SelectedWerkBon.Status == WerkBonStatus.Afgewerkt;
            var wordtAfgewerkt = SelectedWerkBonStatus == WerkBonStatus.Afgewerkt;

            if (SelectedWerkBonStatus.HasValue && SelectedWerkBonStatus.Value != SelectedWerkBon.Status)
                await _statusWorkflow.ChangeWerkBonStatusAsync(SelectedWerkBon.Id, SelectedWerkBonStatus.Value);

            var selectedWerkBonId = SelectedWerkBon.Id;

            await LoadAsync();

            // reselect
            SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == selectedWerkBonId);

            if (!wasAfgewerkt && wordtAfgewerkt)
            {
                _toast.Success("Werkbon afgewerkt: bestelbon/factuur werd automatisch aangemaakt.");
                await _nav.NavigateToAsync<FacturenViewModel>();
            }
        }

        [RelayCommand]
        private async Task MarkeerLijstAlsBesteldAsync(WerkTaak? taak)
        {
            if (taak is null)
                return;

            try
            {
                var bestelDatum = DateTime.Today;
                await _statusWorkflow.MarkLijstAsBesteldAsync(taak.Id, bestelDatum);
            }
            catch (ValidationException ex)
            {
                _toast.Error(ex.Message);
                return;
            }
            catch (InvalidOperationException ex)
            {
                _toast.Error(ex.Message);
                return;
            }

            var selectedWerkBonId = SelectedWerkBon?.Id;
            await LoadAsync();

            if (selectedWerkBonId.HasValue)
                SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == selectedWerkBonId.Value);
        }

        /// <summary>Expliciet selecteren via de 'Bekijk'-knop in de rij.</summary>
        [RelayCommand]
        private void SelecteerWerkBon(WerkBon? werkBon)
        {
            SelectedWerkBon = werkBon;
        }

        /// <summary>Sluit het detailpaneel zonder de selectie te bewaren.</summary>
        [RelayCommand]
        private void SluitDetail()
        {
            SelectedWerkBon = null;
        }

        [RelayCommand]
        private async Task OpenBestelBonAsync()
        {
            var offerteId = SelectedWerkBon?.Offerte?.Id ?? 0;
            if (offerteId == 0)
            {
                _toast.Error("Geen gekoppelde offerte gevonden.");
                return;
            }

            await _offerteNav.OpenOfferteAsync(offerteId);
        }

        [RelayCommand]
        private async Task GaTerugAsync()
        {
            await _nav.NavigateToAsync<HomeViewModel>();
        }
    }
}
