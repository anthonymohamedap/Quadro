# QuadroApp — Project Analysis
*Gegenereerd op 4 april 2026 · .NET 9 / Avalonia 11.3 / EF Core 9 / SQLite*

---

## 1. Overzicht

QuadroApp is een desktop-applicatie voor een **inlijstwerkplaats** (Quadro). Het beheert het volledige werkproces: van klantbeheer en offertes, over werkbonnen en planning, tot facturatie en voorraadbeheer. De app draait op Windows (en theoretisch cross-platform via Avalonia UI) en wordt als één zelfstandig `.exe`-bestand gepubliceerd (`PublishSingleFile`).

### Tech Stack

| Laag | Technologie |
|---|---|
| UI Framework | Avalonia 11.3.12 (Fluent theme, CompiledBindings) |
| MVVM | CommunityToolkit.Mvvm 8.4.0 |
| ORM / DB | EF Core 9.0.9 + SQLite (`quadro.db`) |
| PDF export | QuestPDF 2024.7.1 |
| Excel I/O | ClosedXML 0.105.0 + EPPlus 8.4.1 |
| Toast UI | Huskui.Avalonia 0.10.3 |
| DI container | Microsoft.Extensions.DependencyInjection 9.0 |
| Target framework | .NET 9 (self-contained, trimming uitgeschakeld) |

---

## 2. Architectuur

```
App.axaml.cs          ← DI bootstrapping, seeder, EF migrate
  │
  ├── INavigationService  ← singleton, wisselt CurrentViewModel
  │     └── MainWindow.axaml  ← ContentControl gebonden aan CurrentViewModel
  │
  ├── ViewModels/       ← CommunityToolkit.Mvvm, IAsyncInitializable
  │     └── (transient tenzij anders)
  │
  ├── Views/            ← Avalonia UserControl / Window, CompiledBindings
  │
  ├── Service/          ← business-logica, exports, import-pipeline
  │     ├── Pricing/
  │     ├── Import/
  │     └── Toast/
  │
  ├── Data/             ← AppDbContext, DatabaseSeeder, migrations
  │
  ├── Model/DB/         ← EF entiteiten
  ├── Model/Import/     ← import-preview & resultaat-modellen
  │
  └── Validation/       ← ICrudValidator<T> per entiteit
```

### Navigatiepatroon

`INavigationService.NavigateToAsync<TViewModel>()` lost de ViewModel op via DI, cast naar `IAsyncInitializable` en roept `InitializeAsync()` aan. `MainWindowViewModel.CurrentPage` wordt geüpdatet en de `ContentControl` in `MainWindow` rendert de bijpassende `DataTemplate`.

### IAsyncInitializable

ViewModels implementeren `Task InitializeAsync()` voor laden van data na navigatie. Wordt aangeroepen door `NavigationService` na constructie.

---

## 3. Domeinmodel (25 entiteiten)

### Kernentiteiten

| Entiteit | Tabel | Sleutels / Relaties |
|---|---|---|
| `Klant` | Klanten | contactgegevens, BTW-nummer |
| `TypeLijst` | TypeLijsten | lijsttype + voorraadbeheer; FK → Leverancier |
| `AfwerkingsGroep` | AfwerkingsGroepen | code (1 char: G/P/D/O/R), naam |
| `AfwerkingsOptie` | AfwerkingsOpties | FK → AfwerkingsGroep + Leverancier; unieke index op (GroepId, Volgnummer, Kleur) |
| `Leverancier` | Leveranciers | naam max 3 chars, uniek |

### Offerte-traject

| Entiteit | Opmerkingen |
|---|---|
| `Offerte` | Status enum (Concept/…), planning-velden (GeplandeDatum, DeadlineDatum, GeschatteMinuten), 1:1 → WerkBon |
| `OfferteRegel` | 7 FK's: TypeLijst + 6 AfwerkingsOpties (Glas, PassePartout1, PassePartout2, DiepteKern, Opkleven, Rug). Alle 6 afwerkingen: `OnDelete = NoAction`. Backing fields met auto-sync van FK-int |
| `OfferteStatus` | enum: Concept, Verstuurd, Goedgekeurd, Geweigerd, Afgewerkt |

### Werkplaats

| Entiteit | Opmerkingen |
|---|---|
| `WerkBon` | Status enum (Gepland/InUitvoering/Afgewerkt/Afgehaald), 1:1 → Offerte, bevat collectie WerkTaken |
| `WerkTaak` | GeplandVan/GeplandTot (auto-berekend in SaveChangesAsync), DuurMinuten, Resource, VoorraadStatus enum |
| `GeblokkeerdeDag` | Unieke index op Datum; optionele Reden (max 200 char) |

### Facturatie

| Entiteit | Opmerkingen |
|---|---|
| `Factuur` | DocumentType, KlantNaam/Adres/BTW, Status enum, FK → WerkBon (Restrict), FK → Offerte (Restrict) |
| `FactuurLijn` | Omschrijving, Eenheid, Aantal, PrijsExcl, BtwPct, totaalvelden |

### Voorraadbeheer

| Entiteit | Opmerkingen |
|---|---|
| `LeverancierBestelling` | BestelNummer, Status enum, FK → Leverancier |
| `LeverancierBestelLijn` | Besteld/Ontvangen meter, FK → LeverancierBestelling + TypeLijst (Restrict) + WerkBon (SetNull) |
| `VoorraadMutatie` | MutatieType enum, AantalMeter, FK's → TypeLijst/WerkBon/WerkTaak/BestelLijn |
| `VoorraadAlert` | AlertType/Status enum, Bericht, FK → TypeLijst (SetNull) |

### Import-infra

| Entiteit | Opmerkingen |
|---|---|
| `ImportSession` | EntityName, FileName, Status, ErrorMessage |
| `ImportRowLog` | Key, Message, FK → ImportSession (Cascade) |

### Instellingen

| Entiteit | Opmerkingen |
|---|---|
| `Instelling` | Key/Value-store voor app-instellingen |

---

## 4. AfwerkingsOptie — Familie-concept

Elke `AfwerkingsOptie` behoort tot een groep en heeft:
- **Volgnummer** (`char`): `'1'`–`'9'` of `'A'`–`'K'`
- **Kleur** (`string`, max 50, default `"Standaard"`): onderscheidt varianten binnen dezelfde familie
- **Familie** = alle opties met dezelfde `(AfwerkingsGroepId, Volgnummer)` — zij delen prijsstelling

Uniekheidsconstraint in DB: `(AfwerkingsGroepId, Volgnummer, Kleur)`.

`AfwerkingenService.SaveOptieAsync` synchroniseert bij opslaan de prijsvelden (`KostprijsPerM2`, `WinstMarge`, `AfvalPercentage`, `VasteKost`, `WerkMinuten`) naar alle andere familieleden.

### Standaard groepen (seeder)

| Code | Naam | Opties |
|---|---|---|
| G | Glas | 3 (volgnummers: 1, 2, 3) |
| P | Passe-partout | 3 |
| D | Dieptekern | 3 |
| O | Opkleven | 3 |
| R | Rug | 3 |

**Historische bug (opgelost):** De seeder gebruikte `(char)1` (ASCII SOH = besturingsteken) i.p.v. `'1'` (ASCII 49). `LegacyAfwerkingCode.ApplyAsync()` zocht op `'1'` en vond niets. Fix: alle seeder-toewijzingen vervangen door letterlijke `char`-constanten + `FixVolgnummers()` startup-methode die bestaande databases heelt.

---

## 5. LegacyAfwerkingCode

Een 6-tekens code `GPPDOR` identificeert de afwerkingskeuzes per OfferteRegel:

```
Positie 0 = Glas          (groep G)
Positie 1 = PassePartout1 (groep P)
Positie 2 = PassePartout2 (groep P)
Positie 3 = DiepteKern    (groep D)
Positie 4 = Opkleven      (groep O)
Positie 5 = Rug           (groep R)
```

- `'0'` = geen keuze voor die positie
- `'1'`–`'9'`/`'A'`–`'K'` = volgnummer van de gewenste optie

`Generate(regel)` maakt de code. `ApplyAsync(db, regel, code)` laadt de bijpassende opties uit de DB en koppelt ze aan de regel.

---

## 6. PrijsBerekening

`PricingEngine` (singleton) berekent de verkoopprijs van een `OfferteRegel`:

1. Lijst-bijdrage: `(oppervlakteM2 × PrijsPerMeter) + VasteKost` (TypeLijst)
2. Per afwerking: `(oppervlakteM2 × (1 + afval%) × KostprijsPerM2 × WinstMarge) + VasteKost`
3. Som → ExtraPrijs + Korting → SubtotaalExBtw → BTW → TotaalInclBtw
4. `PreviewPrijsText` in `AfwerkingenViewModel` toont live een voorbeeldberekening voor opgegeven breedte/hoogte.

---

## 7. ViewModels (23 stuks)

| ViewModel | Scherm | Bijzonderheden |
|---|---|---|
| `MainWindowViewModel` | MainWindow | singleton, `CurrentPage` eigenschap |
| `LoginViewModel` | LoginWindow | authenticatie |
| `HomeViewModel` | HomeView | dashboard-tiles, navigeert naar andere VM's |
| `KlantenViewModel` | KlantenView | CRUD klanten, zoeken |
| `KlantDetailViewModel` | KlantDetailView | detail + offertelijst per klant |
| `LijstenViewModel` | LijstenView | CRUD TypeLijsten, voorraadbeheer |
| `LeveranciersViewModel` | LeveranciersView | CRUD leveranciers |
| `AfwerkingenViewModel` | AfwerkingenView | CRUD + import; familie-bewust opslaan; `VolgnummerText` char-binding met toast-validatie |
| `OffertesLijstViewModel` | OffertesLijstView | lijst van offertes, navigeer naar detail |
| `OfferteViewModel` | OfferteView | hoofdscherm; laadt catalog, berekent prijzen; `RelinkSelectionsAfterCatalog()` relinkt alle 8 navigatie-properties |
| `WerkBonLijstViewModel` | WerkBonLijstView | overzicht werkbonnen, statusbeheer |
| `PlanningCalendarViewModel` | PlanningCalendarWindow | kalender met observable DayTile's, GeblokkeerdeDag, WerkTaak beheer |
| `PlanningTijdDialogViewModel` | PlanningTijdDialog | tijdsinvoer (delegate-injectie vanuit code-behind) |
| `WeekWerkLijstViewModel` | WeekWerkLijstWindow | weekoverzicht werktaken |
| `FacturenViewModel` | FacturenView | factuurlijst, statuswijzigingen |
| `FactuurPreviewViewModel` | FactuurPreviewWindow | PDF-preview factuur |
| `FactuurInfoDialogViewModel` | FactuurInfoDialog | factuurinfo invullen |
| `ExportCenterViewModel` | ExportCenterView | centrale Excel-export |
| `InstellingenViewModel` | InstellingenWindow | app-instellingen beheer |
| `ImportPreviewViewModel` | ImportPreviewView | generieke import-preview |
| `AfwerkingExcelPreviewViewModel` | AfwerkingImportPreviewWindow | Excel-import preview afwerkingen |
| `KlantExcelPreviewViewModel` | KlantImportPreviewWindow | Excel-import preview klanten |
| `BulkLijstenViewModel` | BulkLijstenWindow | bulkbewerking TypeLijsten |

### DI-registratie

- `MainWindowViewModel` → **singleton**
- Alle overige → **transient**

---

## 8. PlanningCalendarViewModel — Details

### DayTile (inner class)

```csharp
public partial class DayTile : ObservableObject
{
    [ObservableProperty] IBrush background;
    [ObservableProperty] IBrush border;
    [ObservableProperty] bool isSelected;
    public DateTime Date { get; init; }
}
```

Reactief dankzij `ObservableObject` — AXAML-binding pikt `Border`/`Background`-wijzigingen direct op zonder extra notify-calls.

### WeekRow (week-tabel rij)

6 kolommen: `BonNr`, `KlantNaam`, `Afmeting`, `Lijst`, `Dag`, `DuurMin`

### Geblokkeerde dagen

`GeblokkeerdeDag`-records worden bij `LoadAsync` in een `HashSet<DateTime> _geblokkeerd` geladen. `IsDagGeblokkeerd(date)` en `IsGeselecteerdeDagGeblokkeerd` sturen de UI-indicator en knop-tekst.

### Commando's

- `ToggleBlokkeerDagCommand` — blokkeert/deblokkert geselecteerde dag
- `ToggleBlokkeerWeekCommand` — blokkeert/deblokkert hele week
- `HerplanTaakCommand` — context-menu op WerkTaak
- `VerwijderTaakCommand` — context-menu op WerkTaak
- `ShowTijdDialogAsync` — `Func<…>` delegate geïnjecteerd vanuit `PlanningCalendarWindow.axaml.cs`

### Capaciteit

Constante `CapaciteitMinuten = 8 * 60 = 480` (één standaard werkdag).

---

## 9. Services

### Domeinservices (Scoped)

| Service | Verantwoordelijkheid |
|---|---|
| `AfwerkingenService` | CRUD + familie-sync prijsvelden + volgnummer-normalisering |
| `StockService` | Voorraadmutaties, alerts, berekeningen |
| `WorkflowService` | Algemene workflow-stappen |
| `OfferteWorkflowService` | Offerte statusovergangen |
| `WerkBonWorkflowService` | WerkBon aanmaken, status, stock-reservering |
| `FactuurWorkflowService` | Factuur aanmaken vanuit WerkBon, statusbeheer |
| `FactuurExportService` | PDF-generatie voor facturen (QuestPDF) |
| `CentralExcelExportService` | Excel-exports (EPPlus) |
| `PdfFactuurExporter` | Implementatie `IFactuurExporter` via QuestPDF |

### Singletons

| Service | Verantwoordelijkheid |
|---|---|
| `NavigationService` | Scherm-navigatie, `CurrentViewModel` |
| `OfferteNavigationService` | Offerte-specifieke navigatie |
| `PricingEngine` | Prijsberekeningslogica |
| `PricingService` | Wrapper rond PricingEngine |
| `AppSettingsProvider` | Leest/schrijft `Instelling`-records |
| `ToastService` | Huskui toast-notificaties (Info/Success/Warning/Error) |
| `DialogService` | Modal dialoogvensters |
| `WindowProvider` | Geeft toegang aan Avalonia Window-instantie |

### Transient

| Service | Verantwoordelijkheid |
|---|---|
| `KlantDialogService` | Klant selectie/aanmaak dialoog |
| `LijstDialogService` | TypeLijst selectie dialoog |
| `ImportService` | Generieke import-pipeline orchestratie |
| `ClosedXmlExcelParser` | Parset .xlsx bestanden |
| Klant/TypeLijst/AfwerkingsOptie import-stacks | Map + Validator + Committer per entiteitstype |

---

## 10. Import-pipeline

Generiek pipeline-patroon via interfaces:

```
IExcelParser              → parset rijen uit .xlsx
IExcelMap<T>              → kolom-naar-property mapping
IImportPreviewDefinition  → bouwt preview-rijen
IImportValidator<T>       → valideert elke rij
IImportCommitter<T>       → schrijft goedgekeurde rijen naar DB
IImportService            → orchestreert het hele proces
```

Preview-modellen: `KlantPreviewRow`, `TypeLijstPreviewRow`, `AfwerkingsOptiePreviewRow`
Resultaat-modellen: `ImportResult`, `ImportRowResult`, `ImportIssue`, `ImportRowIssue`, `ImportCommitReceipt`, `Severity`

---

## 11. Validatie

`ICrudValidator<T>` interface met drie methoden: `ValidateCreateAsync`, `ValidateUpdateAsync`, `ValidateDeleteAsync`.

`ValidationResult` kent **errors** (blokkerend) en **warnings** (niet-blokkerend, getoond als toast).

| Validator | Entiteit | Bijzonderheden |
|---|---|---|
| `KlantValidator` | Klant | naam, e-mail, BTW-formaat |
| `TypeLijstValidator` | TypeLijst | artikelnummer, prijzen |
| `AfwerkingsOptieValidator` | AfwerkingsOptie | Volgnummer `1-9`/`A-K`, Kleur max 50, unieke `(Groep+Volgnummer+Kleur)` DB-check; warnings voor 0-waarden |
| `OfferteValidator` | Offerte | klant, regels, afmetingen |

---

## 12. Data-laag

### AppDbContext

25 `DbSet`-properties. Sleutelconfiguraties in `OnModelCreating`:
- Precisies op alle decimale velden
- `Offerte 1:1 WerkBon` via `HasForeignKey<WerkBon>(w => w.OfferteId)`
- `OfferteRegel` → 6 × AfwerkingsOptie met `OnDelete = NoAction` (vermijdt multiple cascade paths)
- `AfwerkingsOptie` unieke samengestelde index `(AfwerkingsGroepId, Volgnummer, Kleur)`
- `SaveChangesAsync` override: auto-berekent `WerkTaak.GeplandTot` en stelt `WerkBon.BijgewerktOp` in

### DatabaseSeeder

Vult bij lege DB: 4 leveranciers (ICO/HOF/FRA/BOL), 3 klanten, 5 afwerkingsgroepen (G/P/D/O/R), 15 afwerkingsopties (3 per groep, met `Kleur = "Standaard"`).

`FixVolgnummers(db)` — idempotente herstel-methode die bij opstart controleert op `Volgnummer < ' '` (besturingstekens) en mapt naar correcte cijfertekens ('1'–'9', 'A'–'K').

### Migrations

Standaard EF Core migrations. `AppDbContextModelSnapshot.cs` aanwezig. `FactuurSchemaUpgrade.cs` bevat handmatige schema-upgrade logica voor de factuur-entiteiten.

---

## 13. Views & Theming

### Stijlen (`Styles/QuadroTheme.axaml`)

Globale stijlen en resources voor de hele app:
- `MainBtn` — gele actie-knop (`#F5C242`)
- `DangerBtn` — rode verwijder-knop
- Resources: `AccentYellow` (#F5C242), `AccentDark` (#444A50), `AccentRed`, `AccentRedDim`
- Header-patroon: `#444A50` achtergrond, wit logo + subtitel, gele "Terug"-knop rechts

### Hoofd-navigatievensters (UserControl, wisselen via ContentControl)

HomeView, KlantenView, LijstenView, LeveranciersView, AfwerkingenView, OffertesLijstView, OfferteView, WerkBonLijstView, FacturenView, ExportCenterView

### Dialoog- en popup-vensters (Window)

LoginWindow, KlantDialog, LijstDialog, InstellingenWindow, FactuurInfoDialog, FactuurPreviewWindow, ImportPreviewWindow, KlantImportPreviewWindow, AfwerkingImportPreviewWindow, BulkLijstenWindow, WeekWerkLijstWindow, PlanningCalendarWindow, PlanningTijdDialog

### PlanningCalendarWindow (herontworpen)

- 2-kolommen layout: links maandkalender-grid (DayTile's), rechts detail-paneel
- DayTile: goud border (`#F5C242`) bij selectie, DeepSkyBlue bij vandaag, grijs anders; `BorderThickness=2`
- Dag-detail (rechts): blokkeerknoppen bovenaan, werkbon-regels sectie, week-tabel (6 kolommen)
- Context-menu op WerkTaak-items: Herplan / Verwijder
- Code-behind injecteert `ShowTijdDialogAsync` delegate in ViewModel via `OnDataContextChanged`

---

## 14. Bekende kwesties & aandachtspunten

### Opgelost in recente sessies

| Probleem | Oplossing |
|---|---|
| Seeder `(char)1` besturingstekens als volgnummer | Vervangen door letterlijke char-constanten + `FixVolgnummers()` startup-fix |
| `RelinkSelectionsAfterCatalog()` relinkte slechts 2 van 8 navigaties | Uitgebreid naar alle 8 (TypeLijst + 6 afwerkingen + Klant) |
| Ongeldig volgnummer werd stil afgewezen | Toast-waarschuwing toegevoegd via `_toast.Warning(...)` |
| List-badge `char`-binding zonder StringFormat | `StringFormat='{}{0}'` toegevoegd |
| Debug `Console.WriteLine` in OfferteViewModel | Verwijderd |
| PlanningCalendar: linker-paneel en 16 tabel-kolommen | Herontwerp: linker paneel weg, 6 kolommen, DayTile reactief |

### Nog openstaande aandachtspunten

- **`Console.WriteLine` in App.axaml.cs** — DB-pad wordt gelogd bij opstart (`[DB] SQLite path = ...`). Onschadelijk maar verbose in productie; vervangen door `ILogger<App>` is netter.
- **Leverancier.Naam max 3 chars** — de 3-tekens beperking is strak; toekomstige leveranciers met langere namen vereisen een migratie.
- **`AvaloniaUseCompiledBindingsByDefault=true`** — alle bindingen zijn gecompileerd; `x:DataType` is verplicht in DataTemplates. Ontbrekende `x:DataType` geeft compile-fout.
- **`WorkflowService.Tests` map** — testsuite bestaat maar is uitgesloten van de build in `.csproj`. Mogelijk verouderd of nog in ontwikkeling.
- **`EfCore.SchemaCompare`** — dependency aanwezig (v8.2.0) maar gebruik niet zichtbaar in productiecode; waarschijnlijk voor ontwikkel-validatie.
- **`Microsoft.EntityFrameworkCore.SqlServer`** — aanwezig als dependency maar app gebruikt uitsluitend SQLite. Verwijderen bespaart ruimte bij publish.
- **`GeblokkeerdeDag.Reden`** — aanwezig in model maar niet in het blokkeer-dialoog getoond aan de gebruiker.

---

## 15. Verbeteringsideeën

1. **Unit tests heractiveren** — `WorkflowService.Tests` herstructureren als apart testproject
2. **Migrations via EF CLI verbeteren** — `FactuurSchemaUpgrade.cs` handmatige upgrade samenvoegen in standaard EF-migraties
3. **Leverancier.Naam uitbreiden** — max 3 chars → max 50 chars (+ migratie)
4. **SqlServer dependency verwijderen** — bespaart ~15 MB in de gepubliceerde executable
5. **Console.WriteLine uit App.axaml.cs** — vervangen door `ILogger<App>`
6. **GeblokkeerdeDag reden-veld** — tonen in blokkeer-UI zodat gebruikers een reden kunnen invullen
7. **WerkTaak.Resource** — vrij tekstveld (max 80 chars); eventueel koppelen aan een medewerker-entiteit

---

*Analyse gebaseerd op volledige codebase-scan van het project (april 2026)*
