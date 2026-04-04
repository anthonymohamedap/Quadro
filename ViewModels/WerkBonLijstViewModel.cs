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
        private readonly IWerkBonWorkflowService _workflow;
        private readonly IWorkflowService _statusWorkflow;
        private readonly IToastService _toast;

        [ObservableProperty] private ObservableCollection<WerkBon> werkBonnen = new();
        [ObservableProperty] private WerkBon? selectedWerkBon;

        [ObservableProperty] private ObservableCollection<WerkTaak> selectedWerkBonTaken = new();

        [ObservableProperty] private string? zoekterm;

        [ObservableProperty] private bool isDetailOpen;

        [ObservableProperty] private DateTimeOffset? geselecteerdeBestelDatum = DateTimeOffset.Now.Date;

        // Dropdown data
        public ObservableCollection<WerkBonStatus> WerkBonStatusOpties { get; } =
            new ObservableCollection<WerkBonStatus>(Enum.GetValues<WerkBonStatus>());

        // gekozen statuses in UI
        [ObservableProperty] private WerkBonStatus? selectedWerkBonStatus;

        public WerkBonLijstViewModel(
            IDbContextFactory<AppDbContext> factory,
            INavigationService nav,
            IWerkBonWorkflowService workflow,
            IWorkflowService statusWorkflow,
            IToastService toast)
            : base(toast)
        {
            _factory = factory;
            _nav = nav;
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

            WerkBonnen = new ObservableCollection<WerkBon>(list);

            // behoud selectie als mogelijk
            if (SelectedWerkBon != null)
            {
                SelectedWerkBon = WerkBonnen.FirstOrDefault(x => x.Id == SelectedWerkBon.Id);
            }
        }

        partial void OnZoektermChanged(string? value) => RunAsync(LoadAsync);

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
                var bestelDatum = (GeselecteerdeBestelDatum ?? DateTimeOffset.Now.Date).Date;
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

        [RelayCommand]
        private async Task GaTerugAsync()
        {
            // pas aan naar jouw bestemming
            await _nav.NavigateToAsync<HomeViewModel>();
        }
    }
}
