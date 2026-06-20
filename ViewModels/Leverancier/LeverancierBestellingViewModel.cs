using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>
/// Bundelt de bestelstaat voor de geselecteerde leverancier: bestelformulier,
/// open bestellingen, voorraadalerts en de bijbehorende commando's.
/// Wordt aangestuurd vanuit <see cref="LeveranciersViewModel"/> via het
/// <c>Bestelling</c>-property.
/// </summary>
public partial class LeverancierBestellingViewModel : ObservableObject
{
    private readonly IStockService _stock;
    private readonly IToastService _toast;
    private readonly Action<bool> _setBusy;
    private readonly Func<Leverancier?> _getLeverancier;
    private readonly Func<Task> _requestRefresh;

    // ───────── Bestel-form velden ─────────

    [ObservableProperty] private ObservableCollection<TypeLijst> bestelbareLijsten = new();
    [ObservableProperty] private ObservableCollection<LeverancierBestelling> openBestellingen = new();
    [ObservableProperty] private ObservableCollection<VoorraadAlert> leverancierAlerts = new();
    [ObservableProperty] private TypeLijst? selectedBestelTypeLijst;
    [ObservableProperty] private decimal? nieuwBestelAantalMeter = 1m;
    [ObservableProperty] private DateTimeOffset? nieuweBestellingDatum = DateTimeOffset.Now.Date;
    [ObservableProperty] private string nieuweBestellingOpmerking = string.Empty;
    [ObservableProperty] private BestelVorm nieuweBestelVorm = BestelVorm.Verstek;

    public IReadOnlyList<BestelVorm> BestelVormen { get; } =
        new[] { BestelVorm.Verstek, BestelVorm.InLengte, BestelVorm.Gemonteerd };

    // RadioButton helpers — each setter writes back to NieuweBestelVorm
    public bool BestelVormIsVerstek
    {
        get => NieuweBestelVorm == BestelVorm.Verstek;
        set { if (value) NieuweBestelVorm = BestelVorm.Verstek; }
    }
    public bool BestelVormIsInLengte
    {
        get => NieuweBestelVorm == BestelVorm.InLengte;
        set { if (value) NieuweBestelVorm = BestelVorm.InLengte; }
    }
    public bool BestelVormIsGemonteerd
    {
        get => NieuweBestelVorm == BestelVorm.Gemonteerd;
        set { if (value) NieuweBestelVorm = BestelVorm.Gemonteerd; }
    }

    partial void OnNieuweBestelVormChanged(BestelVorm value)
    {
        OnPropertyChanged(nameof(BestelVormIsVerstek));
        OnPropertyChanged(nameof(BestelVormIsInLengte));
        OnPropertyChanged(nameof(BestelVormIsGemonteerd));
        OnPropertyChanged(nameof(NieuweBestelEenheid));
        OnPropertyChanged(nameof(NieuweBestelAantalLabel));
    }

    // Eenheid voor de nieuwe bestelling: Gemonteerd → stuks, anders meter.
    public string NieuweBestelEenheid => NieuweBestelVorm == BestelVorm.Gemonteerd ? "stuks" : "meter";
    public string NieuweBestelAantalLabel => $"Aantal ({NieuweBestelEenheid})";

    // ───────── Constructor ─────────

    public LeverancierBestellingViewModel(
        IStockService stock,
        IToastService toast,
        Action<bool> setBusy,
        Func<Leverancier?> getLeverancier,
        Func<Task> requestRefresh)
    {
        _stock = stock;
        _toast = toast;
        _setBusy = setBusy;
        _getLeverancier = getLeverancier;
        _requestRefresh = requestRefresh;
    }

    // ───────── Data laden / resetten ─────────

    /// <summary>Laadt besteldata voor de geselecteerde leverancier.</summary>
    public void LaadData(
        IEnumerable<TypeLijst> lijsten,
        IEnumerable<LeverancierBestelling> bestellingen,
        IEnumerable<VoorraadAlert> alerts)
    {
        BestelbareLijsten = new ObservableCollection<TypeLijst>(lijsten);
        SelectedBestelTypeLijst = BestelbareLijsten.FirstOrDefault();
        NieuwBestelAantalMeter = 1m;
        NieuweBestellingDatum = DateTimeOffset.Now.Date;
        NieuweBestellingOpmerking = string.Empty;
        NieuweBestelVorm = BestelVorm.Verstek;
        OpenBestellingen = new ObservableCollection<LeverancierBestelling>(bestellingen);
        LeverancierAlerts = new ObservableCollection<VoorraadAlert>(alerts);
    }

    /// <summary>Leegt alle bestelstate (geen leverancier geselecteerd).</summary>
    public void Reset()
    {
        BestelbareLijsten = new ObservableCollection<TypeLijst>();
        OpenBestellingen = new ObservableCollection<LeverancierBestelling>();
        LeverancierAlerts = new ObservableCollection<VoorraadAlert>();
        SelectedBestelTypeLijst = null;
        NieuwBestelAantalMeter = 1m;
        NieuweBestellingDatum = DateTimeOffset.Now.Date;
        NieuweBestellingOpmerking = string.Empty;
        NieuweBestelVorm = BestelVorm.Verstek;
    }

    // ───────── Bestelling aanmaken ─────────

    [RelayCommand]
    private async Task MaakLeverancierBestellingAsync()
    {
        var leverancier = _getLeverancier();
        if (leverancier is null || leverancier.Id == 0)
        {
            _toast.Error("Selecteer eerst een leverancier.");
            return;
        }

        if (SelectedBestelTypeLijst is null)
        {
            _toast.Error("Selecteer eerst een lijst.");
            return;
        }

        if (!NieuwBestelAantalMeter.HasValue || NieuwBestelAantalMeter.Value <= 0m)
        {
            _toast.Error($"Aantal {NieuweBestelEenheid} moet groter zijn dan 0.");
            return;
        }

        try
        {
            _setBusy(true);

            var bestelDatum = (NieuweBestellingDatum ?? DateTimeOffset.Now.Date).Date;
            await _stock.CreateSupplierOrderAsync(
                SelectedBestelTypeLijst.Id,
                decimal.Round(NieuwBestelAantalMeter.Value, 2, MidpointRounding.AwayFromZero),
                bestelDatum,
                NieuweBestellingOpmerking,
                NieuweBestelVorm);

            await _requestRefresh();
        }
        catch (Exception ex)
        {
            _toast.Error($"Bestelling aanmaken mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            _setBusy(false);
        }
    }

    // ───────── Ontvangst ─────────

    [RelayCommand]
    private async Task OntvangBestelLijnAsync(LeverancierBestelLijn? lijn)
    {
        if (lijn is null)
            return;

        try
        {
            _setBusy(true);
            await _stock.ReceiveSupplierOrderLineAsync(lijn.Id, lijn.OntvangstInputMeter);
            await _requestRefresh();
        }
        catch (Exception ex)
        {
            _toast.Error($"Ontvangst mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            _setBusy(false);
        }
    }

    // ───────── Annuleren ─────────

    [RelayCommand]
    private async Task AnnuleerBestellingAsync(LeverancierBestelling? bestelling)
    {
        if (bestelling is null)
            return;

        try
        {
            _setBusy(true);
            await _stock.CancelSupplierOrderAsync(bestelling.Id);
            await _requestRefresh();
        }
        catch (Exception ex)
        {
            _toast.Error($"Annuleren mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            _setBusy(false);
        }
    }
}
