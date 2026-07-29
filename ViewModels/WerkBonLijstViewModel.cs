using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
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
        [ObservableProperty] private string? zoekterm;

        // ── Jaar-filter ───────────────────────────────────────────────────────
        public ObservableCollection<int> BeschikbareJaren { get; } = new();
        [ObservableProperty] private int geselecteerdJaar = 0;

        // ── US-51: status-filter (naast het jaarfilter) ────────────────────────
        public ObservableCollection<WerkBonStatusFilterOptie> StatusFilterOpties { get; } = new()
        {
            new WerkBonStatusFilterOptie("Alle statussen", null),
            new WerkBonStatusFilterOptie("Gepland",        WerkBonStatus.Gepland),
            new WerkBonStatusFilterOptie("In uitvoering",  WerkBonStatus.InUitvoering),
            new WerkBonStatusFilterOptie("Afgewerkt",      WerkBonStatus.Afgewerkt),
            new WerkBonStatusFilterOptie("Afgehaald",      WerkBonStatus.Afgehaald),
        };
        [ObservableProperty] private WerkBonStatusFilterOptie? geselecteerdeStatusFilter;

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

            // US-51: standaard "Alle statussen" (veld direct zetten zodat LoadAsync niet
            // al vanuit de constructor afvuurt).
            geselecteerdeStatusFilter = StatusFilterOpties[0];
        }

        public async Task LoadAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();

            // De lijst toont per werkbon enkel kern-info + afgeleide tellers (aantal taken,
            // bestel-voortgang); het volledige detail (taken/lijst/afwerking) zit in het
            // modale overzichtsvenster. Daarom hier alleen Offerte.Klant + Taken laden.
            var query = db.WerkBonnen
                .Include(w => w.Offerte).ThenInclude(o => o.Klant)
                .Include(w => w.Taken)
                .AsQueryable();

            // Jaar-filter
            if (GeselecteerdJaar > 0)
                query = query.Where(w => w.AangemaaktOp.Year == GeselecteerdJaar);

            // US-51: status-filter
            if (GeselecteerdeStatusFilter?.Status is WerkBonStatus statusFilter)
                query = query.Where(w => w.Status == statusFilter);

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
        partial void OnGeselecteerdeStatusFilterChanged(WerkBonStatusFilterOptie? value) => RunAsync(LoadAsync);

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

            // Na plannen kan de status gewijzigd zijn (offerte → InProductie) → lijst verversen.
            var bewaardeId = SelectedWerkBon?.Id;
            await LoadAsync();
            if (bewaardeId.HasValue)
                SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == bewaardeId.Value);
        }

        /// <summary>US-51 Fase B — opent het volledige werkbon-overzicht in een modaal venster.</summary>
        [RelayCommand]
        private async Task OpenWerkBonDetailAsync(WerkBon? werkBon)
        {
            var wb = werkBon ?? SelectedWerkBon;
            if (wb is null)
                return;

            SelectedWerkBon = wb; // markeer de rij als geselecteerd

            var vm = new WerkBonDetailViewModel(_factory, _statusWorkflow, _offerteNav, _toast, wb.Id);
            await vm.LoadAsync();

            var window = new QuadroApp.Views.WerkBonDetailWindow { DataContext = vm };

            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var owner = desktop.MainWindow;
                if (owner is null)
                    return;

                await window.ShowDialog(owner);
            }

            // Status/bestellingen kunnen in het venster gewijzigd zijn → lijst verversen.
            var bewaardeId = wb.Id;
            await LoadAsync();
            SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == bewaardeId);
        }

        [RelayCommand]
        private async Task GaTerugAsync()
        {
            await _nav.NavigateToAsync<HomeViewModel>();
        }
    }

    /// <summary>US-51 — één keuze in het status-filter; <c>Status = null</c> betekent "alle statussen".</summary>
    public sealed record WerkBonStatusFilterOptie(string Label, WerkBonStatus? Status);
}
