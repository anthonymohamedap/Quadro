using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuadroApp.Data;
using QuadroApp.Model;
using QuadroApp.Model.DB;
using QuadroApp.Service.Toast;
using QuadroApp.Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QuadroApp.ViewModels;

/// <summary>
/// Bundelt de planningsuitvoering: plannen van geselecteerde regels, per-regel planning,
/// herplannen en verwijderen van taken. Wordt aangestuurd vanuit <see cref="PlanningCalendarViewModel"/>.
/// </summary>
public partial class PlanningUitvoeringViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IWerkBonWorkflowService _workflow;
    private readonly IToastService _toast;
    private readonly Func<Task> _requestRefresh;
    private readonly Func<DateTime, Task<bool>> _isDagGeblokkeerd;

    private const int CapaciteitMinuten = 8 * 60;

    // Injecteer vanuit de code-behind om een PlanningTijdDialog te tonen.
    public Func<PlanningTijdDialogViewModel, Task<bool>>? ShowTijdDialogAsync { get; set; }

    // State gesynchroniseerd vanuit PlanningCalendarViewModel
    public int WerkBonId { get; set; }
    public DateTime SelectedDate { get; set; }
    public ObservableCollection<RegelPlanItem> RegelsVanWerkBon { get; set; } = new();

    public PlanningUitvoeringViewModel(
        IDbContextFactory<AppDbContext> factory,
        IWerkBonWorkflowService workflow,
        IToastService toast,
        Func<Task> requestRefresh,
        Func<DateTime, Task<bool>> isDagGeblokkeerd)
    {
        _factory = factory;
        _workflow = workflow;
        _toast = toast;
        _requestRefresh = requestRefresh;
        _isDagGeblokkeerd = isDagGeblokkeerd;
    }

    // ───────── PLAN GESELECTEERDE REGELS (MET AUTO-SPREAD) ─────────

    [RelayCommand]
    private async Task PlanGeselecteerdeRegelsAsync()
    {
        if (WerkBonId == 0)
        {
            _toast.Error("Open planning vanuit een werkbon om regels te plannen.");
            return;
        }

        var selectedIds = RegelsVanWerkBon.Where(x => x.IsSelected).Select(x => x.RegelId).ToList();
        if (selectedIds.Count == 0) return;

        // Regels laden om geschatte duur te berekenen
        await using var dbCalc = await _factory.CreateDbContextAsync();

        // Statuscheck: afgewerkte/afgehaalde werkbonnen mogen niet (opnieuw) gepland worden
        var werkBonStatus = await dbCalc.WerkBonnen
            .Where(w => w.Id == WerkBonId)
            .Select(w => (WerkBonStatus?)w.Status)
            .FirstOrDefaultAsync();

        if (werkBonStatus is WerkBonStatus.Afgewerkt or WerkBonStatus.Afgehaald)
        {
            _toast.Error("Deze werkbon is al afgewerkt of afgehaald en kan niet meer gepland worden.");
            return;
        }

        var regels = await dbCalc.OfferteRegels
            .Include(r => r.TypeLijst)
            .Include(r => r.Glas)
            .Include(r => r.PassePartout1)
            .Include(r => r.PassePartout2)
            .Include(r => r.DiepteKern)
            .Include(r => r.Opkleven)
            .Include(r => r.Rug)
            .Where(r => selectedIds.Contains(r.Id))
            .ToListAsync();

        int totaalGeschat = regels.Sum(CalcMinutenVoorRegel);

        // Datum bepalen via dialoog
        DateTime startDag;
        if (ShowTijdDialogAsync is not null)
        {
            var werkBonLabel = await dbCalc.WerkBonnen
                .Where(w => w.Id == WerkBonId)
                .Select(w => w.Offerte.Klant != null ? w.Offerte.Klant.Achternaam : null)
                .FirstOrDefaultAsync() ?? $"WerkBon #{WerkBonId}";

            var dialogVm = new PlanningTijdDialogViewModel
            {
                ContextLabel = $"WerkBon #{WerkBonId} — {werkBonLabel} · {selectedIds.Count} regel(s)",
                GeplandeDatum = new DateTimeOffset(SelectedDate.Date),
                TotaalMinuten = totaalGeschat,
            };

            bool ok = await ShowTijdDialogAsync(dialogVm);
            if (!ok) return;

            startDag = dialogVm.GetStartDatum();
        }
        else
        {
            startDag = SelectedDate.Date.AddHours(9);
        }

        var huidigeDag = startDag.Date;

        try
        {
            foreach (var r in regels)
            {
                int duur = CalcMinutenVoorRegel(r);
                huidigeDag = await _workflow.PlanRegelMetDagCapaciteitAsync(
                    WerkBonId,
                    r.Id,
                    huidigeDag,
                    duur,
                    CapaciteitMinuten,
                    "Inlijsten");
            }
        }
        catch (InvalidOperationException ex)
        {
            _toast.Error(ex.Message);
            await _requestRefresh();
            return;
        }

        _toast.Success($"{regels.Count} taken gepland vanaf {startDag:dd/MM}.");
        await _requestRefresh();
    }

    // ───────── PLAN PER REGEL — eigen datum per inlijsting (US-21) ─────────

    /// <summary>
    /// US-21: plant elke geselecteerde inlijsting met een EIGEN datum. Toont één
    /// tijd-dialoog per regel (met de regelnaam als context) en plant die regel
    /// meteen op de gekozen dag. Zo kan inlijsting 1 op bv. 20/06 en inlijsting 2
    /// op 27/06 — elke regel behoudt zijn eigen GeplandVan.
    /// </summary>
    [RelayCommand]
    private async Task PlanPerRegelAsync()
    {
        if (WerkBonId == 0)
        {
            _toast.Error("Open planning vanuit een werkbon om regels te plannen.");
            return;
        }

        var selectedIds = RegelsVanWerkBon.Where(x => x.IsSelected).Select(x => x.RegelId).ToList();
        if (selectedIds.Count == 0) return;

        await using var dbCalc = await _factory.CreateDbContextAsync();

        // Statuscheck: afgewerkte/afgehaalde werkbonnen mogen niet (opnieuw) gepland worden
        var werkBonStatus = await dbCalc.WerkBonnen
            .Where(w => w.Id == WerkBonId)
            .Select(w => (WerkBonStatus?)w.Status)
            .FirstOrDefaultAsync();

        if (werkBonStatus is WerkBonStatus.Afgewerkt or WerkBonStatus.Afgehaald)
        {
            _toast.Error("Deze werkbon is al afgewerkt of afgehaald en kan niet meer gepland worden.");
            return;
        }

        var regels = await dbCalc.OfferteRegels
            .Include(r => r.TypeLijst)
            .Include(r => r.Glas)
            .Include(r => r.PassePartout1)
            .Include(r => r.PassePartout2)
            .Include(r => r.DiepteKern)
            .Include(r => r.Opkleven)
            .Include(r => r.Rug)
            .Where(r => selectedIds.Contains(r.Id))
            .ToListAsync();

        // Behoud de volgorde zoals in de lijst (Sortering volgt de RegelsVanWerkBon-volgorde).
        var geordend = selectedIds
            .Select(id => regels.FirstOrDefault(r => r.Id == id))
            .Where(r => r is not null)
            .Cast<OfferteRegel>()
            .ToList();

        int gepland = 0;
        var standaardDag = SelectedDate.Date;

        try
        {
            foreach (var r in geordend)
            {
                int duur = CalcMinutenVoorRegel(r);
                var label = RegelsVanWerkBon.FirstOrDefault(x => x.RegelId == r.Id)?.Label
                            ?? $"Regel #{r.Id}";

                DateTime startDag;
                if (ShowTijdDialogAsync is not null)
                {
                    var dialogVm = new PlanningTijdDialogViewModel
                    {
                        ContextLabel = $"Inlijsting: {label}",
                        GeplandeDatum = new DateTimeOffset(standaardDag),
                        TotaalMinuten = duur,
                    };

                    bool ok = await ShowTijdDialogAsync(dialogVm);
                    if (!ok) continue;   // deze regel overslaan, ga door met de volgende

                    startDag = dialogVm.GetStartDatum();
                }
                else
                {
                    startDag = standaardDag.AddHours(9);
                }

                await _workflow.PlanRegelMetDagCapaciteitAsync(
                    WerkBonId,
                    r.Id,
                    startDag.Date,
                    duur,
                    CapaciteitMinuten,
                    "Inlijsten");

                // Volgende regel standaard op dezelfde gekozen dag voorstellen.
                standaardDag = startDag.Date;
                gepland++;
            }
        }
        catch (InvalidOperationException ex)
        {
            _toast.Error(ex.Message);
            await _requestRefresh();
            return;
        }

        if (gepland > 0)
            _toast.Success($"{gepland} inlijsting(en) elk op eigen datum gepland.");
        await _requestRefresh();
    }

    /// <summary>
    /// Zoekt de eerste beschikbare dag (niet geblokkeerd) waar de taak past
    /// binnen de dagcapaciteit. Als de dag vol is, schuift door naar de volgende.
    /// </summary>
    private async Task<DateTime> ZoekBeschikbareDag(
        AppDbContext db, DateTime vanafDag, int benodigdeMinuten, HashSet<DateTime> geblokkeerd)
    {
        var dag = vanafDag.Date;
        for (int i = 0; i < 365; i++)
        {
            if (geblokkeerd.Contains(dag))
            {
                dag = dag.AddDays(1);
                continue;
            }

            var dagBezet = await db.WerkTaken
                .Where(t => t.GeplandVan.Date == dag)
                .SumAsync(t => t.DuurMinuten);

            if (dagBezet + benodigdeMinuten <= CapaciteitMinuten)
                return dag;

            dag = dag.AddDays(1);
        }

        return vanafDag.Date;
    }

    // ───────── HERPLANNEN ─────────

    [RelayCommand]
    private async Task HerplanTaakAsync(WerkTaak taak)
    {
        if (ShowTijdDialogAsync is null) return;

        var dialogVm = new PlanningTijdDialogViewModel
        {
            ContextLabel = $"WerkBon #{taak.WerkBonId} — {taak.Omschrijving}",
            GeplandeDatum = new DateTimeOffset(taak.GeplandVan.Date),
            TotaalMinuten = taak.DuurMinuten,
        };

        bool ok = await ShowTijdDialogAsync(dialogVm);
        if (!ok) return;

        var nieuweDag = dialogVm.GetStartDatum();

        // Check blokkering
        if (await _isDagGeblokkeerd(nieuweDag))
        {
            _toast.Error("De gekozen datum is geblokkeerd.");
            return;
        }

        if (taak.OfferteRegelId.HasValue && taak.DuurMinuten > CapaciteitMinuten)
        {
            await _workflow.PlanRegelMetDagCapaciteitAsync(
                taak.WerkBonId,
                taak.OfferteRegelId.Value,
                nieuweDag,
                taak.DuurMinuten,
                CapaciteitMinuten,
                taak.Omschrijving);
        }
        else
        {
            await using var db = await _factory.CreateDbContextAsync();
            var dbTaak = await db.WerkTaken.FindAsync(taak.Id);
            if (dbTaak is null) return;

            dbTaak.GeplandVan = nieuweDag;
            dbTaak.GeplandTot = nieuweDag.AddMinutes(taak.DuurMinuten);
            dbTaak.DuurMinuten = taak.DuurMinuten;

            // US-27: afhaaldatum mee laten syncen bij herplannen, net zoals bij
            // het eerste inplannen (PlanRegelMetDagCapaciteitAsync doet dit al).
            if (taak.OfferteRegelId.HasValue)
            {
                var regel = await db.OfferteRegels.FindAsync(taak.OfferteRegelId.Value);
                if (regel is not null)
                {
                    regel.AfhaalDatum = nieuweDag.Date;

                    var offerte = await db.Offertes
                        .Include(o => o.Regels)
                        .FirstOrDefaultAsync(o => o.Id == regel.OfferteId);
                    if (offerte is not null)
                    {
                        var laatste = offerte.Regels
                            .Where(r => r.AfhaalDatum.HasValue)
                            .Max(r => (DateTime?)r.AfhaalDatum);
                        if (laatste.HasValue)
                            offerte.AfhaalDatum = laatste.Value;
                    }
                }
            }

            await db.SaveChangesAsync();
        }

        _toast.Success($"Taak herplanned naar {nieuweDag:dd/MM} ({taak.DuurMinuten} min).");
        await _requestRefresh();
    }

    // ───────── VERWIJDEREN ─────────

    [RelayCommand]
    private async Task VerwijderTaakAsync(WerkTaak taak)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var dbTaak = await db.WerkTaken.FindAsync(taak.Id);
        if (dbTaak is null) return;

        db.WerkTaken.Remove(dbTaak);
        await db.SaveChangesAsync();

        _toast.Success("Taak verwijderd.");
        await _requestRefresh();
    }

    // ───────── HELPERS ─────────

    private static int CalcMinutenVoorRegel(OfferteRegel r)
    {
        int min = 0;
        min += r.TypeLijst?.WerkMinuten ?? 0;
        min += r.Glas?.WerkMinuten ?? 0;
        min += r.PassePartout1?.WerkMinuten ?? 0;
        min += r.PassePartout2?.WerkMinuten ?? 0;
        min += r.DiepteKern?.WerkMinuten ?? 0;
        min += r.Opkleven?.WerkMinuten ?? 0;
        min += r.Rug?.WerkMinuten ?? 0;
        min += r.ExtraWerkMinuten;

        int stuks = r.AantalStuks <= 0 ? 1 : r.AantalStuks;
        min *= stuks;
        return Math.Max(min, 15);
    }
}
