using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels
{
    /// <summary>
    /// US-51 Fase B — modaal werkbon-overzicht. Toont alle info van één werkbon overzichtelijk
    /// (kop, planning-samenvatting, taken met bestel-/voorraadstatus, gekoppelde bestelbon) en
    /// biedt de acties status wijzigen/opslaan, per-lijst bestellen en — als laatste stap —
    /// de gekoppelde offerte openen. Hergebruikt dezelfde services als de werkbonnenlijst.
    /// </summary>
    public partial class WerkBonDetailViewModel : ObservableObject
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IWorkflowService _statusWorkflow;
        private readonly IOfferteNavigationService _offerteNav;
        private readonly IToastService _toast;
        private readonly int _werkBonId;

        public WerkBonDetailViewModel(
            IDbContextFactory<AppDbContext> factory,
            IWorkflowService statusWorkflow,
            IOfferteNavigationService offerteNav,
            IToastService toast,
            int werkBonId)
        {
            _factory = factory;
            _statusWorkflow = statusWorkflow;
            _offerteNav = offerteNav;
            _toast = toast;
            _werkBonId = werkBonId;
        }

        /// <summary>Wordt door het venster gezet zodat 'Sluiten' het venster kan afsluiten.</summary>
        public Action? RequestClose { get; set; }

        [ObservableProperty] private WerkBon? werkBon;
        [ObservableProperty] private ObservableCollection<WerkTaak> taken = new();
        [ObservableProperty] private Factuur? bestelbon;

        [ObservableProperty] private string kopTitel = "Werkbon";
        [ObservableProperty] private string klantNaam = "";
        [ObservableProperty] private string totaalLabel = "";
        [ObservableProperty] private string aangemaaktLabel = "";
        [ObservableProperty] private string afhaalLabel = "";
        [ObservableProperty] private string planningSamenvatting = "";

        public ObservableCollection<WerkBonStatus> StatusOpties { get; } =
            new(Enum.GetValues<WerkBonStatus>());

        [ObservableProperty] private WerkBonStatus? geselecteerdeStatus;

        public bool HeeftBestelbon => Bestelbon is not null;
        public bool HeeftAfhaalDatum => WerkBon?.AfhaalDatum is not null;

        partial void OnBestelbonChanged(Factuur? value) => OnPropertyChanged(nameof(HeeftBestelbon));
        partial void OnWerkBonChanged(WerkBon? value) => OnPropertyChanged(nameof(HeeftAfhaalDatum));

        public async Task LoadAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();

            var wb = await db.WerkBonnen
                .Include(w => w.Offerte).ThenInclude(o => o.Klant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.TypeLijst)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Glas)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout1)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout2)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.DiepteKern)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Opkleven)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Rug)
                .FirstOrDefaultAsync(w => w.Id == _werkBonId);

            if (wb is null)
            {
                _toast.Error("Werkbon niet gevonden.");
                return;
            }

            WerkBon = wb;
            GeselecteerdeStatus = wb.Status;
            Taken = new ObservableCollection<WerkTaak>(wb.Taken.OrderBy(t => t.GeplandVan));

            Bestelbon = await db.Facturen
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.WerkBonId == _werkBonId);

            // Kop-labels
            KopTitel = $"Werkbon #{wb.Id}";
            KlantNaam = wb.Offerte?.Klant is { } k ? $"{k.Voornaam} {k.Achternaam}".Trim() : "—";
            TotaalLabel = wb.TotaalPrijsIncl.ToString("C2", new System.Globalization.CultureInfo("nl-BE"));
            AangemaaktLabel = $"Aangemaakt {wb.AangemaaktOp:dd/MM/yyyy}";
            AfhaalLabel = wb.AfhaalDatum is DateTime d ? $"Ophalen {d:dd/MM/yyyy}" : "";

            // Planning-samenvatting
            var totMin = wb.Taken.Sum(t => t.DuurMinuten);
            if (wb.Taken.Count == 0)
            {
                PlanningSamenvatting = "Nog geen taken gepland.";
            }
            else
            {
                var van = wb.Taken.Min(t => t.GeplandVan);
                var tot = wb.Taken.Max(t => t.GeplandTot);
                PlanningSamenvatting =
                    $"{wb.Taken.Count} taken · {totMin / 60}u {totMin % 60}m · {van:dd/MM} – {tot:dd/MM}";
            }
        }

        [RelayCommand]
        private async Task SaveStatusAsync()
        {
            if (WerkBon is null || GeselecteerdeStatus is null || GeselecteerdeStatus == WerkBon.Status)
                return;

            try
            {
                await _statusWorkflow.ChangeWerkBonStatusAsync(WerkBon.Id, GeselecteerdeStatus.Value);
            }
            catch (InvalidOperationException ex)
            {
                _toast.Warning(ex.Message);
                return;
            }

            _toast.Success("Werkbonstatus bijgewerkt.");
            await LoadAsync();
        }

        [RelayCommand]
        private async Task MarkeerLijstAlsBesteldAsync(WerkTaak? taak)
        {
            if (taak is null) return;

            try
            {
                await _statusWorkflow.MarkLijstAsBesteldAsync(taak.Id, DateTime.Today, taak.GeselecteerdeBestelVorm);
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

            _toast.Success("Bestelling geplaatst.");
            await LoadAsync();
        }

        [RelayCommand]
        private async Task OpenOfferteAsync()
        {
            var offerteId = WerkBon?.Offerte?.Id ?? 0;
            if (offerteId == 0)
            {
                _toast.Error("Geen gekoppelde offerte gevonden.");
                return;
            }

            RequestClose?.Invoke();
            await _offerteNav.OpenOfferteAsync(offerteId);
        }

        [RelayCommand]
        private void Sluit() => RequestClose?.Invoke();
    }
}
