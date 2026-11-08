using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuadroApp.Data;
using QuadroApp.Model.DB;
using QuadroApp.Service;
using QuadroApp.Service.Interfaces;
using QuadroApp.Service.Pricing;
using QuadroApp.Service.Verkoop;
using WorkflowService.Tests.TestInfrastructure;
using Xunit;

namespace WorkflowService.Tests;

/// <summary>US-87 — fundament van het verkoopdossier: stapovergangen, overslaan en annuleren.</summary>
public class VerkoopDossierServiceTests
{
    private sealed record Opstelling(SqliteTestDatabase Scope, QuadroApp.Service.WorkflowService Workflow,
        WerkBonWorkflowService WerkBonWorkflow, VerkoopDossierService Dossier, int OfferteId, int WerkBonId, int[] RegelIds);

    private static async Task<Opstelling> OpstellingAsync(bool metAfhaalDatum = true, int regels = 2, decimal voorschot = 0m)
    {
        var scope = await DbFactoryBuilder.CreateSqliteAsync();
        var f = scope.Factory;
        var pricing = new PricingService(f, new VastePrijsInstellingen(), new PricingEngine(), NullLogger<PricingService>.Instance);
        var bestelbon = new BestelbonWorkflowService(f, pricing, new TestAuthService());
        var workflow = new QuadroApp.Service.WorkflowService(f, NullLogger<QuadroApp.Service.WorkflowService>.Instance, new TestToastService(), bestelbon);

        int offerteId;
        await using (var db = await f.CreateDbContextAsync())
        {
            var offerte = new Offerte
            {
                OfferteNummer = 423, Datum = DateTime.Today, Status = OfferteStatus.Verzonden,
                Klant = new Klant { Voornaam = "Sofie", Achternaam = "Claes" },
                TotaalInclBtw = 121m * regels, SubtotaalExBtw = 100m * regels, BtwBedrag = 21m * regels,
                AfhaalDatum = metAfhaalDatum ? DateTime.Today.AddDays(8) : null
            };
            for (var i = 0; i < regels; i++)
                offerte.Regels.Add(new OfferteRegel
                {
                    AantalStuks = 1, BreedteCm = 30, HoogteCm = 40, Titel = $"Inlijsting {i + 1}", AfgesprokenPrijsExcl = 121m,
                    TotaalExcl = 100m, SubtotaalExBtw = 100m, BtwBedrag = 21m, TotaalInclBtw = 121m
                });
            db.Offertes.Add(offerte);
            await db.SaveChangesAsync();
            offerteId = offerte.Id;
            if (voorschot > 0m)
            {
                db.Ontvangsten.Add(new Ontvangst { Soort = OntvangstSoort.Voorschot, OfferteId = offerteId, Datum = DateTime.Today, Betaalwijze = Betaalwijze.Bancontact, BedragIncl = voorschot });
                await db.SaveChangesAsync();
            }
        }

        await workflow.ChangeOfferteStatusAsync(offerteId, OfferteStatus.Goedgekeurd);   // Bevestigen → Werkbon

        await using var check = await f.CreateDbContextAsync();
        var werkBonId = await check.WerkBonnen.Where(w => w.OfferteId == offerteId).Select(w => w.Id).SingleAsync();
        var regelIds = await check.OfferteRegels.Where(r => r.OfferteId == offerteId).OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync();
        return new Opstelling(scope, workflow, new WerkBonWorkflowService(f, workflow), new VerkoopDossierService(f, workflow, new TestAuthService()),
            offerteId, werkBonId, regelIds);
    }

    private static DateTime VolgendeMaandag()
    {
        var d = DateTime.Today.AddDays(1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private static async Task PlanAsync(Opstelling o, params int[] regelIds)
    {
        foreach (var id in regelIds)
            await o.WerkBonWorkflow.PlanRegelMetDagCapaciteitAsync(o.WerkBonId, id, VolgendeMaandag(), 30, 480, "Inlijsten");
    }

    private static async Task<(OfferteStatus Offerte, WerkBonStatus WerkBon)> StatusAsync(Opstelling o)
    {
        await using var db = await o.Scope.Factory.CreateDbContextAsync();
        var off = await db.Offertes.AsNoTracking().Include(x => x.WerkBon).SingleAsync(x => x.Id == o.OfferteId);
        return (off.Status, off.WerkBon!.Status);
    }

    private sealed class GeenOfferteNavigatie : IOfferteNavigationService
    {
        public Task OpenOfferteAsync(int offerteId) => Task.CompletedTask;
        public Task NewOfferteAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Stap2_toont_de_planning_en_inplannen_werkt_het_dossier_bij_zonder_stap_te_wijzigen_US92()
    {
        var o = await OpstellingAsync(metAfhaalDatum: false, regels: 1);
        await using var _ = o.Scope;
        var f = o.Scope.Factory;
        var toast = new TestToastService();
        var dossier = new QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel(f, new VerkoopOverzichtService(f), o.Dossier,
            new TestNavigationService(), new GeenOfferteNavigatie(), o.Workflow, o.WerkBonWorkflow, new TestDialogService(), toast);
        await dossier.ReceiveAsync(o.OfferteId);
        await dossier.InitializeAsync();

        Assert.Equal(VerkoopStap.Werkbon, dossier.Info!.Huidig);
        Assert.True(dossier.IsPlanningZichtbaar);
        Assert.False(dossier.ToontPlanningKnop);
        Assert.Contains(dossier.Meldingen, m => m.StartsWith("Plan eerst"));
        var regel = Assert.Single(dossier.Planning!.RegelsVanWerkBon);

        await dossier.Planning.PlanRegelOpDatumAsync(regel.RegelId, VolgendeMaandag());
        for (var i = 0; i < 50 && dossier.Meldingen.Any(m => m.StartsWith("Plan eerst")); i++)
            await Task.Delay(100);

        Assert.DoesNotContain(dossier.Meldingen, m => m.StartsWith("Plan eerst"));
        Assert.DoesNotContain(dossier.Meldingen, m => m.Contains("afhaaldatum"));   // = laatste plandag (US-27)
        Assert.Equal(VerkoopStap.Werkbon, dossier.Info!.Huidig);                     // inplannen ≠ start productie
        Assert.Equal((OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland), await StatusAsync(o));
    }

    [Fact]
    public async Task Planningpagina_voor_een_werkbon_toont_zijn_inlijstingen_en_terug_gaat_naar_de_herkomst_US92()
    {
        var o = await OpstellingAsync(regels: 2);
        await using var _ = o.Scope;
        var nav = new TestNavigationService();
        var terug = false;

        await QuadroApp.ViewModels.PlanningPaginaViewModel.ToonAsync(nav, o.Scope.Factory, o.WerkBonWorkflow, new TestToastService(),
            o.Workflow, o.WerkBonId, () => { terug = true; return Task.CompletedTask; });

        var pagina = Assert.IsType<QuadroApp.ViewModels.PlanningPaginaViewModel>(nav.CurrentViewModel);
        await pagina.InitializeAsync();
        Assert.Equal(2, pagina.Kalender.RegelsVanWerkBon.Count);

        await pagina.GaTerugCommand.ExecuteAsync(null);
        Assert.True(terug);
    }

    [Fact]
    public async Task Planner_van_stap2_plant_via_dezelfde_uitvoering_en_toont_de_status_US92()
    {
        var o = await OpstellingAsync(regels: 2);
        await using var _ = o.Scope;
        var f = o.Scope.Factory;
        var kalender = new QuadroApp.ViewModels.PlanningCalendarViewModel(f, o.WerkBonWorkflow, new TestToastService(), o.Workflow);
        await kalender.InitializeAsync(o.WerkBonId);
        var planner = new QuadroApp.ViewModels.WerkbonPlannerViewModel(kalender, f, o.WerkBonId);
        await planner.LaadAsync();

        Assert.Equal(14, planner.Dagen.Count);                       // 2 weken, ook het weekend (planning kan daar werk zetten)
        Assert.Equal("2 nog te plannen", planner.KopRechts);
        Assert.False(planner.HeeftSelectie);
        Assert.False(planner.Regels[0].IsEnkelGepland);              // geen herplannen/✕ zolang niet gepland

        planner.Regels[0].IsSelected = true;
        Assert.True(planner.HeeftSelectie);
        planner.KiesDagCommand.Execute(new QuadroApp.ViewModels.PlannerDag { Datum = VolgendeMaandag() });
        await planner.PlanOpDagCommand.ExecuteAsync(null);

        Assert.Equal("1 nog te plannen", planner.KopRechts);
        Assert.True(planner.Regels[0].IsGepland);
        Assert.True(planner.Regels[0].IsEnkelGepland);
        Assert.False(planner.HeeftSelectie);                         // geplande regel niet meer geselecteerd
        Assert.Equal("te bestellen", planner.Regels[0].BestelTekst);

        await planner.VerwijderTaakCommand.ExecuteAsync(planner.Regels[0]);
        Assert.Equal("2 nog te plannen", planner.KopRechts);

        // Een lange inlijsting wordt over twee dagen verdeeld: beide delen zichtbaar, ✕ per deel.
        await o.WerkBonWorkflow.PlanRegelMetDagCapaciteitAsync(o.WerkBonId, o.RegelIds[1], VolgendeMaandag(), 600, 480, "Inlijsting 2");
        await planner.LaadAsync();
        var lang = planner.Regels[1];
        Assert.True(lang.HeeftMeerdereDelen);
        Assert.Equal("gepland in 2 delen", lang.PlanTekst);
        Assert.Equal(600, lang.DuurMinuten);
        await planner.VerwijderDeelCommand.ExecuteAsync(lang.Delen.Last());
        Assert.Equal("gepland " + VolgendeMaandag().ToString("ddd d/MM", new System.Globalization.CultureInfo("nl-BE")), planner.Regels[1].PlanTekst);
        Assert.Equal((OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland), await StatusAsync(o));
    }

    [Fact]
    public async Task Pagina_planning_zonder_werkbon_toont_de_nieuwe_planner_met_ander_werk_US92()
    {
        var o = await OpstellingAsync(regels: 1);
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);

        var pagina = new QuadroApp.ViewModels.PlanningPaginaViewModel(o.Scope.Factory, o.WerkBonWorkflow, new TestToastService(),
            o.Workflow, new TestNavigationService());
        await pagina.InitializeAsync();

        var planner = pagina.Planner!;
        Assert.False(planner.HeeftWerkBon);
        Assert.Equal(2, planner.PlannerKolommen);
        Assert.Empty(planner.Regels);
        var dag = Assert.Single(planner.Dagen, d => d.Datum == VolgendeMaandag());
        Assert.Equal(new[] { "Claes" }, dag.AnderWerk);
        Assert.Empty(dag.TakenDezeVerkoop);
    }

    private static OntvangstService Ontvangsten(Opstelling o)
    {
        var pricing = new PricingService(o.Scope.Factory, new VastePrijsInstellingen(), new PricingEngine(), NullLogger<PricingService>.Instance);
        return new OntvangstService(o.Scope.Factory, new TestAuthService(), new BestelbonWorkflowService(o.Scope.Factory, pricing, new TestAuthService()));
    }

    private sealed class BestelbonProvider(Opstelling o) : IServiceProvider
    {
        public object? GetService(Type t)
        {
            if (t != typeof(QuadroApp.ViewModels.BestelbonnenViewModel)) return null;
            var pricing = new PricingService(o.Scope.Factory, new VastePrijsInstellingen(), new PricingEngine(), NullLogger<PricingService>.Instance);
            var bw = new BestelbonWorkflowService(o.Scope.Factory, pricing, new TestAuthService());
            return new QuadroApp.ViewModels.BestelbonnenViewModel(o.Scope.Factory, bw,
                new BestelbonExportService(o.Scope.Factory, Array.Empty<IBestelbonExporter>()), new TestToastService(),
                new TestNavigationService(), new OntvangstService(o.Scope.Factory, new TestAuthService(), bw), new TestPathOpener());
        }
    }

    [Fact]
    public async Task Momentopname_enkel_bij_afronden_en_maar_een_keer_US102()
    {
        var o = await OpstellingAsync(regels: 1);
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);
        await o.Dossier.StartProductieAsync(o.OfferteId);
        await o.Dossier.MeldKlaarAsync(o.OfferteId);

        Assert.False(await o.Dossier.MaakMomentopnameAsync(o.OfferteId));     // nog niet afgehaald/betaald
        Assert.Null(await o.Dossier.GetMomentopnameAsync(o.OfferteId));

        var ontvangsten = Ontvangsten(o);
        var stand = await ontvangsten.GetAfrekenStandAsync(o.WerkBonId);
        await ontvangsten.AfrekenenBijAfhalingAsync(o.WerkBonId, stand!.Stand.Rest, DateTime.Today, Betaalwijze.Cash);

        Assert.True(await o.Dossier.MaakMomentopnameAsync(o.OfferteId));
        Assert.False(await o.Dossier.MaakMomentopnameAsync(o.OfferteId));     // eenmalig

        // Later een regel hernoemen verandert de momentopname niet.
        await using (var db = await o.Scope.Factory.CreateDbContextAsync())
        {
            (await db.OfferteRegels.SingleAsync()).Titel = "Andere titel";
            await db.SaveChangesAsync();
        }
        var moment = await o.Dossier.GetMomentopnameAsync(o.OfferteId);
        Assert.NotNull(moment);
        Assert.Equal("Sofie Claes", moment!.KlantNaam);
        Assert.StartsWith("Inlijsting 1 · 1× 30×40 cm", Assert.Single(moment.Regels));
    }

    [Fact]
    public async Task Dossier_stappen_3_tot_5_met_dezelfde_werkbon_en_bestelbon_US93()
    {
        var o = await OpstellingAsync(regels: 1);
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);
        await o.Dossier.StartProductieAsync(o.OfferteId);

        var f = o.Scope.Factory;
        var dialogs = new TestDialogService { ConfirmResult = true };
        var dossier = new QuadroApp.ViewModels.Verkoop.VerkoopDossierViewModel(f, new VerkoopOverzichtService(f), o.Dossier,
            new TestNavigationService(), new GeenOfferteNavigatie(), o.Workflow, o.WerkBonWorkflow, dialogs, new TestToastService(),
            ontvangsten: Ontvangsten(o), services: new BestelbonProvider(o));
        await dossier.ReceiveAsync(o.OfferteId);
        await dossier.InitializeAsync();

        // Stap 3: werkbon zoals in het werkbonvenster, stap overslaan mogelijk.
        Assert.Equal(VerkoopStap.InProductie, dossier.Info!.Huidig);
        Assert.True(dossier.IsStap3Zichtbaar);
        Assert.True(dossier.KanOverslaan);
        Assert.Single(dossier.WerkbonDetail!.Taken);

        // Klaar om af te halen → stap 4 met de bestelbon.
        await dossier.HoofdKnopCommand.ExecuteAsync(null);
        Assert.Equal(VerkoopStap.KlaarOmAfTeHalen, dossier.Info!.Huidig);
        Assert.True(dossier.IsStap45Zichtbaar);
        Assert.True(dossier.HeeftBestelbonDetail);
        Assert.False(dossier.KanOverslaan);

        // Klant komt afhalen → afrekenen (er is een rest).
        await dossier.HoofdKnopCommand.ExecuteAsync(null);
        Assert.True(dossier.WerkbonDetail!.IsAfrekenenOpen);

        // Betalen en afhalen → afgerond, momentopname bewaard.
        await dossier.WerkbonDetail.BetalenCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && dossier.Info?.IsAfgerond != true; i++) await Task.Delay(100);
        Assert.True(dossier.Info!.IsAfgerond);
        Assert.True(dossier.IsAfgerondMelding);
        for (var i = 0; i < 30 && await o.Dossier.GetMomentopnameAsync(o.OfferteId) is null; i++) await Task.Delay(100);
        Assert.NotNull(await o.Dossier.GetMomentopnameAsync(o.OfferteId));
    }

    [Fact]
    public async Task Werkbon_pdf_zonder_en_met_prijzen_US94()
    {
        var o = await OpstellingAsync(regels: 2);
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds[0]);

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;   // zoals App.axaml.cs
        var data = await PdfWerkBonExporter.LeesAsync(o.Scope.Factory, o.WerkBonId);
        Assert.Equal(423, data.Nummer);                                   // werkbon = verkoopnummer
        Assert.StartsWith("Sofie Claes", data.KlantRegel);
        Assert.Equal(2, data.Regels.Count);
        Assert.StartsWith("1 × Inlijsting 1 — 30 × 40 cm", data.Regels[0].Kop);
        Assert.StartsWith("gepland ", data.Regels[0].Gepland);
        Assert.Null(data.Regels[1].Gepland);
        Assert.Equal("Geen lijst gekozen", data.Regels[1].LijstOfKader);

        var vorigeHome = Environment.GetEnvironmentVariable("HOME");
        var map = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QuadroWerkbonPdf_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(map);
        Environment.SetEnvironmentVariable("HOME", map);
        try
        {
            var zonder = new PdfWerkBonExporter().Export(data, metPrijzen: false);
            var met = new PdfWerkBonExporter().Export(data, metPrijzen: true);
            Assert.EndsWith("Werkbon-423.pdf", zonder);
            Assert.EndsWith("Werkbon-423-met-prijzen.pdf", met);
            Assert.True(new System.IO.FileInfo(zonder).Length > 500);
            Assert.True(System.IO.File.Exists(met));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", vorigeHome);
            try { System.IO.Directory.Delete(map, true); } catch (System.IO.IOException) { }
        }
    }

    [Fact]
    public void Overslaan_vraagt_een_reden_US93()
    {
        var vm = new QuadroApp.ViewModels.Verkoop.StapOverslaanDialogViewModel(VerkoopStap.Werkbon);
        Assert.True(vm.KanBevestigen);
        Assert.Equal("Standaardkader uit voorraad", vm.Reden);
        vm.GekozenReden = QuadroApp.ViewModels.Verkoop.StapOverslaanDialogViewModel.Andere;
        Assert.False(vm.KanBevestigen);
        vm.AndereReden = "  Klant had haast  ";
        Assert.True(vm.KanBevestigen);
        Assert.Equal("Klant had haast", vm.Reden);
    }

    [Fact]
    public async Task Bevestigen_maakt_de_bestelbon_definitief_en_klaar_houdt_hetzelfde_nummer()
    {
        // Anthony 08/11: de bestelbon wordt definitief bij het akkoord van de klant, niet pas op het einde.
        var o = await OpstellingAsync(regels: 1);   // bevestigd zonder voorlopige bon
        await using var _ = o.Scope;

        string nummerBijAkkoord;
        await using (var db = await o.Scope.Factory.CreateDbContextAsync())
        {
            var bon = await db.Bestelbonnen.SingleAsync(b => b.OfferteId == o.OfferteId);
            Assert.NotNull(bon.VolgNr);
            nummerBijAkkoord = bon.BestelbonNummer;
            Assert.Matches(@"^\d{4}-\d+$", nummerBijAkkoord);
        }

        await PlanAsync(o, o.RegelIds);
        await o.Dossier.StartProductieAsync(o.OfferteId);
        var nummerBijKlaar = await o.Dossier.MeldKlaarAsync(o.OfferteId);

        Assert.Equal(nummerBijAkkoord, nummerBijKlaar);
        await using var check = await o.Scope.Factory.CreateDbContextAsync();
        Assert.Equal(1, await check.Bestelbonnen.CountAsync());
    }

    [Fact]
    public async Task Inplannen_verandert_de_offertestatus_niet_meer()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);
        Assert.Equal((OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland), await StatusAsync(o));   // was InProductie (US-51)
    }

    [Fact]
    public async Task Start_productie_vraagt_alles_gepland_en_een_afhaaldatum()
    {
        var o = await OpstellingAsync(metAfhaalDatum: false);
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds[0]);

        var ontbreekt = await o.Dossier.WatOntbreektVoorProductieAsync(o.OfferteId);
        Assert.Contains("Plan eerst alle inlijstingen (1 nog te plannen).", ontbreekt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => o.Dossier.StartProductieAsync(o.OfferteId));
        Assert.Equal((OfferteStatus.Goedgekeurd, WerkBonStatus.Gepland), await StatusAsync(o));
    }

    [Fact]
    public async Task Start_productie_zet_werkbon_in_uitvoering_en_offerte_in_productie()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);
        Assert.Empty(await o.Dossier.WatOntbreektVoorProductieAsync(o.OfferteId));

        await o.Dossier.StartProductieAsync(o.OfferteId);
        Assert.Equal((OfferteStatus.InProductie, WerkBonStatus.InUitvoering), await StatusAsync(o));
    }

    [Fact]
    public async Task Weeklijst_zet_enkel_volledig_geplande_verkopen_in_productie()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds[0]);   // 1 van 2 gepland

        Assert.Equal(0, await o.Dossier.ZetWeekInProductieAsync(VolgendeMaandag()));
        Assert.Equal(WerkBonStatus.Gepland, (await StatusAsync(o)).WerkBon);

        await PlanAsync(o, o.RegelIds[1]);
        Assert.Equal(1, await o.Dossier.ZetWeekInProductieAsync(VolgendeMaandag().AddDays(3)));   // dag in dezelfde week
        Assert.Equal((OfferteStatus.InProductie, WerkBonStatus.InUitvoering), await StatusAsync(o));
    }

    [Fact]
    public async Task Klaar_melden_maakt_de_werkbon_afgewerkt_met_bestelbon()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await PlanAsync(o, o.RegelIds);
        await o.Dossier.StartProductieAsync(o.OfferteId);

        var nummer = await o.Dossier.MeldKlaarAsync(o.OfferteId);
        Assert.False(string.IsNullOrEmpty(nummer));
        Assert.Equal((OfferteStatus.Besteld, WerkBonStatus.Afgewerkt), await StatusAsync(o));
    }

    [Fact]
    public async Task Overslaan_vanuit_stap_2_gaat_meteen_naar_klaar_en_toont_overgeslagen()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;

        await Assert.ThrowsAsync<InvalidOperationException>(() => o.Dossier.SlaOverNaarKlaarAsync(o.OfferteId, " "));
        var nummer = await o.Dossier.SlaOverNaarKlaarAsync(o.OfferteId, "Standaardkader uit voorraad");

        Assert.False(string.IsNullOrEmpty(nummer));
        Assert.Equal(WerkBonStatus.Afgewerkt, (await StatusAsync(o)).WerkBon);

        var regel = Assert.Single(await new VerkoopOverzichtService(o.Scope.Factory).GetOverzichtAsync());
        Assert.Equal(VerkoopStap.KlaarOmAfTeHalen, regel.Stap.Huidig);
        Assert.Equal(StapToestand.Overgeslagen, regel.Stap.ToestandVan(VerkoopStap.Werkbon));
        Assert.Equal(StapToestand.Overgeslagen, regel.Stap.ToestandVan(VerkoopStap.InProductie));
        Assert.Equal(StapToestand.Klaar, regel.Stap.ToestandVan(VerkoopStap.Offerte));

        await using var db = await o.Scope.Factory.CreateDbContextAsync();
        Assert.Equal("Standaardkader uit voorraad", (await db.Offertes.SingleAsync()).OverslaanReden);
    }

    [Fact]
    public async Task Annuleren_met_terugbetaling_boekt_het_voorschot_terug()
    {
        var o = await OpstellingAsync(voorschot: 100m);
        await using var _ = o.Scope;

        await o.Dossier.AnnuleerAsync(o.OfferteId, new AnnuleerOpdracht("Klant ziet af", VoorschotAfhandeling.Terugbetalen, Betaalwijze.Cash));

        await using var db = await o.Scope.Factory.CreateDbContextAsync();
        var offerte = await db.Offertes.SingleAsync();
        Assert.Equal(OfferteStatus.Geannuleerd, offerte.Status);
        Assert.Equal("Klant ziet af", offerte.AnnuleerReden);
        Assert.NotNull(offerte.GeannuleerdOp);
        Assert.All(await db.Bestelbonnen.ToListAsync(), b => Assert.Equal(BestelbonStatus.Geannuleerd, b.Status));

        var terug = Assert.Single(await db.Ontvangsten.Where(x => x.Soort == OntvangstSoort.Correctie).ToListAsync());
        Assert.Equal(-100m, terug.BedragIncl);
        Assert.Equal(Betaalwijze.Cash, terug.Betaalwijze);
        Assert.Equal(0m, await o.Dossier.OntvangenBedragAsync(o.OfferteId));
    }

    [Fact]
    public async Task Ontvangen_telt_ook_betalingen_op_de_bestelbon_zonder_offerteId_US90()
    {
        var o = await OpstellingAsync(voorschot: 100m);
        await using var _ = o.Scope;
        await o.Dossier.SlaOverNaarKlaarAsync(o.OfferteId, "Klant neemt meteen mee");

        await using (var db = await o.Scope.Factory.CreateDbContextAsync())
        {
            var bon = await db.Bestelbonnen.SingleAsync();
            db.Ontvangsten.Add(new Ontvangst
            {
                Soort = OntvangstSoort.Betaling, BestelbonId = bon.Id, OfferteId = null,
                Datum = DateTime.Today, BedragIncl = 20m, Betaalwijze = Betaalwijze.Cash
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(120m, await o.Dossier.OntvangenBedragAsync(o.OfferteId));
    }

    [Fact]
    public async Task Annuleren_met_voorschot_houden_boekt_niets_terug()
    {
        var o = await OpstellingAsync(voorschot: 100m);
        await using var _ = o.Scope;

        await o.Dossier.AnnuleerAsync(o.OfferteId, new AnnuleerOpdracht("Te duur", VoorschotAfhandeling.Houden));

        await using var db = await o.Scope.Factory.CreateDbContextAsync();
        Assert.Equal(OfferteStatus.Geannuleerd, (await db.Offertes.SingleAsync()).Status);
        Assert.Empty(await db.Ontvangsten.Where(x => x.Soort == OntvangstSoort.Correctie).ToListAsync());
        Assert.Equal(100m, await o.Dossier.OntvangenBedragAsync(o.OfferteId));
    }

    [Fact]
    public async Task Annuleren_kan_nog_na_klaar_maar_niet_zonder_reden()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await o.Dossier.SlaOverNaarKlaarAsync(o.OfferteId, "Klant neemt meteen mee");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => o.Dossier.AnnuleerAsync(o.OfferteId, new AnnuleerOpdracht("", VoorschotAfhandeling.Houden)));

        await o.Dossier.AnnuleerAsync(o.OfferteId, new AnnuleerOpdracht("Klant ziet af", VoorschotAfhandeling.Houden));

        await using var db = await o.Scope.Factory.CreateDbContextAsync();
        Assert.Equal(OfferteStatus.Geannuleerd, (await db.Offertes.SingleAsync()).Status);
        Assert.All(await db.Bestelbonnen.ToListAsync(), b => Assert.Equal(BestelbonStatus.Geannuleerd, b.Status));
    }

    [Fact]
    public async Task Annuleren_na_afhalen_kan_niet()
    {
        var o = await OpstellingAsync();
        await using var _ = o.Scope;
        await o.Dossier.SlaOverNaarKlaarAsync(o.OfferteId, "Klant neemt meteen mee");
        await o.Workflow.ChangeWerkBonStatusAsync(o.WerkBonId, WerkBonStatus.Afgehaald);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => o.Dossier.AnnuleerAsync(o.OfferteId, new AnnuleerOpdracht("Klant ziet af", VoorschotAfhandeling.Houden)));
        Assert.Contains("afgehaald", ex.Message);
    }

    [Theory]
    [InlineData(VerkoopStapFlags.Werkbon | VerkoopStapFlags.Productie, StapToestand.Overgeslagen, StapToestand.Overgeslagen)]
    [InlineData(VerkoopStapFlags.Productie, StapToestand.Klaar, StapToestand.Overgeslagen)]
    [InlineData(VerkoopStapFlags.Geen, StapToestand.Klaar, StapToestand.Klaar)]
    public void Resolver_toont_overgeslagen_stappen(VerkoopStapFlags flags, StapToestand werkbon, StapToestand productie)
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Besteld, WerkBonStatus.Afgewerkt, BestelbonStatus.KlaarVoorExport, 50m, flags);
        Assert.Equal(VerkoopStap.KlaarOmAfTeHalen, info.Huidig);
        Assert.Equal(werkbon, info.ToestandVan(VerkoopStap.Werkbon));
        Assert.Equal(productie, info.ToestandVan(VerkoopStap.InProductie));
    }

    [Fact]
    public void Afgerond_met_overgeslagen_stap_blijft_overgeslagen_tonen()
    {
        var info = VerkoopStapResolver.Bepaal(OfferteStatus.Betaald, WerkBonStatus.Afgehaald, BestelbonStatus.Betaald, 0m,
            VerkoopStapFlags.Werkbon | VerkoopStapFlags.Productie);
        Assert.True(info.IsAfgerond);
        Assert.Equal(StapToestand.Overgeslagen, info.ToestandVan(VerkoopStap.Werkbon));
        Assert.Equal(StapToestand.Klaar, info.ToestandVan(VerkoopStap.KlaarOmAfTeHalen));
    }

    private sealed class VastePrijsInstellingen : IPricingSettingsProvider
    {
        public Task<decimal> GetUurloonAsync() => Task.FromResult(60m);
        public Task<decimal> GetBtwPercentAsync() => Task.FromResult(21m);
        public Task<decimal> GetDefaultPrijsPerMeterAsync() => Task.FromResult(0m);
        public Task<decimal> GetDefaultWinstFactorAsync() => Task.FromResult(1m);
        public Task<decimal> GetDefaultAfvalPercentageAsync() => Task.FromResult(10m);
    }
}
