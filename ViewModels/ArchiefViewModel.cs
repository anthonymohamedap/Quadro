using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Model.Snapshot;
using QuadroApp.Service.Interfaces;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels
{
    public partial class ArchiefViewModel : AsyncViewModelBase, IAsyncInitializable
    {
        private readonly IOfferteArchiefService _archief;
        private readonly INavigationService _nav;
        private readonly IDialogService _dialogs;
        private readonly IToastService _toast;

        // ── Lijst ────────────────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<OfferteArchief> archiefItems = new();
        [ObservableProperty] private OfferteArchief? geselecteerdItem;
        [ObservableProperty] private string? zoekterm;
        [ObservableProperty] private bool isBezig;

        // ── Jaar-filter ───────────────────────────────────────────────────────
        public ObservableCollection<int> BeschikbareJaren { get; } = new();
        [ObservableProperty] private int geselecteerdJaar = 0;

        // ── Detail-venster ────────────────────────────────────────────────────
        [ObservableProperty] private bool isDetailOpen;
        [ObservableProperty] private OfferteArchiefSnapshot? geselecteerdSnapshot;

        public IAsyncRelayCommand<OfferteArchief?> HerstellenCommand  { get; }
        public IAsyncRelayCommand<OfferteArchief?> VerwijderenCommand { get; }
        public IRelayCommand SluitDetailCommand { get; }

        public ArchiefViewModel(
            IOfferteArchiefService archief,
            INavigationService nav,
            IDialogService dialogs,
            IToastService toast)
            : base(toast)
        {
            _archief  = archief;
            _nav      = nav;
            _dialogs  = dialogs;
            _toast    = toast;

            HerstellenCommand  = new AsyncRelayCommand<OfferteArchief?>(HerstellenAsync,
                a => a is not null && !a.IsHersteld);
            VerwijderenCommand = new AsyncRelayCommand<OfferteArchief?>(VerwijderenAsync,
                a => a is not null);
            SluitDetailCommand = new RelayCommand(() => { IsDetailOpen = false; GeselecteerdItem = null; });
        }

        public async Task InitializeAsync() => await LaadAsync();

        // ── Laden ─────────────────────────────────────────────────────────────

        private async Task LaadAsync()
        {
            IsBezig = true;
            try
            {
                // Jaar-opties ophalen
                var jaren = await _archief.GetJarenAsync();
                BeschikbareJaren.Clear();
                BeschikbareJaren.Add(0);
                foreach (var j in jaren) BeschikbareJaren.Add(j);

                // Items laden (gefilterd op jaar in de service)
                var alle = await _archief.GetAlleAsync(GeselecteerdJaar);

                // Zoekterm client-side
                var zoek = Zoekterm?.Trim();
                if (!string.IsNullOrWhiteSpace(zoek))
                    alle = alle.FindAll(a =>
                        a.KlantNaam.Contains(zoek, StringComparison.OrdinalIgnoreCase) ||
                        a.OrigineleOfferteId.ToString().Contains(zoek) ||
                        (a.Reden?.Contains(zoek, StringComparison.OrdinalIgnoreCase) ?? false));

                ArchiefItems = new ObservableCollection<OfferteArchief>(alle);
            }
            catch (Exception ex)
            {
                _toast.Error($"Kon archief niet laden: {ex.Message}");
            }
            finally { IsBezig = false; }
        }

        partial void OnZoektermChanged(string? value)         => RunAsync(LaadAsync);
        partial void OnGeselecteerdJaarChanged(int value)     => RunAsync(LaadAsync);

        // ── Selectie ──────────────────────────────────────────────────────────

        partial void OnGeselecteerdItemChanged(OfferteArchief? value)
        {
            GeselecteerdSnapshot = null;
            IsDetailOpen = value is not null;
            HerstellenCommand.NotifyCanExecuteChanged();
            VerwijderenCommand.NotifyCanExecuteChanged();

            if (value is not null)
                RunAsync(() => LaadSnapshotAsync(value));
        }

        private async Task LaadSnapshotAsync(OfferteArchief item)
        {
            try
            {
                GeselecteerdSnapshot = await _archief.GetSnapshotAsync(item.Id);
            }
            catch (Exception ex)
            {
                _toast.Error($"Snapshot kon niet geladen worden: {ex.Message}");
            }
        }

        // ── Herstellen ────────────────────────────────────────────────────────

        private async Task HerstellenAsync(OfferteArchief? item)
        {
            if (item is null || item.IsHersteld) return;

            IsBezig = true;
            try
            {
                var nieuweId = await _archief.HerstellenAsync(item.Id);
                _toast.Success($"Hersteld als nieuwe offerte #{nieuweId}.");

                IsDetailOpen = false;
                GeselecteerdItem = null;
                GeselecteerdSnapshot = null;

                await LaadAsync();

                // Navigeer naar offertelijst
                await _nav.NavigateToAsync<OffertesLijstViewModel>();
            }
            catch (Exception ex)
            {
                _toast.Error($"Herstellen mislukt: {ex.Message}");
            }
            finally { IsBezig = false; }
        }

        // ── Permanent verwijderen ─────────────────────────────────────────────

        private async Task VerwijderenAsync(OfferteArchief? item)
        {
            if (item is null) return;

            var ok = await _dialogs.ConfirmAsync(
                "Permanent verwijderen",
                $"Archief-entry #{item.Id} ({item.KlantNaam}) permanent verwijderen? " +
                $"Dit kan NIET ongedaan gemaakt worden.");
            if (!ok) return;

            IsBezig = true;
            try
            {
                await _archief.VerwijderenAsync(item.Id);
                _toast.Success($"Archief-entry #{item.Id} permanent verwijderd.");

                IsDetailOpen = false;
                GeselecteerdItem = null;
                GeselecteerdSnapshot = null;

                await LaadAsync();
            }
            catch (Exception ex)
            {
                _toast.Error($"Verwijderen mislukt: {ex.Message}");
            }
            finally { IsBezig = false; }
        }

        // ── Terug ─────────────────────────────────────────────────────────────

        [RelayCommand]
        private async Task GaTerugAsync() => await _nav.NavigateToAsync<OffertesLijstViewModel>();
    }
}
