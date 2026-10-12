using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
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
        private readonly IOntvangstService? _ontvangsten;
        private readonly int _werkBonId;

        public WerkBonDetailViewModel(
            IDbContextFactory<AppDbContext> factory,
            IWorkflowService statusWorkflow,
            IOfferteNavigationService offerteNav,
            IToastService toast,
            int werkBonId,
            IOntvangstService? ontvangsten = null)
        {
            _factory = factory;
            _statusWorkflow = statusWorkflow;
            _offerteNav = offerteNav;
            _toast = toast;
            _werkBonId = werkBonId;
            _ontvangsten = ontvangsten;
        }

        // ── US-76: afrekenen bij afhaling ──

        public IReadOnlyList<Betaalwijze> BetaalwijzeOpties { get; } = Enum.GetValues<Betaalwijze>();

        [ObservableProperty] private bool isAfrekenenOpen;
        [ObservableProperty] private AfrekenStand? afrekenStand;
        [ObservableProperty] private decimal? afrekenBedrag;
        [ObservableProperty] private DateTimeOffset? afrekenDatum = DateTimeOffset.Now;
        [ObservableProperty] private Betaalwijze afrekenBetaalwijze = Betaalwijze.Bancontact;

        public decimal AfrekenRest => AfrekenStand?.Stand.Rest ?? 0m;

        /// <summary>Werkbon is al afgehaald maar de bestelbon is nog niet volledig betaald.</summary>
        public bool HeeftOpenstaandeRest => WerkBon?.Status == WerkBonStatus.Afgehaald && AfrekenRest > 0m && !IsAfrekenenOpen;

        public string AfrekenTitel => AfrekenStand is null
            ? string.Empty
            : $"Afrekenen bestelbon {AfrekenStand.FactuurNummer}: nog € {AfrekenRest:0.00} te betalen";

        public string AfrekenDetail => AfrekenStand is null
            ? string.Empty
            : $"Totaal € {AfrekenStand.Stand.TotaalIncl:0.00} · voorschot € {AfrekenStand.Stand.Voorschot:0.00} · al betaald € {AfrekenStand.Stand.Betaald:0.00}";

        partial void OnAfrekenStandChanged(AfrekenStand? value)
        {
            OnPropertyChanged(nameof(AfrekenRest));
            OnPropertyChanged(nameof(AfrekenTitel));
            OnPropertyChanged(nameof(AfrekenDetail));
            OnPropertyChanged(nameof(HeeftOpenstaandeRest));
        }

        partial void OnIsAfrekenenOpenChanged(bool value) => OnPropertyChanged(nameof(HeeftOpenstaandeRest));

        private async Task LaadAfrekenStandAsync()
        {
            AfrekenStand = _ontvangsten is null ? null : await _ontvangsten.GetAfrekenStandAsync(_werkBonId);
        }

        private void OpenAfrekenen()
        {
            AfrekenBedrag = AfrekenRest;
            AfrekenDatum = DateTimeOffset.Now;
            IsAfrekenenOpen = true;
        }

        /// <summary>Betalen: betaling op de bestelbon + werkbon Afgehaald, in één keer. Blijft er nog
        /// een rest (gesplitste betaling, bv. deels cash), dan blijft het blok open met de nieuwe rest.</summary>
        [RelayCommand]
        private async Task BetalenAsync()
        {
            if (_ontvangsten is null || WerkBon is null) return;
            if (AfrekenBedrag is not > 0m)
            {
                _toast.Warning("Geef een bedrag groter dan 0 in.");
                return;
            }

            try
            {
                var bedrag = AfrekenBedrag.Value;
                var nieuw = await _ontvangsten.AfrekenenBijAfhalingAsync(
                    WerkBon.Id, bedrag, (AfrekenDatum ?? DateTimeOffset.Now).Date, AfrekenBetaalwijze);

                await LoadAsync();
                AfrekenStand = nieuw;

                if (nieuw.Stand.Rest > 0m)
                {
                    _toast.Success($"€ {bedrag:0.00} ontvangen — nog € {nieuw.Stand.Rest:0.00} te betalen.");
                    OpenAfrekenen();
                }
                else
                {
                    _toast.Success("Afgehaald en volledig betaald.");
                    IsAfrekenenOpen = false;
                }
            }
            catch (Exception ex)
            {
                _toast.Error(ex.GetBaseException().Message);
            }
        }

        /// <summary>Later betalen: werkbon wel op Afgehaald, bestelbon blijft open.</summary>
        [RelayCommand]
        private async Task LaterBetalenAsync()
        {
            if (WerkBon is null) return;
            IsAfrekenenOpen = false;

            if (WerkBon.Status != WerkBonStatus.Afgehaald)
            {
                try
                {
                    await _statusWorkflow.ChangeWerkBonStatusAsync(WerkBon.Id, WerkBonStatus.Afgehaald);
                    _toast.Success($"Afgehaald — nog € {AfrekenRest:0.00} te betalen.");
                }
                catch (InvalidOperationException ex)
                {
                    _toast.Warning(ex.Message);
                    return;
                }
            }

            await LoadAsync();
        }

        [RelayCommand]
        private void AnnuleerAfrekenen()
        {
            IsAfrekenenOpen = false;
            GeselecteerdeStatus = WerkBon?.Status;
        }

        /// <summary>Werkbon al afgehaald met een openstaande rest → het afrekenblok opnieuw openen.</summary>
        [RelayCommand]
        private void Afrekenen()
        {
            if (AfrekenRest > 0m) OpenAfrekenen();
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
        partial void OnWerkBonChanged(WerkBon? value)
        {
            OnPropertyChanged(nameof(HeeftAfhaalDatum));
            OnPropertyChanged(nameof(HeeftOpenstaandeRest));
        }

        public async Task LoadAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();

            var wb = await db.WerkBonnen
                .Include(w => w.Offerte).ThenInclude(o => o.Klant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.TypeLijst)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.InlegTypeLijst)   // US-71: inleg-nummer tonen
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Glas)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout1)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout2)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.DiepteKern)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Opkleven)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.Rug)
                // US-51: ook de gekozen afwerking-varianten (kleur/uitvoering) voor de samenvatting.
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.GlasVariant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout1Variant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.PassePartout2Variant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.DiepteKernVariant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.OpklevenVariant)
                .Include(w => w.Taken).ThenInclude(t => t.OfferteRegel).ThenInclude(r => r!.RugVariant)
                .FirstOrDefaultAsync(w => w.Id == _werkBonId);

            if (wb is null)
            {
                _toast.Error("Werkbon niet gevonden.");
                return;
            }

            WerkBon = wb;
            GeselecteerdeStatus = wb.Status;
            await LaadAfrekenStandAsync();   // US-76
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

            // US-76: afhalen met nog een rest op de bestelbon → eerst de vraag "afrekenen".
            if (GeselecteerdeStatus == WerkBonStatus.Afgehaald && _ontvangsten is not null)
            {
                await LaadAfrekenStandAsync();
                if (AfrekenRest > 0m)
                {
                    OpenAfrekenen();
                    return;
                }
            }

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
