using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>
/// US-93 — stappen 3 tot 5 in het verkoopdossier. Zelfde functies als vandaag:
/// stap 3 = inhoud van het werkbonvenster (werktaken, bestelstatus, "Bestelling plaatsen", planning,
/// bestelbon); stap 4 en 5 = het scherm Bestelbonnen voor deze bon (gegevens, lijnen, betalingen,
/// "Rest betaald") en het afrekenen bij afhaling (US-76). Klaar melden, afhalen en afrekenen lopen via
/// dezelfde services; enkel de statuskeuzelijst wordt een knop per stap.
/// </summary>
public partial class VerkoopDossierViewModel
{
    /// <summary>De werkbon van deze verkoop (stappen 3–5): zelfde ViewModel als het werkbonvenster.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStap3Zichtbaar), nameof(IsStap45Zichtbaar), nameof(IsSamenvattingZichtbaar))]
    private WerkBonDetailViewModel? werkbonDetail;

    /// <summary>De bestelbon van deze verkoop (stappen 4–5): zelfde ViewModel als het scherm Bestelbonnen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeeftBestelbonDetail))]
    private BestelbonnenViewModel? bestelbonDetail;

    public bool IsStap3Zichtbaar => BekekenStap == VerkoopStap.InProductie && WerkbonDetail is not null;
    public bool IsStap45Zichtbaar => BekekenStap >= VerkoopStap.KlaarOmAfTeHalen && WerkbonDetail is not null;
    public bool HeeftBestelbonDetail => BestelbonDetail?.GeselecteerdeBestelbon is not null;

    /// <summary>Afgerond (afgehaald en volledig betaald): melding bovenaan stap 5 (US-101).</summary>
    public bool IsAfgerondMelding => Info is { IsAfgerond: true } && BekekenStap == VerkoopStap.AfgehaaldEnBetaald;

    /// <summary>"⤼ Stap overslaan": enkel vanuit stap 2 of 3 (beslist 02/11: altijd rechtstreeks naar Klaar).</summary>
    public bool KanOverslaan => !IsTerugGekeken
        && Info is { IsGeannuleerd: false, IsAfgerond: false, IsAfgehaaldNogTeBetalen: false, Huidig: VerkoopStap.Werkbon or VerkoopStap.InProductie };

    /// <summary>Stap 3, kaart Offerte: "2 inlijstingen · € 245,00".</summary>
    public string OfferteSamenvatting => _offerte is null
        ? ""
        : $"{(_offerte.Regels.Count == 1 ? "1 inlijsting" : $"{_offerte.Regels.Count} inlijstingen")} · € {_offerte.TotaalInclBtw:#,##0.00}"
          + (Info is { Huidig: > VerkoopStap.Offerte } ? " · prijs vast" : "");

    /// <summary>US-102 — inlijstingen zoals ze waren bij het afronden (momentopname).</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> MomentopnameRegels { get; } = new();
    [ObservableProperty] private string? momentopnameTekst;

    private bool _vernieuwtNaWerkbon;

    private void WerkStappen345Bij()
    {
        OnPropertyChanged(nameof(IsStap3Zichtbaar));
        OnPropertyChanged(nameof(IsStap45Zichtbaar));
        OnPropertyChanged(nameof(IsAfgerondMelding));
        OnPropertyChanged(nameof(KanOverslaan));
        OnPropertyChanged(nameof(OfferteSamenvatting));
        OnPropertyChanged(nameof(IsSamenvattingZichtbaar));
    }

    private async Task LaadStappen345Async()
    {
        if (BekekenStap < VerkoopStap.InProductie || _offerte?.WerkBon is not { } wb) return;

        if (WerkbonDetail is null)
        {
            var vm = new WerkBonDetailViewModel(_factory, _workflow, _offerteNav, _toast, wb.Id, _ontvangsten);
            await vm.LoadAsync();
            vm.PropertyChanged += OpWerkbonGewijzigd;
            WerkbonDetail = vm;
        }

        if (BekekenStap >= VerkoopStap.KlaarOmAfTeHalen && _bestelbon is not null
            && BestelbonDetail?.GeselecteerdeBestelbon?.Id != _bestelbon.Id)
            await LaadBestelbonDetailAsync(_bestelbon.Id);

        await WerkMomentopnameBijAsync();
        WerkStappen345Bij();
    }

    private async Task LaadBestelbonDetailAsync(int bestelbonId)
    {
        if (_services?.GetService(typeof(BestelbonnenViewModel)) is not BestelbonnenViewModel vm) return;
        await vm.InitializeAsync();
        vm.GeselecteerdeBestelbon = vm.Bestelbonnen.FirstOrDefault(b => b.Id == bestelbonId);
        vm.PropertyChanged += OpBestelbonGewijzigd;
        if (BestelbonDetail is not null) BestelbonDetail.PropertyChanged -= OpBestelbonGewijzigd;
        BestelbonDetail = vm;
    }

    private void StopStappen345()
    {
        if (WerkbonDetail is not null) WerkbonDetail.PropertyChanged -= OpWerkbonGewijzigd;
        if (BestelbonDetail is not null) BestelbonDetail.PropertyChanged -= OpBestelbonGewijzigd;
        WerkbonDetail = null;
        BestelbonDetail = null;
    }

    /// <summary>Na Betalen / Later betalen / Bestelling plaatsen laadt de werkbon zichzelf: dan ook het dossier.</summary>
    private void OpWerkbonGewijzigd(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WerkBonDetailViewModel.WerkBon))
            _ = VernieuwNaWerkbonAsync(bestelbonOok: true);
    }

    /// <summary>Betaling geregistreerd, verwijderd of "Rest betaald" op de bestelbon: kop en stap bijwerken.</summary>
    private void OpBestelbonGewijzigd(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BestelbonnenViewModel.StandRest))
            _ = VernieuwNaWerkbonAsync(bestelbonOok: false);
    }

    private async Task VernieuwNaWerkbonAsync(bool bestelbonOok)
    {
        if (_vernieuwtNaWerkbon) return;
        _vernieuwtNaWerkbon = true;
        try
        {
            await Task.Delay(250);
            var vorige = Info?.Huidig;
            var wasAfgerond = Info?.IsAfgerond == true;
            await LaadAsync(naarHuidigeStap: true);
            if (bestelbonOok && _bestelbon is not null && BekekenStap >= VerkoopStap.KlaarOmAfTeHalen)
                await LaadBestelbonDetailAsync(_bestelbon.Id);
            if (Info is { IsAfgerond: true } && !wasAfgerond)
                await MaakMomentopnameAlsNodigAsync();
            if (vorige != Info?.Huidig) WerkStappen345Bij();
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
        finally { _vernieuwtNaWerkbon = false; }
    }

    // ── Stap 3 ───────────────────────────────────────────────────────────────

    /// <summary>"✎ Offerte aanpassen…" in stap 3: opent stap 1 bewerkbaar (zoals "Open offerte" in de werkbon).</summary>
    [RelayCommand]
    private async Task OfferteAanpassenVanuitStapAsync()
    {
        var offerte = Stappen.FirstOrDefault(s => s.Stap == VerkoopStap.Offerte);
        await BekijkStapCommand.ExecuteAsync(offerte);
        if (Stap1 is { IsAlleenLezen: false }) OfferteAanpassen();
    }

    // ── Stap overslaan (stap 2 en 3) ─────────────────────────────────────────

    [RelayCommand]
    private async Task StapOverslaanAsync()
    {
        if (!KanOverslaan || _offerte is null || Info is null) return;
        var dialoogVm = new StapOverslaanDialogViewModel(Info.Huidig);
        if (EigenaarVenster() is not { } owner) return;
        var dialoog = new QuadroApp.Views.StapOverslaanDialog { DataContext = dialoogVm };
        dialoogVm.RequestClose = ok => dialoog.Close(ok);
        if (!await dialoog.ShowDialog<bool>(owner)) return;

        try
        {
            var nr = await _dossier.SlaOverNaarKlaarAsync(OfferteId, dialoogVm.Reden);
            _toast.Success(string.IsNullOrWhiteSpace(nr)
                ? "Overgeslagen: klaar om af te halen."
                : $"Overgeslagen: klaar om af te halen. Bestelbon {nr}.");
            StopStappen345();
            await LaadAsync(naarHuidigeStap: true);
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    // ── Afgerond: ontgrendelen (US-101) ─────────────────────────────────────

    [RelayCommand]
    private async Task OntgrendelenVanuitAfgerondAsync()
    {
        var offerte = Stappen.FirstOrDefault(s => s.Stap == VerkoopStap.Offerte);
        await BekijkStapCommand.ExecuteAsync(offerte);
        if (Stap1 is null) return;
        if (!Stap1.IsOntgrendelenZichtbaar)
        {
            _toast.Warning("Ontgrendelen kan enkel een beheerder (recht Gebruikers beheren).");
            return;
        }
        await Stap1.OntgrendelenCommand.ExecuteAsync(null);
        if (!Stap1.IsAlleenLezen) OfferteAanpassen();
    }

    // ── US-102: momentopname bij afronden ───────────────────────────────────

    private async Task MaakMomentopnameAlsNodigAsync()
    {
        try { await _dossier.MaakMomentopnameAsync(OfferteId); }
        catch (Exception ex) { _toast.Warning($"Momentopname niet bewaard: {ex.Message}"); }
    }

    private async Task WerkMomentopnameBijAsync()
    {
        MomentopnameRegels.Clear();
        MomentopnameTekst = null;
        if (Info is not { IsAfgerond: true }) return;

        var moment = await _dossier.GetMomentopnameAsync(OfferteId);
        if (moment is null)
        {
            // Afgerond buiten het dossier (oud scherm): de momentopname nu alsnog maken.
            await MaakMomentopnameAlsNodigAsync();
            moment = await _dossier.GetMomentopnameAsync(OfferteId);
        }
        if (moment is null) return;

        MomentopnameTekst = $"Zoals bij het afronden op {moment.GemaaktOp:dd/MM/yyyy HH:mm} — klant {moment.KlantNaam}";
        foreach (var r in moment.Regels)
            MomentopnameRegels.Add(r);
    }
}
