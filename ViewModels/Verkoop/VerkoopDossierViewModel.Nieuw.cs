using QuadroApp.Model.DB;
using QuadroApp.Service.Verkoop;
using System;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels.Verkoop;

/// <summary>
/// Fix (Anthony 08/11): "+ Nieuwe verkoop" begon in het oude offertescherm; na "Bevestigen" en de
/// planning kwam je daar ook terug. Een nieuwe verkoop start nu in stap 1 van het dossier (OfferteId 0),
/// met dezelfde offerte-ViewModel en dezelfde opslag. Zodra de offerte bewaard is (eerste keer
/// opslaan, verlaten van stap 1, bestelbon of bevestigen) krijgt het dossier haar nummer en gaat het
/// gewoon verder, ook naar stap 2 na "Bevestigen → Werkbon".
/// </summary>
public partial class VerkoopDossierViewModel
{
    /// <summary>Nieuwe verkoop die nog niet bewaard is.</summary>
    public bool IsNieuw => OfferteId == 0;

    private async Task StartNieuweVerkoopAsync()
    {
        if (_services?.GetService(typeof(OfferteViewModel)) is not OfferteViewModel vm)
        {
            _toast.Error("Nieuwe verkoop kon niet gestart worden.");
            return;
        }

        await vm.InitializeAsync();
        await vm.NewAsync();
        vm.Workflow.PlanningOpenenNaBevestigen = false;
        vm.Workflow.Bevestigd += () => _ = NaBevestigenAsync();
        vm.PropertyChanged += OpNieuweOfferteGewijzigd;

        Stap1 = vm;
        _bewaardeVingerafdruk = vm.Vingerafdruk();
        StartBewaardTimer();

        Info = VerkoopStapResolver.Bepaal(OfferteStatus.Concept, null, null, 0m);
        BekekenStap = VerkoopStap.Offerte;
        Titel = "Nieuwe verkoop";
        KlantRegel = "Kies of maak een klant en voeg de inlijstingen toe; de verkoop krijgt zijn nummer bij het bewaren.";
        StatusTekst = "Offerte";
        TotaalTekst = "€ 0,00";
        IsAllesBetaald = false;
        NogTeBetalenTekst = "nog niet bewaard";
        KanAnnuleren = false;

        Chips.Clear();
        Chips.Add(new DossierChip("Offerte (nieuw)", true));
        Chips.Add(new DossierChip("Werkbon", false));
        Chips.Add(new DossierChip("Bestelbon", false));
        Chips.Add(new DossierChip("Voorschot", false));

        Stappen.Clear();
        foreach (var s in BouwStappen(Info, BekekenStap, DateTime.Today, null))
            Stappen.Add(s);

        HoofdKnopTekst = HoofdKnopVoor(Info);
        IsOfferteAanpassen = false;
        vm.AlleenBekijken = false;
        WerkBewaardTekstBij();
        OnPropertyChanged(nameof(IsStap1Zichtbaar));
        OnPropertyChanged(nameof(IsSamenvattingZichtbaar));
        OnPropertyChanged(nameof(IsBestelbonKnopZichtbaar));
    }

    /// <summary>De nieuwe offerte is voor het eerst bewaard: het dossier neemt haar nummer over.</summary>
    private void OpNieuweOfferteGewijzigd(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OfferteViewModel.Offerte) || !IsNieuw) return;
        if (Stap1?.Offerte is not { Id: > 0 } offerte) return;

        OfferteId = offerte.Id;
        OnPropertyChanged(nameof(IsNieuw));
        _bewaardeVingerafdruk = Stap1.Vingerafdruk();
        _ = LaadAsync();
    }

    /// <summary>Nieuwe verkoop met gegevens die (nog) niet bewaard konden worden, bv. zonder klant.</summary>
    private bool HeeftOnbewaardeNieuweVerkoop()
        => IsNieuw && Stap1 is { } vm && vm.Vingerafdruk() != _bewaardeVingerafdruk;
}
