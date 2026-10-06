using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>US-67 — "overzicht betalingen": ontvangsten per dag en per betaalwijze over een
/// periode (standaard de lopende maand), met de twee afdrukken van de oude kassa.
/// Alleen-lezen: registreren gebeurt bij de offerte (voorschot), de bestelbon (betaling) en
/// in winkelverkopen.</summary>
public partial class OverzichtBetalingenViewModel : ObservableObject, IAsyncInitializable
{
    private readonly IBetalingsOverzichtService _service;
    private readonly INavigationService _nav;
    private readonly IToastService _toast;
    private readonly IPathOpener _pathOpener;

    [ObservableProperty] private DateTimeOffset? van;
    [ObservableProperty] private DateTimeOffset? tot;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? foutmelding;

    [ObservableProperty] private BetalingsOverzicht? overzicht;
    [ObservableProperty] private ObservableCollection<BetalingsOverzichtDag> dagen = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailRegels))]
    [NotifyPropertyChangedFor(nameof(HeeftGeselecteerdeDag))]
    private BetalingsOverzichtDag? geselecteerdeDag;

    public IReadOnlyList<BetalingsOverzichtRegel> DetailRegels =>
        GeselecteerdeDag?.Regels ?? (IReadOnlyList<BetalingsOverzichtRegel>)Array.Empty<BetalingsOverzichtRegel>();

    public bool HeeftGeselecteerdeDag => GeselecteerdeDag is not null;

    public string PeriodeTekst => Overzicht is null
        ? string.Empty
        : $"{Overzicht.Van:dd/MM/yyyy} – {Overzicht.Tot:dd/MM/yyyy} · {Overzicht.AantalOntvangsten} ontvangst(en)";

    public Action? OnTerug { get; set; }

    private bool _laadtPeriode;

    public OverzichtBetalingenViewModel(
        IBetalingsOverzichtService service,
        INavigationService nav,
        IToastService toast,
        IPathOpener pathOpener)
    {
        _service = service;
        _nav = nav;
        _toast = toast;
        _pathOpener = pathOpener;

        ZetMaand(DateTime.Today, laden: false);
    }

    public async Task InitializeAsync() => await LaadAsync();

    partial void OnVanChanged(DateTimeOffset? value) { if (!_laadtPeriode) _ = LaadAsync(); }
    partial void OnTotChanged(DateTimeOffset? value) { if (!_laadtPeriode) _ = LaadAsync(); }

    partial void OnOverzichtChanged(BetalingsOverzicht? value) => OnPropertyChanged(nameof(PeriodeTekst));

    [RelayCommand]
    private async Task DezeMaandAsync()
    {
        ZetMaand(DateTime.Today, laden: false);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VorigeMaandAsync()
    {
        ZetMaand(DateTime.Today.AddMonths(-1), laden: false);
        await LaadAsync();
    }

    [RelayCommand]
    private async Task VandaagAsync()
    {
        _laadtPeriode = true;
        Van = new DateTimeOffset(DateTime.Today);
        Tot = new DateTimeOffset(DateTime.Today);
        _laadtPeriode = false;
        await LaadAsync();
    }

    private void ZetMaand(DateTime dag, bool laden)
    {
        var eerste = new DateTime(dag.Year, dag.Month, 1);
        _laadtPeriode = !laden;
        Van = new DateTimeOffset(eerste);
        Tot = new DateTimeOffset(eerste.AddMonths(1).AddDays(-1));
        _laadtPeriode = false;
    }

    [RelayCommand]
    private async Task GaTerugAsync()
    {
        if (OnTerug is not null)
            OnTerug();
        else
            await _nav.NavigateToAsync<HomeViewModel>();
    }

    [RelayCommand]
    private async Task LaadAsync()
    {
        if (IsBusy || Van is null || Tot is null) return;

        try
        {
            IsBusy = true;
            Foutmelding = null;

            var geselecteerd = GeselecteerdeDag?.Datum;
            var resultaat = await _service.GetOverzichtAsync(Van.Value.Date, Tot.Value.Date);

            Overzicht = resultaat;
            Dagen = new ObservableCollection<BetalingsOverzichtDag>(resultaat.Dagen);
            GeselecteerdeDag = geselecteerd is null
                ? null
                : Dagen.FirstOrDefault(d => d.Datum == geselecteerd.Value);
        }
        catch (Exception ex)
        {
            Foutmelding = $"Overzicht laden mislukt: {ex.InnerException?.Message ?? ex.Message}";
            _toast.Error(Foutmelding);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task AfdrukkenDetailAsync() => AfdrukkenAsync(detail: true);

    [RelayCommand]
    private Task AfdrukkenDagtotalenAsync() => AfdrukkenAsync(detail: false);

    private async Task AfdrukkenAsync(bool detail)
    {
        if (Overzicht is null)
        {
            _toast.Warning("Laad eerst een periode.");
            return;
        }

        try
        {
            IsBusy = true;
            var exporter = new PdfBetalingsOverzichtExporter();
            var overzicht = Overzicht;
            var path = await Task.Run(() => detail
                ? exporter.ExportDetail(overzicht)
                : exporter.ExportPerDag(overzicht));

            if (!File.Exists(path))
            {
                _toast.Error("PDF kon niet aangemaakt worden.");
                return;
            }

            _pathOpener.OpenFile(path);
        }
        catch (Exception ex)
        {
            _toast.Error($"Afdrukken mislukt: {ex.InnerException?.Message ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
