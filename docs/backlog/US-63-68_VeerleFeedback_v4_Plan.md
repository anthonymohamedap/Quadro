# Plan — Veerle-feedback v4.0 (testdag 09/09/2026): 6 stories, elk op een eigen branch

## Context

Na de tweede testdag gaf Veerle vijf stuks feedback over de offerte/prijsmodule en het
kassa/boekhoudluik (zie `docs/backlog`-brondocument, versie 4.0). Twee dingen zijn in dit
plan-traject bijgesteld t.o.v. het brondocument:

1. **Hernummerd US-58..62 → US-63..68** — de git-historiek gebruikt "US-58" al voor de
   (afgeronde) kant-en-klaar-kaders-feature; hergebruik van dat nummer voor iets anders zou
   verwarrend zijn in commit-historiek en PR's.
2. **Extra story US-68 toegevoegd** — tijdens het plannen bleek dat de bestaande
   "kant-en-klaar kader"-regel binnen de offerte precies is wat Veerle's mail al aanklaagde
   ("de module dekt niet de bedoeling"). Beslissing: die regel-type wordt uit de offerte-flow
   gehaald; nieuwe kant-en-klaar/winkelverkopen lopen voortaan via het nieuwe WinkelVerkoop-
   register (US-66). Bestaande historische offertes met een kant-en-klaar-regel blijven gewoon
   werken (geen data-migratie, enkel de UI om een NIEUWE regel van dat type te kiezen verdwijnt).

**Prioriteit (van Veerle):** prijsberekening/bestelbon is dringend, boekhoud-luik niet.
Volgorde: **US-64 → US-63 → US-65 → US-66 → US-68 & US-67 (in elke volgorde, na US-66)**.

Elke story wordt op een eigen feature-branch van `main` gebouwd en apart gemerged, in deze
volgorde — niet als lang-lopende parallelle branches, omdat US-63/US-65 dezelfde paar regels in
`PricingEngine.Calculate` raken en US-66/US-68/US-67 een harde entiteit-afhankelijkheid hebben.

| # | Titel | Branch | Afhankelijk van | Schatting |
|---|---|---|---|---|
| US-64 | Inleg-confirmatiebox (bugfix, afwerking US-53) | `feature/us64-inleg-confirmatiebox` | — | ~1u |
| US-63 | Korting per offerteregel + op bestelbon | `feature/us63-korting-per-offerteregel` | US-64 (merge-hygiëne) | ~4-5u |
| US-65 | Inleg telt mee in de prijsberekening | `feature/us65-inleg-prijsberekening` | US-63 (zelfde code-blok) | ~2-3u |
| US-66 | Winkelverkopen uitboeken (los register) | `feature/us66-winkelverkoop` | — | ~5-6u |
| US-68 | Kant-en-klaar kader uit offerte-flow halen | `feature/us68-retire-kantklaar-offerte` | US-66 | ~2-3u |
| US-67 | Overzicht betalingen / dagontvangsten (Fase 1) | `feature/us67-dagontvangsten-rapport` | US-66 | ~6-8u |

Workflow per story: `git checkout main && git pull` → `git checkout -b feature/us<n>-<slug>` →
implementeren + `dotnet test` → bij een migratie: **handmatig** ook de Npgsql-kant toevoegen
(zie risico onder) → PR → merge naar `main` → volgende story.

---

## US-64 — Inleg-confirmatiebox

**Bevinding:** geen ViewModel-bug — `ApplyTypeLijstFilter` en `ApplyInlegTypeLijstFilter`
(`ViewModels/Offerte/OfferteRegelViewModel.cs`, resp. ~L284-324 en ~L243-278) gebruiken beide al
een diff-aanpak (nooit `.Clear()`), dus de selectie gaat niet verloren. Het is puur een
XAML-omissie: de "Inleg nr."-picker mist het blauwe `InfoBg`-bevestigingsblok dat de hoofdlijst-
picker al heeft.

**Aanpak:** in `Views/OfferteView.axaml`, bij het "Inleg nr."-blok (~L354-371), boven de
zoek-`TextBox` een `Border` toevoegen — kopie van het bestaande blok bij de hoofdlijst
(~L242-263): `Background="{DynamicResource InfoBg}"`, `TextBlock` gebonden aan
`SelectedRegelInlegTypeLijst.Artikelnummer` met `TargetNullValue='Geen inleg-nummer geselecteerd'`.
Geen edit/beheer-knoppen nodig (de hoofdlijst heeft die voor iets anders — niet 1-op-1 relevant
hier). Geen model-, ViewModel- of migratie-wijzigingen.

**Verificatie:** app opstarten, offerteregel openen, inleg-nummer zoeken en selecteren → blauw
blok moet het gekozen artikelnummer tonen en dat blijven doen na wissen zoekterm / opslaan /
heropenen.

---

## US-63 — Korting per offerteregel

**Model** — `QuadroApp.Data/Model/DB/OfferteRegel.cs`: nieuw veld naast het bestaande absolute
`Korting` (~L234):
```csharp
public decimal KortingPct { get; set; } = 0m;   // bv 10 = 10%, per-regel i.p.v. per-offerte
```
Ook nieuw: `RegelKortingExcl` (persisted, informatief) — zodat de bestelbon-PDF het korting-bedrag
kan tonen zonder de prijsformule te herberekenen (zelfde patroon als de al bestaande persisted
totalen zoals `TotaalExcl`).

**`QuadroApp.Data/Data/AppDbContext.cs`** — in het `OfferteRegel`-entity-blok (~L332-341):
`entity.Property(x => x.KortingPct).HasPrecision(18, 2);` (en idem voor `RegelKortingExcl`).

**`Service/Pricing/PricingEngine.cs`** — in `Calculate`, na de bestaande absolute korting
(~L60-64):
```csharp
lineEx -= r.Korting;
lineEx -= lineEx * (r.KortingPct / 100m);
lineEx = Math.Max(0m, lineEx);
```
**Beslissing:** `KortingPct` telt **niet** mee wanneer `r.AfgesprokenPrijsExcl` is ingevuld — die
afgesproken-prijs-tak is al een volledige override (negeert ook `Korting`/`ExtraPrijs`/
afwerkingen) en moet dat blijven. Voeg hiervoor een regressietest toe.

**UI** — `Views/OfferteView.axaml`, in de bestaande "Prijs"-kaart (~L374-382, tot nu toe enkel
"Afgesproken prijs"): extra rij met een `NumericUpDown` gebonden aan `SelectedRegel.KortingPct`
(direct, geen forwarding-property nodig — zelfde patroon als `SelectedRegel.Opmerking`, L397).

**`ViewModels/Offerte/OfferteRegelViewModel.cs`** — `RegelDupliceren` (~L343-367) moet
`KortingPct = s.KortingPct` meenemen, anders verliest een gedupliceerde regel stilletjes zijn
korting.

**Bestelbon — gekozen aanpak: nieuwe kolommen op `FactuurLijn`** (i.p.v. het tag-mechanisme):
- `QuadroApp.Data/Model/DB/FactuurLijn.cs`: `KortingPct` en `KortingExcl` toevoegen
  (`[Precision(18,2)]`), configureren in `AppDbContext.cs` (~L295-305).
- `Service/FactuurWorkflowService.cs`, `BuildLijnen`/`CreateLijn` (~L186-300): `r.KortingPct` en
  `r.RegelKortingExcl` doorgeven naar de nieuwe `FactuurLijn`-velden.
- `Service/PdfOfferteExporter.cs`, `DrawRegel` (~L124-160): als `r.KortingPct > 0`, een rode
  sub-regel "Korting X%" tonen (zelfde stijl als de bestaande aggregaat-korting in `DrawTotals`).
- `Service/PdfFactuurExporter.cs`, `DrawItemBlock`/`BuildRenderItem` (~L170-260, 365-471): idem,
  per regel de korting tonen naast het bestaande `item.ItemTotal`.

**Migratie:** één migratie `AddOfferteRegelKortingPct` met `OfferteRegel.KortingPct`,
`OfferteRegel.RegelKortingExcl`, `FactuurLijn.KortingPct`, `FactuurLijn.KortingExcl` — toevoegen
in **zowel** `QuadroApp.Data/Migrations/` **als** `QuadroApp.Migrations.Npgsql/Migrations/`
(zelfde naam, timestamp mag een paar seconden afwijken). `Scripts/add-migration.ps1` doet enkel de
SQLite-kant — de Npgsql-migratie moet je met de hand aanmaken en verifiëren met
`dotnet ef migrations has-pending-model-changes --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql`
(dit wordt door geen enkele test afgedwongen, zie risico's onderaan).

**Tests** — `WorkflowService.Tests/PricingEngineTests.cs`: nieuwe cases voor (a) enkel
`KortingPct` op één regel, andere regels ongewijzigd, (b) `Korting` + `KortingPct` samen op
dezelfde regel, (c) `KortingPct` genegeerd wanneer `AfgesprokenPrijsExcl` gezet is. **Belangrijk:**
alle bestaande tests in dat bestand toetsen exacte decimalen (bv. `101.30m`) zonder korting — die
moeten byte-voor-byte ongewijzigd blijven als `KortingPct == 0`.

---

## US-65 — Inleg telt mee in de prijsberekening

**`Service/Pricing/PricingEngine.cs`** — in `Calculate`, naast de bestaande hoofdlijst-prijs
(~L37-46), een tweede aanroep van dezelfde `CalculateLijstPrijsExcl`-formule op de inleg:
```csharp
var inlegPrijs = (r.InlegTypeLijst is not null && r.KantKlaarKaderId is null)
    ? CalculateLijstPrijsExcl(r.InlegTypeLijst, r.InlegBreedteCm ?? r.BreedteCm,
                               r.InlegHoogteCm ?? r.HoogteCm, uurloon, ...)
    : 0m;
...
lineEx = lijstPrijs + inlegPrijs + optiesEx;
```
**Beslissing (interim, tot US-68 gemerged is):** inleg-prijs wordt **onderdrukt** wanneer
`r.KantKlaarKaderId` gezet is — consistent met de bestaande regel dat een kant-en-klaar kader al
volledig afgewerkt is (afwerkingen worden daar nu ook al onderdrukt, zie `optiesEx`-tak). Zodra
US-68 gemerged is, bestaat `KantKlaarKaderId` niet meer voor nieuwe regels en wordt deze
voorwaarde vanzelf irrelevant — dan mag de `&& r.KantKlaarKaderId is null`-guard weer verwijderd
worden als opruiming, maar dat is optioneel.

**Geen migratie** — pure formule-wijziging, geen nieuwe DB-velden.

**Risico om vóór het mergen expliciet met Veerle af te stemmen:** bestaande offertes die al een
inleg hebben ingevuld, krijgen bij de volgende keer openen/bewerken een **hoger herberekend
totaal** dan voorheen — een stille retroactieve prijswijziging op oude offertes. Al geëxporteerde/
betaalde facturen worden niet herberekend (`FactuurWorkflowService.NeedsDraftPrijsRefresh`
beperkt herberekening tot `Draft`/een smalle backfill-case), maar de onderliggende offerte
springt wél zodra iemand ze heropent. Vraag dit expliciet af voor het mergen, niet enkel vermelden
in de PR-tekst.

**Tests** — `WorkflowService.Tests/PricingEngineTests.cs`: inleg met eigen maten, inleg zonder
maten (fallback op buitenmaat), geen inleg (moet exact gelijk blijven aan huidige verwachte
waarden), en inleg + kant-en-klaar-kader (moet `0m` opleveren voor de inleg-bijdrage).

---

## US-66 — Winkelverkopen uitboeken (los register)

**Nieuw, top-level enum** — `QuadroApp.Data/Model/DB/Betaalwijze.cs`:
```csharp
public enum Betaalwijze { Kontant, Bancontact, Visa, Cheque, Proton, Storting }
```
Bewust top-level (niet genest in `WinkelVerkoop`) zodat US-67-Fase-2 dit later kan hergebruiken op
`Factuur`/`Offerte`.

**Nieuwe entiteit** — `QuadroApp.Data/Model/DB/WinkelVerkoop.cs`, naar het model van de bestaande
`KantKlaarKader.cs` (eenvoudig, geen leverancier/voorraad-koppeling):
```csharp
public class WinkelVerkoop
{
    public int Id { get; set; }
    public DateTime Datum { get; set; } = DateTime.Today;
    public string Omschrijving { get; set; } = string.Empty;   // [Required, MaxLength(300)]
    public decimal Aantal { get; set; } = 1m;
    public decimal PrijsInclBtw { get; set; }                  // [Precision(18,2)] — INCL. BTW
    public decimal BtwPct { get; set; }                        // [Precision(5,2)]
    public Betaalwijze Betaalwijze { get; set; }
    public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;
    [NotMapped] public decimal TotaalInclBtw => Aantal * PrijsInclBtw;
}
```
Geen soft-delete-filter nodig (niets anders verwijst naar deze entiteit, in tegenstelling tot
`KantKlaarKader` dat `DeleteBehavior.Restrict` nodig heeft omdat `OfferteRegel` erop wijst) — hard
delete is aanvaardbaar, blijft achteraf traceerbaar via de bestaande automatische `AuditLog`
(`AppDbContext.SaveChangesAsync`).

**`AppDbContext.cs`** — `DbSet<WinkelVerkoop> WinkelVerkopen => Set<WinkelVerkoop>();` naast
`KantKlaarKaders`; entity-config met `HasConversion<string>()` op `Betaalwijze` (zelfde patroon
als `WerkBonStatus`/`FactuurStatus`).

**ViewModel + View** — `ViewModels/WinkelVerkopen/WinkelVerkopenViewModel.cs` +
`Views/WinkelVerkopenView.axaml(.cs)`, 1-op-1 naar het patroon van
`ViewModels/KantKlaarKaders/KantKlaarKaderenViewModel.cs` / `Views/KantKlaarKaderenView.axaml`
(Load/Save/Delete/Filter), met een extra `ComboBox` voor `Betaalwijze`
(`Enum.GetValues<Betaalwijze>()`) en een datumfilter i.p.v. enkel naam-filter. Hergebruik
`Permissie.Factureren` voor toegang (geen nieuwe permissie nodig).

**Wiring** (3 plekken, zelfde als elk ander scherm):
- `App.axaml.cs`: `services.AddTransient<WinkelVerkopenViewModel>();`
- `App.axaml`: `DataTemplate` voor `WinkelVerkopenViewModel` → `WinkelVerkopenView`
- `ViewModels/HomeViewModel.cs` + `Views/HomeView.axaml`: nieuwe navigatie-knop/command.

**Migratie:** nieuwe tabel `WinkelVerkopen`, `AddWinkelVerkoop`, in beide migratie-mappen (zie
US-63 voor het Npgsql-aandachtspunt).

**Tests:** minstens een DbContext round-trip test (opslaan + herladen van een `WinkelVerkoop`,
controleren dat `Betaalwijze` correct persisteert via `HasConversion<string>()`) — er bestaat geen
precedent van ViewModel-tests voor dit type eenvoudig CRUD-scherm (`KantKlaarKaderenViewModel`
heeft er ook geen), dus dat is geen regressie t.o.v. een bestaande baseline.

---

## US-68 — Kant-en-klaar kader uit de offerte-flow halen

**Aanpak (bewust geen data-migratie):** de bestaande `KantKlaarKader`-catalogus en
`OfferteRegel.KantKlaarKaderId`/`KantKlaarKader`-navigatie blijven in de database en in
`Service/Pricing/PricingEngine.cs` bestaan — bestaande historische offertes met zo'n regel moeten
correct blijven renderen/herberekenen. Wat verandert: het wordt **niet langer mogelijk om een
NIEUWE regel van dit type aan te maken.**

- `Views/OfferteView.axaml`: de "Kant-en-klaar kader"-kaart (~L307-325, incl. de
  dropdown/zoekveld om een `KantKlaarKader` te selecteren) verwijderen uit het regel-invoerscherm
  voor **nieuwe** regels. Als een bestaande regel al een `KantKlaarKaderId` heeft, toon die
  read-only (naam + prijs), zodat je een oude offerte nog kan inkijken/afdrukken zonder de keuze
  te kunnen wijzigen.
- `ViewModels/KantKlaarKaders/KantKlaarKaderenViewModel.cs` / `Views/KantKlaarKaderenView.axaml`
  (het losse beheerscherm voor de catalogus): laten bestaan maar het scherm/menu-item markeren als
  "legacy" (bv. verplaatsen naar onderaan het navigatiemenu of een tooltip "vervangen door
  Winkelverkopen" toevoegen) — niet hard verwijderen, de catalogus-data is nog nodig voor
  historische offertes.
- Documenteren (code-comment op `OfferteRegel.KantKlaarKaderId` en op `KantKlaarKader.cs`) dat dit
  pad legacy/read-only is voor nieuwe workflows; nieuwe kant-en-klaar-verkopen horen bij
  `WinkelVerkoop` (US-66).

**Geen migratie nodig** — geen schema-wijziging, enkel UI/documentatie.

**Afhankelijkheid:** moet ná US-66 gemerged worden (de UI/instructie verwijst naar het nieuwe
WinkelVerkoop-scherm als vervanging).

**Tests:** regressietest dat een bestaande `OfferteRegel` met `KantKlaarKaderId` nog steeds correct
doorrekent via `PricingEngine` (geen wijziging aan de berekening zelf, enkel aan de UI-toegang tot
het aanmaken van nieuwe regels van dat type).

---

## US-67 — Overzicht betalingen / dagontvangsten (Fase 1)

**Gekozen scope — Fase 1 dekt enkel `WinkelVerkoop`:** van de drie bronnen die Veerle noemt
(afgehaalde inlijstingen, voorschotten, winkelverkopen) heeft vandaag **alleen** `WinkelVerkoop`
(US-66) een `Betaalwijze`. Nergens in de bestaande code — niet op `Offerte`, niet op `Factuur`,
niet op `WerkBon` — bestaat een betaalwijze-veld. Die twee andere bronnen toevoegen is een
schema-wijziging op twee al-bestaande, druk gebruikte entiteiten (`Factuur`, `Offerte`) én een
wijziging van het bestaande "markeer als betaald"/"voorschot betaald"-scherm — dat is een eigen
beslissing die niet stilzwijgend mag meeliften op een rapportage-story. **Fase 2 (betaalwijze op
Factuur/Offerte + volledige aggregatie) wordt dus expliciet niet meegenomen in deze branch — pas
oppakken als apart, bevestigd vervolg-verhaal.**

**Nieuwe service** — `Service/DagOntvangstenReportService.cs`: query `WinkelVerkoop` tussen twee
datums, groeperen per `Datum.Date` × `Betaalwijze`, som van `TotaalInclBtw`.

**Nieuwe view/viewmodel:** datumbereik-pickers + grid (rijen = dagen, kolommen = elke
`Betaalwijze`-waarde + Totaal-kolom).

**Export:** Excel als primaire export — nieuwe partial `CentralExcelExportService.Dagontvangsten.cs`,
zelfde `ExcelExportDataset`/`Col(...)`-patroon als het bestaande
`Service/Export/CentralExcelExportService.Facturen.cs`. Een PDF-export
(`Service/PdfDagOntvangstenExporter.cs`, naar het eenvoudige `new XyzExporter().Export(...)`-
patroon van `PdfWeekLijstExporter.cs`) is een nice-to-have binnen deze story, geen harde vereiste.

**Verplicht:** een duidelijk zichtbare banner/tekst op het rapport dat het momenteel enkel
winkelverkopen dekt, en dat afgehaalde/betaalde inlijstingen en voorschotten er nog niet in zitten
— zodat de boekhouder Fase 1 niet per ongeluk als het volledige dagontvangsten-cijfer gebruikt.

**Migratie:** geen (leest enkel de al bestaande `WinkelVerkoop`-tabel uit US-66).

**Afhankelijkheid:** hard, ná US-66 (heeft de `WinkelVerkoop`-tabel + `Betaalwijze`-enum nodig).

---

## Terugkerend risico (alle migratie-stories: US-63, US-66)

`Scripts/add-migration.ps1` maakt enkel een SQLite-migratie aan (`QuadroApp.Data`/
`QuadroApp.Migrations`). Er is **geen script en geen test** voor de Postgres-kant — de bestaande
`WorkflowService.Tests/ModelDriftTests.cs` controleert alleen SQLite. Na elke migratie moet je dus
met de hand:
1. Dezelfde migratie ook aanmaken in `QuadroApp.Migrations.Npgsql/Migrations/` (zelfde naam).
2. Verifiëren met
   `dotnet ef migrations has-pending-model-changes --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql`
   — dit wordt door niets in CI afgedwongen, dus vergeten is een stil risico op schema-drift
   tussen SQLite (dev) en Postgres (productie). (Zie ook bestaande memory: een "pending model
   changes"-waarschuwing op Npgsql is op zichzelf soms benigne, maar een écht ontbrekende
   migratie niet — dubbel checken blijft nodig.)

## Verificatie per story

- **Alle stories:** `dotnet test` moet slagen, met bijzondere aandacht voor
  `WorkflowService.Tests/PricingEngineTests.cs` (US-63/US-65/US-68 raken `PricingEngine`) — de
  bestaande exacte-decimalen-tests mogen niet veranderen wanneer de nieuwe velden op hun
  default/afwezig staan.
- **US-64/US-63/US-65:** app opstarten, een offerte met meerdere regels openen, per regel een
  inleg en/of korting instellen, controleren dat de offerte-totalen en (voor US-63) de bestelbon-
  PDF live/correct meebewegen, en dat een regel zonder korting/inleg exact hetzelfde bedrag toont
  als vóór de wijziging.
- **US-66/US-68:** nieuw Winkelverkopen-scherm openen, een verkoop toevoegen met elke
  `Betaalwijze`, controleren dat die na herstart/herladen bewaard blijft; een bestaande offerte met
  een kant-en-klaar-regel openen en controleren dat ze nog correct afdrukt, en dat het aanmaken
  van een NIEUWE kant-en-klaar-regel niet meer mogelijk is.
- **US-67:** een periode met gekende winkelverkopen opvragen, controleren dat de dag/betaalwijze-
  totalen kloppen, en dat de "enkel winkelverkopen"-banner zichtbaar is; Excel-export openen en
  controleren dat de kolommen/bedragen overeenkomen met het scherm.
