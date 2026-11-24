# QuadroApp — analyse van de codebase en de repository (november 2026)

- **Datum:** 16 november 2026
- **Bron:** branch `dev` op commit `9fc6960` (PR #136). `main` staat op `c56e796` (PR #135, tag `v1.3.27`).
- **Aard:** enkel analyse. Er is geen code gewijzigd, niets gemerged en geen branch of tag verwijderd. Dit bestand is het enige dat gecommit is.
- **Omgeving:** Linux-container, .NET SDK 10.0.115. De clone was eerst *shallow*; de volledige geschiedenis is opgehaald met `git fetch --unshallow` voordat de git-cijfers berekend zijn.
- **Bronvermelding:** elke bewering heeft een `pad:regel` (stand `dev` `9fc6960`) of het commando met de uitkomst. Wat niet met zekerheid vast te stellen was, staat in hoofdstuk 8.

---

## 1. Samenvatting

**De 5 belangrijkste bevindingen**

1. **`dev` kan vandaag zonder conflicten naar `main`.** Het resultaat van de merge is exact de inhoud van `dev` (bewijs in 3.10). Negen bestanden van de oude schermen verdwijnen dan van `main`.
2. **De repository is publiek** en bevat het adres, de poort en de gebruikersnaam van de productiedatabase; het runbook raadt daarbij "Allowed IPs `0.0.0.0/0`" aan. Alleen het wachtwoord beschermt de database (3.8).
3. **Alle 562 testen draaien op SQLite of InMemory, geen enkele op PostgreSQL**, terwijl productie op PostgreSQL draait. Eén verschil is al zichtbaar: de bewaking tegen gelijktijdig wijzigen van een offerte werkt volgens het model niet op PostgreSQL (3.5).
4. **De code is gezonder dan verwacht:** build met 0 waarschuwingen, 562/562 testen groen in 38 s, geen kwetsbare pakketten, "factuur" is volledig uit de code. Dode code is beperkt tot ongeveer 1 000 regels.
5. **De schermlogica is de zwakke plek:** `OfferteViewModel` telt 1 902 regels en 18 afhankelijkheden, 52 databasecontexten worden in ViewModels geopend, en zes schermen overschrijven een hele rij met een kopie uit het geheugen (het patroon achter "offertenummer 0").

**De 5 voorstellen met de meeste winst**

1. `dev` nu naar `main` mergen, daarna pas refactoren (V-01).
2. Database afschermen: repo privé of gegevens weg, Allowed IPs beperken, wachtwoord vernieuwen (A-01).
3. CI herstellen: nieuw `RELEASE_PAT`, `fail-fast: false`, CI ook op `dev`, rendertool mee bouwen (V-03 t/m V-06).
4. 69 gemergede branches verwijderen en automatisch opruimen aanzetten (V-02).
5. Vangnet-testen schrijven en dan `OfferteViewModel` in kleine stappen splitsen, te beginnen met een pure verplaatsing naar partial-bestanden (V-20 t/m V-25).

---

## 2. Cijfers

### Bestanden en regels per project (`.cs` + `.axaml`, stand `dev`)

| Project | Bestanden | Regels | Opmerking |
|---|---:|---:|---|
| `QuadroApp` (app in de repo-root: Views, ViewModels, Service, Converters, Validation, Model, Styles, App, MainWindow, Program) | 318 | 39 051 | waarvan 59 `.axaml` met 8 934 regels |
| `QuadroApp.Data` zonder migraties | 54 | 4 066 | context, entiteiten, patchers, seeder |
| `QuadroApp.Data/Migrations` (SQLite) | 41 | 39 716 | 20 migraties + snapshot |
| `QuadroApp.Migrations.Npgsql` | 36 | 35 954 | 17 migraties + snapshot + design-time factory |
| `WorkflowService.Tests` | 76 | 11 456 | 67 testklassen in de root + 9 hulpklassen |
| `Tools/SchermAfbeeldingen` | 1 | 679 | staat niet in `Quadro.sln` |
| `Scripts/MigrationTool` | 1 | 213 | staat niet in `Quadro.sln` |

Per map in de app: Views 113 bestanden / 9 434 regels, ViewModels 51 / 14 084, Service 129 / 12 906, Converters 12 / 481, Validation 6 / 562. De repository telt 648 bestanden (`git ls-files | wc -l`).

Van de 75 670 regels migratiecode zijn er 67 844 gegenereerde `*.Designer.cs` (90 %).

### Grootste bestanden (`wc -l`)

| Regels | Bestand |
|---:|---|
| 1 902 | `ViewModels/Offerte/OfferteViewModel.cs` |
| 1 106 | `ViewModels/Verkoop/VerkoopDossierViewModel*.cs` (vier partial-bestanden: 752 + 221 + 82 + 51) |
| 782 | `App.axaml.cs` |
| 683 | `Service/BestelbonWorkflowService.cs` |
| 650 | `ViewModels/Planning/PlanningCalendarViewModel.cs` |
| 621 | `ViewModels/Afwerkingen/AfwerkingenViewModel.cs` |
| 612 | `Views/Offerte/OfferteRegelEditor.axaml` |
| 607 | `Service/PdfBestelbonExporter.cs` |
| 594 | `ViewModels/Leverancier/LeveranciersViewModel.cs` |
| 573 | `ViewModels/Lijsten/LijstenViewModel.cs` |
| 553 | `QuadroApp.Data/Data/DatabaseSeeder.cs` |
| 542 | `ViewModels/Excel/ExportCenterViewModel.cs` |

### Build en testen

| Commando | Uitkomst |
|---|---|
| `dotnet build Quadro.sln` (Debug) | 0 waarschuwingen, 0 fouten, 54 s |
| `dotnet build Quadro.sln -c Release` | 0 waarschuwingen, 0 fouten |
| `dotnet test WorkflowService.Tests/WorkflowService.Tests.csproj` | **562 geslaagd, 0 gefaald, 0 overgeslagen**, 38 s |
| `dotnet build Tools/SchermAfbeeldingen` | 0 waarschuwingen, 0 fouten |
| `dotnet build Scripts/MigrationTool` | 2 waarschuwingen, 0 fouten |
| `dotnet list Quadro.sln package --vulnerable --include-transitive` | geen kwetsbare pakketten in de 4 projecten |
| Extra analyse: `dotnet build QuadroApp.csproj -p:AnalysisMode=Recommended` | 229 unieke meldingen, zie 3.7 |

De testrunner meldt één opmerking: "VSTEST_ARTIFACTS_DIRECTORY … most likely caused by an outdated Microsoft.NET.Test.Sdk" (pakket staat op 17.12.0).

### Git

| | Aantal | Bron |
|---|---:|---|
| Commits (alle refs) | 580 | `git rev-list --count --all` |
| Commits op `dev` / `main` | 577 / 512 | `git rev-list --count` |
| Remote branches | 73 | `git ls-remote --heads origin \| wc -l` |
| waarvan werkbranches (zonder `main`, `dev`) | 71 | 46 `feature/`, 11 `fix/`, 9 `docs/`, 3 tijdelijke werkbranches, 1 `chore/`, 1 `anthonymohamedap-patch-1` |
| Gemerged in `main` én `dev` | 45 | `git branch -r --merged` |
| Gemerged in `dev`, nog niet in `main` | 24 | idem |
| Niet gemerged | 2 | zie 3.6 |
| Verlaten (ouder dan 30 dagen, niet gemerged) | 0 | oudste niet-gemergede branch is van 31 oktober |
| Open pull requests | 0 | GitHub, 16 november |
| Tags | 84 | `demo` + `v1.0.0` … `v1.3.27` |
| Grootte van `.git` | 13 MB | `du -sh .git` na volledige fetch |
| Migraties | 20 (SQLite) + 17 (Npgsql) | `ls …/Migrations` |

---

## 3. Bevindingen per onderdeel

### 3.1 Architectuur en structuur

**Mappen en lagen**

- De app zelf is geen map maar de **repo-root** (`QuadroApp.csproj` naast `README.md`). Daardoor staan broncode, scripts, docs en workflows door elkaar op het hoogste niveau.
- `QuadroApp.Data` is netjes afgescheiden (enkel EF-pakketten, geen verwijzing naar de app). De namespaces kloppen niet met de map: de entiteiten zitten in `QuadroApp.Model.DB`, niet in `QuadroApp.Data.…`, en er zijn twee schrijfwijzen door elkaar (`namespace X;` en `namespace X { }`).
- De laag `Service` kent de laag `ViewModels` op vier plaatsen: `Service/DialogService.cs` (maakt vensters en ViewModels aan, r. 44–196), `Service/PdfWeekLijstExporter.cs` (gebruikt `KlantWeekBlock` uit de ViewModels), `Service/NavigationService.cs` en `Service/OfferteNavigationService.cs`. De twee navigatieservices horen bij de UI; de PDF-exporter zou geen ViewModel-type mogen kennen.
- Veertien bestanden in `ViewModels` en `Service` verwijzen rechtstreeks naar Avalonia-controls of vensters (`new …Window(`, `using QuadroApp.Views`, `Avalonia.Controls`), onder meer `OfferteViewModel`, `OfferteWorkflowViewModel`, `OffertesLijstViewModel`, `PlanningCalendarViewModel`, `LijstenViewModel`, `VerkoopDossierViewModel`. Dat maakt die ViewModels moeilijk te testen zonder scherm.
- **Datatoegang zit voor een derde in de schermlogica:** `CreateDbContext` komt 52 keer voor in `ViewModels` en 96 keer in `Service` (`git grep -E "CreateDbContext(Async)?\("`). Koplopers: `PlanningCalendarViewModel` 7, `PlanningUitvoeringViewModel` 6, `OfferteViewModel` 6.
- Kleine onregelmatigheden: `Service/Import/interfaces/FilePickerService.cs` is een implementatie in een map voor interfaces (en de map heeft een kleine letter, naast `Service/Interfaces/`); `Service/Interfaces/IBetalingsOverzichtService.cs` bevat ook de modelklassen `BetaalwijzeTotalen`, `BetalingsOverzichtDag`, `BetalingsOverzicht` (r. 46–92); `Model/RegelPlanItem.cs` is een map met één bestand; `QuadroApp.Data/Model/DB/leverancier.cs` begint met een kleine letter; `ViewModels/Beginscherm.cs` is geen ViewModel.

**Te grote klassen**

- `OfferteViewModel` (1 902 regels): 18 constructor-parameters (`ViewModels/Offerte/OfferteViewModel.cs:904-921`), 13 commando's, 6 databasecontexten. De inhoud volgens de eigen kopjes:

  | Regels | Blok |
  |---|---|
  | 46–72, 255–665 | doorgeef-properties naar de geselecteerde inlijsting ("AXAML hoeft niet te wijzigen"), ± 450 regels, waarvan 14 keer dezelfde regel `if (!_suppressRecalc) Prijzen.TriggerRecalc();` |
  | 73–200 | voorschot ontvangen (US-69) |
  | 665–900 | afwerkingen in twee en drie niveaus |
  | 1094–1320 | catalogus en offerte laden |
  | 1342–1440 | status en vergrendelen (US-56, US-100, US-101) |
  | 1442–1580 | opslaan |
  | 1582–1837 | "Pricing helpers", 255 regels |
  | 1837–1902 | navigatie en databasehulpjes |

  Er bestaan al drie deel-ViewModels (`KlantSelectieViewModel`, `OffertePrijsViewModel`, `OfferteWorkflowViewModel`, aangemaakt op r. 932–953), dus de richting is ingezet.
- `VerkoopDossierViewModel` is al gesplitst in vier partial-bestanden, maar het hoofdbestand blijft 752 regels en bevat dubbel werk: `WerkBonDetailViewModel` wordt op twee plaatsen met dezelfde parameters aangemaakt (`VerkoopDossierViewModel.cs:627` en `.Stappen345.cs:70`), en `OfferteViewModel` en `BestelbonnenViewModel` worden elk op twee plaatsen uit de container gehaald (zie hieronder).
- `App.axaml.cs` (782 regels) doet vijf dingen: foutafhandeling, registratie van alle services, configuratie lezen, database opstarten en opstarttaken.

**Dependency injection**

- Service locator buiten de compositieroot (`git grep "App.Services\|GetRequiredService\|GetService"`):
  - `Views/InstellingenWindow.axaml.cs:18, 46, 55, 64, 73, 81, 82, 83` — acht keer `App.Services.GetRequiredService<…>()` in code-behind;
  - `Views/LijstenWindow.axaml.cs:12`, `MainWindow.axaml.cs:38`;
  - `ViewModels/HomeViewModel.cs:212` (`_services.GetRequiredService<InstellingenViewModel>()`);
  - `VerkoopDossierViewModel`: `_services?.GetService(typeof(OfferteViewModel))` op `.Nieuw.cs:22` en `.cs:548`, `typeof(BestelbonnenViewModel)` op `.cs:705` en `.Stappen345.cs:86`, `typeof(IPathOpener)` op `.Afdrukken.cs:37`. De parameter is optioneel (`IServiceProvider? services = null`, `.cs:71`): ontbreekt hij, dan doet de knop stil niets (`return`).
  - `NavigationService` en `OfferteNavigationService` halen ViewModels op via `IServiceProvider`; dat is voor navigatie aanvaardbaar.
- Services met `new` in plaats van injectie:
  - `new PdfBetalingsOverzichtExporter()` (`ViewModels/Betalingen/OverzichtBetalingenViewModel.cs:161`), `new PdfWeekLijstExporter()` (`ViewModels/Planning/WeekWerkLijstViewModel.cs:168`), `new PdfKlantenlijstExporter()` (`ViewModels/Klanten/KlantenViewModel.cs:283`), `new PdfWerkBonExporter()` (`VerkoopDossierViewModel.Afdrukken.cs:35`). Alleen `PdfBestelbonExporter` zit in de container (`App.axaml.cs:197`).
  - `new VerkoopDossierService(_factory, _workflow)` in `WeekWerkLijstViewModel.cs:198`, terwijl `IVerkoopDossierService` geregistreerd is (`App.axaml.cs:194`).
  - `Service/WorkflowService.cs:23-133` bevat drie hulpconstructors met ingebouwde `NoOpBestelbonWorkflowService`, `FallbackStockService` en `NoOpWerkBonArchiefService`. Ze worden alleen door de rendertool en de testen gebruikt (`Tools/SchermAfbeeldingen/Program.cs:58, 76, 231, 270, 366`): testhulp in productiecode.
- **"Scoped" bestaat in de praktijk niet.** Negentien services en validators zijn als `AddScoped` geregistreerd (`App.axaml.cs:183-198`, `238`, `239`, `241`), maar de container wordt gebouwd zonder scope-controle (`App.axaml.cs:270`) en ViewModels worden uit de root-container gehaald. Een scoped service uit de root leeft even lang als de app. Dat werkt omdat de services geen toestand bijhouden en zelf contexten uit de factory halen, maar de registratie zegt iets anders dan wat er gebeurt.
- Positief: nergens wordt een `AppDbContext` als veld bewaard (`git grep` op velden van type `AppDbContext`: 0 treffers). Alles loopt via `IDbContextFactory`.

### 3.2 Dode en dubbele code

Elke "ongebruikt"-claim is nagekeken met `git grep -w <naam> -- '*.cs' '*.axaml'` over de hele repo, inclusief testen en rendertool.

**Zonder gebruiker in productiecode**

| Wat | Bewijs | Regels |
|---|---|---:|
| Drie oude import-dialogen: `IDialogService.ShowImportPreviewAsync`, `ShowKlantImportPreviewAsync`, `ShowAfwerkingImportPreviewAsync` | enige treffers: interface (`Service/Interfaces/IDialogService.cs:13, 20, 25`), implementatie (`Service/DialogService.cs:33, 85, 179`), testfake en `OffertePrijsViewModelTests.cs:78-89`. De drie schermen gebruiken `ShowUnifiedImportPreviewAsync` (`AfwerkingenViewModel.cs:196`, `KlantenViewModel.cs:139`, `LijstenViewModel.cs:406`) | |
| … en wat alleen daardoor nog bestaat: `Views/ImportPreviewWindow.axaml(.cs)`, `Views/KlantImportPreviewWindow.axaml(.cs)`, `Views/AfwerkingImportPreviewWindow.axaml(.cs)`, `ViewModels/Excel/KlantExcelPreviewViewModel.cs`, `ViewModels/Excel/AfwerkingExcelPreviewViewModel.cs`, `KlantPreviewRow`, `AfwerkingsOptiePreviewRow`, `TypeLijstPreviewRow`, niet-generieke `ImportPreviewResult` | vensters worden alleen in `DialogService.cs:44, 95, 188` aangemaakt | ± 430 |
| `ViewModels/Leverancier/LeverancierBestellingViewModel.cs` | naam komt alleen in het eigen bestand voor | 219 |
| `Service/Toast/ToastColorConverter.cs` | alleen eigen bestand | 24 |
| `WerkBonStatusToBrushConverter` in `Converters/WerkBonStatusConverters.cs:14` | alleen eigen bestand | ± 25 |
| `QuadroApp.Data/Model/Import/ImportCommitResult.cs` | één treffer: de declaratie | 3 |
| NuGet-pakket **EPPlus 8.4.1** (`QuadroApp.csproj`) | `git grep "OfficeOpenXml"`: 0 treffers. Excel loopt via ClosedXML | |
| `EnsureImportAuditTablesAsync` (`Service/Import/ImportService.cs:190-236`) | maakt met ruwe SQL tabellen aan die al in de Baseline-migratie staan (`20260720093144_Baseline.cs:43, 167`); slaat zichzelf over op PostgreSQL (r. 192) | 46 |

`docs/cleanup-candidates.md` noemde deze import-dialogen al, maar verwijst ook naar `Views/LoginWindow.axaml` en `ViewModels/LoginViewModel.cs`, die niet meer bestaan.

**Ingebed in de app zonder gebruik**

`QuadroApp.csproj` neemt met `<AvaloniaResource Include="Assets\**\*" />` de hele map `Assets` op in `QuadroApp.dll`. De code gebruikt maar vier bestanden (`git grep "Assets/"`): `Quadro_logo2012_RGB.jpg`, `Quadro_logo2012_ZW1.jpg`, `hib logo 7.4 Kb.jpg`, `Guild Certified Framer logo 125px.png`. De vier `.eps`-bestanden in `Assets/DRUK` (samen 3,9 MB), `Assets/-Quadro_logo2012_GUIDE-.pdf` (0,9 MB) en `Assets/2.Voorbeeld huidige bestelbon (2).pdf` (0,2 MB) zitten wél in de binary: de Release-dll is 7,8 MB en bevat negen keer de tekst `%!PS-Adobe` en de bestandsnamen. In hetzelfde projectbestand staan nog restjes: `<None Remove="Assets\JPEG\Quadro_logo2012_RGB.jpg" />` (die map bestaat niet) en `<Page Include="Assets\quadro_building.png" />`.

**Na US-95**

De views zelf hebben allemaal nog een gebruiker (elke view wordt minstens één keer in productiecode aangemaakt of in een andere view opgenomen). De onderdelen van het oude offertescherm leven verder in het dossier (`Views/VerkoopDossierView.axaml:234-254`). Een zoektocht naar commando's zonder knop leverde niets op.

**Gedupliceerde logica**

- **Klantnaam:** `Klant.VolledigeNaam` bestaat (`QuadroApp.Data/Model/DB/Klant.cs:15`), maar de naam wordt op acht plaatsen opnieuw samengesteld met vier verschillende terugvalteksten: `BestelbonWorkflowService.cs:395` ("Onbekende klant"), `VerkoopDossierService.cs:300` ("zonder klant"), `VerkoopOverzichtService.cs:216` (leeg), `WerkBonDetailViewModel.cs:238` ("—"), `OfferteArchiefService.cs:83`, `WerkBonArchiefService.cs:91`, `PdfKlantenlijstExporter.cs:139`, `KlantenViewModel.cs:396`.
- **Betaalstand ("nog te betalen"):** vijf varianten: `Bestelbon.RestTeBetalen` (`Bestelbon.cs:115`, zonder latere betalingen), `Offerte.cs:109`, `IOntvangstService.cs:56` (met betalingen, afgerond, minimaal 0), `PdfBestelbonExporter.cs:305` (zelfde formule opnieuw), `VerkoopOverzichtService.cs:211-212`.
- **Totalen van de bestelbon:** de som van de lijnen wordt op drie plaatsen berekend: `Bestelbon.cs:103-112`, `BestelbonWorkflowService.cs:361-390`, `PdfBestelbonExporter.cs:65-66, 318`.
- **Uitvoermap voor PDF's:** vijf exporters bouwen elk zelf een pad op `MyDocuments` (zie 3.3).
- **Testhulp:** zie 3.4.

**Oude namen**

- "Factuur" is weg uit de code: `git grep -il factuur -- '*.cs' '*.axaml'` geeft buiten de migraties alleen twee commentaarregels (`SqliteSchemaPatcher.cs:135`, `Scripts/MigrationTool/Program.cs:37`) en één test. Er bestaat geen `PdfFactuurExporter`.
- "Bestelling" voor een bevestigde offerte is weg uit de code: de enum heet `VerkoopStapFlags.Werkbon` (`VerkoopStapFlags.cs:10`) en `BevestigAlsBestelling` komt niet meer voor. Op het scherm staat "bestelling" alleen nog bij leveranciers (`LeveranciersView.axaml`, `WerkBonTaken.axaml`, `WeekWerkLijstWindow.axaml`), zoals afgesproken.
- "KantKlaarKader" tegenover "standaardkader": 223 voorkomens in 33 bestanden, inclusief de entiteit en dus de tabelnaam. In de `.axaml`-bestanden is er nog één treffer op "kant-en-klaar"; er is niet nagekeken of dat zichtbare tekst is. Hernoemen in de code alleen is veel werk voor weinig winst; hernoemen in de database vraagt een migratie (zie 7).

### 3.3 Patronen met bugrisico

Geen van deze punten is aangepast.

**`IsBusy`-bewakers**

Er zijn 26 bewakers (`git grep "IsBusy) return\|if (IsBusy)"`). Met een script is elke herlaad-aanroep gezocht die gebeurt terwijl `IsBusy` nog aan staat (20 treffers) en daarna per aangeroepen methode nagekeken of die zelf op `IsBusy` terugkeert.

- Eén geval heeft nog het oude patroon: `ViewModels/Lijsten/BulkLijstenViewModel.cs:228` roept `LoadAsync()` aan binnen `IsBusy = true` (r. 214), en `LoadAsync` keert op r. 117 meteen terug. In de praktijk wordt dit pad niet bereikt, omdat `LijstenViewModel.cs:547` altijd `RefreshRequested` invult. Het is dus dode, maar foute code.
- Verwant risico: `LeveranciersViewModel.LoadLeveranciersAsync` (r. 245) en `ExportCenterViewModel.LaadConfiguratieAsync` (r. 390) zetten zelf `IsBusy = true` en in hun `finally` weer op `false`, terwijl ze worden aangeroepen vanuit een methode die `IsBusy` al aan had (`LeveranciersViewModel.cs:365, 444`; `ExportCenterViewModel.cs:325-355`). Daardoor gaat de vlag uit vóór de buitenste actie klaar is en kan de gebruiker een tweede actie starten.
- De andere treffers zijn in orde: de aangeroepen methode heeft geen bewaker (`AfwerkingenViewModel.LoadOptiesAsync`, `OfferteViewModel.LoadAsync`, `KlantenViewModel.LoadAsync` gebruikt een aparte vlag `IsLoading`).
- De structurele oorzaak blijft: elke ViewModel beheert zijn eigen vlag met de hand. Eén gedeelde hulpmethode ("voer uit en herlaad daarna") zou het patroon onmogelijk maken.

**Fire-and-forget**

- `AsyncViewModelBase.RunAsync` (`ViewModels/AsyncViewModelBase.cs:21-26`) toont bij een fout een toast, maar **schrijft niets naar het logboek of naar de foutentabel**. 18 aanroepen in 9 ViewModels. Gooit `taskFactory()` al vóór de eerste `await`, dan valt de fout buiten de `ContinueWith`.
- 13 keer `_ = …Async()`. De meeste methodes vangen hun fouten zelf op. Twee niet:
  - `VerkoopDossierViewModel.NaBevestigenAsync` (`.cs:563`, 8 regels, geen `try`), gestart op `.cs:552` en `.Nieuw.cs:31`;
  - `VerkoopDossierViewModel.VernieuwNaPlanningAsync` (`.cs:670`): `try` zonder `catch`, gestart op `.cs:667`.
  Een fout daarin komt pas bij de garbage collector boven als "UnobservedTaskException" (`App.axaml.cs:88-92`), zonder melding voor de gebruiker.
- 9 keer `async void`, allemaal in code-behind. Zes hebben geen `try/catch`: `InstellingenWindow.axaml.cs:42, 51, 60, 69`, `PlanningDagDetails.axaml.cs:19, 26`, `WerkbonPlanner.axaml.cs:37, 54`. Een fout daar gaat naar de algemene vanger van de UI-thread en verschijnt als onverwachte fout in `crash.log`.
- `ToastService` post altijd naar de UI-thread (`Service/Toast/ToastService.cs:24-31`), dus toasts vanaf een achtergrondthread zijn veilig.

**DbContext-gebruik**

- Stub-update in `OfferteViewModel.cs:1492-1522`: een nieuw `Offerte`-object met een handmatige lijst van 15 velden wordt aan de context gehangen. De fix voor "offertenummer 0" markeert nu alleen die velden. Het risico blijft dat een nieuw veld op `Offerte` vergeten wordt in de lijst en dan nooit bewaard wordt.
- **Hele rij overschrijven vanuit het geheugen** (`Attach` + `State = Modified`), hetzelfde soort patroon, op vijf plaatsen: `KantKlaarKaderenViewModel.cs:139-140`, `KlantenViewModel.cs:366-367`, `LijstenViewModel.cs:360-361`, `OfferteRegelViewModel.cs:469-470`, `WinkelVerkopenViewModel.cs:310-311`. Elke kolom wordt weggeschreven met wat het scherm toevallig in het geheugen heeft. Voor `TypeLijst` is dat extra gevoelig, omdat de voorraad in dezelfde rij staat. `LeveranciersViewModel.cs:357-358` doet het wél per veld.
- Nieuwe offerte: twee keer `SaveChangesAsync` zonder transactie (`OfferteViewModel.cs:1476-1487`). Valt de verbinding ertussen weg, dan bestaat de offerte zonder inlijstingen.
- `OfferteViewModel.cs:1303` maakt een context aan en geeft hem door zonder hem vrij te geven (`await _dbFactory.CreateDbContextAsync()` als argument; `LoadBestelbonContextAsync` op `OfferteWorkflowViewModel.cs:151` sluit hem niet).
- `AppDbContext` overschrijft alleen `SaveChangesAsync` (`AppDbContext.cs:488`). De synchrone `SaveChanges()` slaat de audit, `GeplandTot` en `BijgewerktOp` over. Nu gebruikt alleen `DatabaseSeeder` de synchrone variant (zes keer), dus er is geen fout, maar het is een valkuil.
- Audit-fouten worden ingeslikt (`AppDbContext.cs:523-527`, bewust: "audit mag een geslaagde save nooit laten falen"), zonder logregel.
- **Offertenummer:** `OfferteNummering.VolgendeAsync` neemt het hoogste nummer plus één (`Service/OfferteNummering.cs`), en de index op `OfferteNummer` is bewust niet uniek (`AppDbContext.cs:215-217`). Twee pc's die tegelijk een nieuwe offerte bewaren, kunnen hetzelfde nummer krijgen. Er zijn al drie datamigraties geweest om nummer 0 te herstellen (`OfferteNummerNulCorrigeren`, `OfferteNummerNulNaArchief`, `OfferteNummerNulNaOpslaan`).
- `AsNoTracking` wordt bij lezen ruim gebruikt; er is geen systematisch gemis gevonden.

**Paden op `MyDocuments`**

| Plaats | Terugval als de map leeg is? |
|---|---|
| `Service/PdfWerkBonExporter.cs:40-43` | ja, naar de thuismap, met `Path.GetFullPath` |
| `Service/BestelbonExportService.cs:81-82` | nee |
| `Service/PdfBetalingsOverzichtExporter.cs:43-45` | nee |
| `Service/PdfKlantenlijstExporter.cs:24-26` | nee |
| `Service/PdfWeekLijstExporter.cs:27-29` | nee |

Zonder terugval wordt het pad relatief (`QuadroApp/…` onder de werkmap van het proces).

**Cultuur en afronding**

- De cultuur wordt één keer centraal gezet (`Program.cs:32-36`, nl-BE). Daarnaast maken vijf bestanden hun eigen `CultureInfo("nl-BE")`. De analyzer telt 39 plaatsen waar een opmaak- of parse-aanroep geen cultuur meekrijgt (CA1305) en 10 hoofdletter-conversies zonder cultuur (CA1304/CA1311). Zolang de centrale instelling blijft staan, is dat geen fout.
- `PasswordHasher.cs:32` en `ImportService.cs:45` gebruiken `int.Parse`/`int.TryParse` zonder cultuur; voor gehele getallen zonder scheidingsteken is dat onschuldig.
- **Twee afrondingsregels door elkaar.** Van de 58 `Math.Round`-aanroepen in productiecode gebruiken er 52 de standaard van .NET (halve cent naar het dichtste even getal), waaronder `PricingEngine` (7), `BestelbonWorkflowService` (13), `OntvangstService` (6) en `PdfBestelbonExporter` (5). Zes aanroepen, plus zeven `decimal.Round`, ronden "weg van nul" af: `Ontvangst.cs:75` (totaal van een winkelverkoop), `StockReservatieService.cs:123`, `WerkBonWorkflowService.cs:340, 418, 423`, `BulkLijstenViewModel.cs:365-392`, `LeveranciersViewModel.cs:557`. Een bedrag dat precies op een halve cent uitkomt, kan dus in een winkelverkoop anders afgerond worden dan op een bestelbon. Elke wijziging hieraan verandert bedragen en hoort onder "Vraagt akkoord"; `PricingEngine` blijft buiten schot.

**Threading**

- PDF maken gebeurt op twee plaatsen op de UI-thread: `WeekWerkLijstViewModel.cs:168-169` en `VerkoopDossierViewModel.Afdrukken.cs:35`. Elders gebeurt het met `Task.Run` (`KlantenViewModel.cs:284`, `OverzichtBetalingenViewModel.cs:163`). Bij een grote weeklijst bevriest het venster even.
- `ConfigureAwait(false)` komt alleen voor in de foutlog-code (10 keer); ViewModels gebruiken het niet, wat juist is.
- Drie klassen houden een `CancellationTokenSource` of semafoor bij zonder `Dispose` (CA1001): `FoutRegistratie.cs:20`, `ImportPreviewViewModel.cs:17`, `OffertePrijsViewModel.cs:11`.

**Query-filters**

`AppDbContext.cs:61-68` verbergt gearchiveerde klanten, lijsten, standaardkaders, leveranciers en afwerkingen. De verwijzingen vanuit een offerte zijn optioneel (`Offerte.KlantId`, `OfferteRegel.TypeLijstId` zijn `int?`), dus een oude offerte met een gearchiveerde lijst blijft zichtbaar maar toont die lijst als leeg. `VoorraadMutatie` en `LeverancierBestelLijn` hebben een verplichte verwijzing naar `TypeLijst` én een filter daarop: die rijen verdwijnen uit elke lijst zodra de lijst gearchiveerd is. Of dat overal de bedoeling is, staat in 8.

### 3.4 Tests

**Goed gedekt** (op basis van klassenamen die in de testen voorkomen): prijsberekening (`PricingEngineTests`, 470 regels), de workflowservices, het verkoopdossier (`VerkoopDossierServiceTests`, 24 testen), migraties en datamigraties (`MigrationSafetyTests`, `BestelbonMigratieTests`, `FoutLogMigratieTests`, `BetaalwijzeTests`), model-drift voor SQLite (`ModelDriftTests`), beveiliging (`AuthServiceTests`, `SecretStoreTests`, `DevDatabaseGuardTests`), foutlog en filter, Excel-export, backup.

**Zonder enige test** (naam komt nergens in `WorkflowService.Tests` voor):

- ViewModels: `AfwerkingenViewModel` (621 regels), `LeveranciersViewModel` (594), `LijstenViewModel` (573), `KlantenViewModel` (482), `PlanningUitvoeringViewModel` (454), `WerkBonDetailViewModel` (340), `ImportPreviewViewModel` (281), `MainWindowViewModel`, `ArchiefViewModel`, `OverzichtBetalingenViewModel`, `KlantSelectieViewModel`. In totaal komen 21 van de 51 ViewModel-bestanden in een test voor.
- Services: `WerkBonArchiefService` (374), `VoorraadAlertService`, `StockReservatieService`, `LeverancierBestelService`, de Excel-maps voor import (`AfwerkingsOptieExcelMap`, `TypeLijstExcelMap`, `KlantExcelMap`).
- **Drie van de vijf PDF-exporters:** `PdfWeekLijstExporter` (277), `PdfBetalingsOverzichtExporter` (225), `PdfKlantenlijstExporter` (193).
- **PostgreSQL:** `git grep -l UseNpgsql -- WorkflowService.Tests` geeft niets. De drift-test zegt zelf dat ze alleen SQLite dekt en dat Npgsql met de hand moet (`ModelDriftTests.cs:11-13`). CI doet die controle niet.

**Infrastructuur**

- De QuestPDF-licentie wordt in vier testklassen apart gezet (`BestelbonAfdrukkenTests.cs:17`, `BestelbonBetalingenPdfTests.cs:76`, `BestelbonVolgnummerTests.cs:117`, `VerkoopDossierServiceTests.cs:300`). Een nieuwe PDF-test die dat vergeet, slaagt of faalt afhankelijk van welke test eerst liep.
- `TestToastService` bestaat drie keer: `TestInfrastructure/TestToastService.cs`, en privé in `OffertePrijsViewModelTests.cs:96` en `WorkflowServiceTests.cs:681`.
- Twee soorten testdatabase: 13 testbestanden gebruiken EF InMemory, 40 gebruiken SQLite. InMemory controleert geen verwijzingen, geen unieke indexen en geen gelijktijdigheid.
- Zeven testklassen delen statische toestand en zijn daarom in een collectie gezet (`[Collection("StaticAuthState")]`, `[Collection("CrashContext")]`); de oorzaak is `AuditContext._currentUserName` (`QuadroApp.Data/Data/AuditContext.cs:11`) en `CrashContext`. Dat is correct opgelost, maar elke nieuwe test die de aangemelde gebruiker zet, moet in dezelfde collectie.
- De rendertool verwijst naar het testproject voor zijn fakes (`SchermAfbeeldingen.csproj`), staat niet in de solution en wordt dus niet door CI gebouwd. Hij bouwt nu wel (0 fouten).

**Snelheid:** de som van alle testen is 85,5 s, de wandkloktijd 38 s. `VoorschotVerwijderenCrashTests` neemt 19,4 s voor 24 testen (draait bewust zonder parallellisme). De traagste losse test duurt 2,8 s. Snelheid is geen probleem.

**Eerst te schrijven, vóór de refactors**

1. Karakteriseringstest voor `OfferteViewModel`: laden, één veld per groep wijzigen, bewaren, opnieuw laden en álle kolommen van `Offerte` en `OfferteRegel` vergelijken (vangt een vergeten veld in de stub-lijst).
2. Test dat de doorgeef-properties van `OfferteViewModel` een `PropertyChanged` geven wanneer de geselecteerde inlijsting wijzigt (lijst uit r. 880–900).
3. Tekstvergelijking van de drie ongeteste PDF's (inhoud via `pdftotext`) plus een rendervergelijking.
4. Eén gedeelde plek voor de QuestPDF-licentie in het testproject.
5. Een rendervergelijking als vaste stap: alle scenario's renderen op de branch vóór en na, en de PNG's pixel per pixel vergelijken.

### 3.5 Database en migraties

- **Aantal:** 20 migraties voor SQLite (Baseline van 20 juli 2026 + 19), 17 voor Npgsql (Baseline van 4 augustus 2026 + 16). Sinds de Npgsql-Baseline lopen beide reeksen gelijk: dezelfde 16 namen, enkele seconden na elkaar.
- **Snapshots consistent:** beide snapshots bevatten 53 entiteitsblokken. Een vergelijking van alle properties en indexen geeft precies drie verschillen: de kolom `xmin` op `Bestelbon`, `TypeLijst` en `WerkBon` in Npgsql. Dat is bewust (`AppDbContext.cs:458-486`).
- **Gelijktijdig wijzigen op PostgreSQL:** vijf entiteiten hebben `[Timestamp] RowVersion` (`Bestelbon.cs:129`, `Offerte.cs:27-28`, `TypeLijst.cs:110`, `WerkBon.cs:36`, `WerkTaak.cs:94-95`). Op PostgreSQL krijgen er maar drie `xmin`. Voor `Offerte` en `WerkTaak` blijft `RowVersion` in de Npgsql-snapshot een `bytea`-kolom die als token dient, en het commentaar in `AppDbContext.cs:460-462` zegt zelf dat PostgreSQL zo'n kolom niet bijwerkt. In de Npgsql-Baseline en `PostgresSchemaPatcher` staat geen trigger. Als dat klopt, wordt een gelijktijdige wijziging van een offerte in productie nooit opgemerkt, terwijl `OfferteViewModel.cs:1562-1569` daar wel op rekent. Zie 8.
- **Migraties uit volgorde na de merge:** `dev` voegt `20261102212044_Verkoopdossier` en `20261108153507_Momentopname` toe. De productiedatabase heeft via `main` al latere migraties gekregen (tot `20261112140204`). EF past ontbrekende migraties toe ongeacht hun datum, en beide voegen alleen kolommen toe aan `Offertes`, dus dit zou moeten werken. Het is niet getest op een kopie van productie.
- **Squashen:** niet nodig om technische redenen. Winst zou zijn: ± 68 000 regels gegenereerde code minder. Risico: de geschiedenistabel van elke bestaande database moet de nieuwe Baseline als "toegepast" krijgen; de logica daarvoor bestaat (`App.axaml.cs:676-680`, `PostgresSchemaPatcher`, `SqliteSchemaPatcher`), maar een fout daar raakt productie. Advies: pas overwegen nadat `dev` in productie staat en bevestigd is welke databases er bestaan. `Scripts/regen-baseline.ps1` dekt alleen SQLite.
- **Herstelcode naast migraties:** `SqliteSchemaPatcher.cs` (322 regels, ± 45 ruwe SQL-opdrachten), `BestelbonSchemaUpgrade.cs` (115) en `PostgresSchemaPatcher.cs` (92) draaien bij elke start. De SQLite-patcher herstelt databases van vóór de Baseline. Of er zulke databases nog bestaan, is de vraag (zie 8).
- **Decimale velden** (42 kolommen in de Npgsql-snapshot): 14 × `numeric(18,2)`, 11 × `numeric(10,2)`, 4 × `(5,2)`, 2 × `(6,3)`, 1 × `(18,4)` (`KantKlaarKader.PrijsPerStukExcl`, bewust sinds 12 november). Afwijkend: `OfferteRegel.InlegBreedteCm` en `InlegHoogteCm` zijn `numeric` zonder precisie (snapshot r. 1031–1035), en zeven decimale properties op `OfferteRegel` hebben geen `[Precision]` in het model (`BreedteCm`, `HoogteCm`, `InlegBreedteCm`, `InlegHoogteCm`, `ExtraPrijs`, `Korting`, `KortingPct`). Geen fout, wel inconsistent.
- **Indexen:** 60 in de snapshot; de nagekeken verwijzingen (op `Offerte`, `OfferteRegel`, `Ontvangst`, `WerkBon`, `WerkTaak`) hebben een index. `Offerte.OfferteNummer` en `WerkBon.OfferteId` zijn niet uniek.
- **Excel-export en archief bewaren statussen als enumnaam** (`Status.ToString()` op 15 plaatsen, o.a. `CentralExcelExportService.Offertes.cs:32`, `OfferteArchiefService.cs:88`, `WerkBonArchiefService.cs:95-96`). In het archiefscherm wordt die tekst getoond (`ArchiefView.axaml:122, 240`, `StatusOpMoment`). Dat botst met de afspraak "nooit de enumnaam", maar aanpassen verandert wat de gebruiker ziet en wat bewaard wordt.

### 3.6 Git en repository

**Branches**

- Van de 71 werkbranches zijn er 69 volledig opgenomen in `dev` (45 daarvan ook in `main`). De volledige lijst met laatste commit staat in hoofdstuk 6.
- Twee branches zijn niet gemerged, en geen van beide bevat nog iets dat ontbreekt:
  - `feature/us-86-home-blocks-wba2ws` (`525d848`): de top is een merge-commit van PR #103 binnen de branch zelf. Beide ouders (`94545eb`, `149d6dd`) zitten in `main` en `dev`; `git cherry origin/dev …` toont geen eigen wijzigingen.
  - `docs/us99-factuur-uit-de-code` (`b5c8257`): één commit met de eerste versie van `docs/backlog/US-99_Factuur_uit_de_code.md`. `dev` heeft een nieuwere versie van datzelfde bestand (laatst gewijzigd in `a620033`; de branch mist 21 regels).
- Er zijn geen echt verlaten branches en geen open pull requests.
- Voor elke hotfix bestaan twee branches en twee PR's: één naar `main` en een tweeling met `-dev` naar `dev` (`fix/feedback-veerle-0911` / `-dev`, `fix/feedback-veerle-1211` / `-dev`, `feature/foutlog-in-db` / `-dev`, `fix/voorschot-verwijderen-crash` / `-dev`). Dat verklaart een deel van de drukte en verdwijnt zodra `dev` in `main` zit.
- `main` heeft geen branchbeveiliging (GitHub-API: "Branch not protected").

**Tags en releases**

- 84 tags: `demo` en `v1.0.0` tot `v1.3.27`, zonder gaten. Niets op te ruimen. `<Version>1.1.0</Version>` in `QuadroApp.csproj:10` loopt achter; de echte versie komt uit de tag (`build.yml:75`).
- Elke release bevat `MigrationTool-osx-arm64` van 82 MB (assets van `v1.3.27`), een hulpmiddel voor de eenmalige overstap van SQLite naar PostgreSQL (`build.yml:138-154`).

**Grote bestanden**

- De hele `.git` is 13 MB. De grootste objecten in de geschiedenis: `docs/QuadroApp_Handleiding.pdf` 1,5 MB, twee versies van `QuadroApp_Handleiding_v2.docx` (1,3 en 1,1 MB), `QuadroApp_Handleiding_v2.pdf` 1,2 MB, vier `.eps` van 0,9 tot 1,1 MB, `Assets/-Quadro_logo2012_GUIDE-.pdf` 0,9 MB.
- **Git LFS of de geschiedenis herschrijven is niet zinvol.** De winst is hoogstens enkele MB; herschrijven verandert alle commit-hashes, breekt de 84 tags en de links in PR's, en verplicht iedereen opnieuw te clonen. Wel zinvol: de drukbestanden uit de app-binary halen (3.2) en ze eventueel naar een map buiten `Assets` verplaatsen.

**`.gitignore`**

- In de geschiedenis zit geen enkele `bin/`, `obj/`, `.db`, `.sqlite`, `.dll`, `.exe` of `appsettings.json` (gezocht over alle objecten van alle refs).
- Wel in de repo en beter niet: `docs/.~lock.QuadroApp_Handleiding_v2.pdf#` (een lockbestand van LibreOffice), twee versies van de handleiding in twee formaten (`docs/QuadroApp_Handleiding.docx/.pdf` en `_v2.docx/.pdf`, samen ± 4 MB), `docs/backlog/QuadroApp_Backlog.docx`.
- `appsettings.example.json` is op 27 september verwijderd (`b8d6679`), maar `README.md` verwijst er nog naar als sjabloon.

**Werkwijze**

- De afspraak "één story per branch" wordt gevolgd; 184 van de laatste 200 commits (zonder merges) volgen het conventionele formaat. Over de hele geschiedenis voldoen 252 van de 406.
- `README.md` zegt "Feature-branch vanaf `main`". De werkelijke flow (features naar `dev`, hotfixes dubbel) staat nergens beschreven.

### 3.7 CI/CD en configuratie

**`auto-tag.yml`**

- De laatste twaalf runs: tien mislukt (run 82 van 31 oktober tot run 93 van 13 november), twee staan sinds 11 november op "queued" (runs 91 en 92). De fout zit in de checkout-stap: `fatal: could not read Username for 'https://github.com': terminal prompts disabled` (log van run 43416736441). De workflow gebruikt `secrets.RELEASE_PAT || github.token` (r. 28): een secret dat bestaat maar ongeldig is, wint van de terugval. Dat past bij een verlopen token.
- De tags `v1.3.26` en `v1.3.27` bestaan toch, dus die zijn met de hand gezet.
- De foutmelding verschijnt pas na drie pogingen van de checkout en zegt niets over het token.

**`build.yml`**

- Geen `fail-fast: false` in de matrix (r. 22–41). Bij `v1.3.27` was de eerste poging: macOS mislukt, Windows geannuleerd. De tweede poging (13 november, 14:43–14:46 UTC) slaagde voor beide en de release is gepubliceerd met bestanden voor Windows en macOS.
- De release-build draait geen testen. Een tag die met de hand op een willekeurige commit gezet wordt, levert zonder controle een installer op.
- Commentaar "── 2. .NET 9" boven een stap die .NET 10 installeert (r. 48–51).
- De notarisatie geeft bij een fout de ruwe uitvoer van `vpk`; er is geen stap die vooraf nakijkt of het Apple-account in orde is.
- `actions/checkout@v4` geeft in elke run de waarschuwing dat Node.js 20 verouderd is.

**`ci.yml`**

- Draait bij push op `main`, `feature/**`, `fix/**` en bij PR's naar `main` (r. 5–8). **Niet** bij push op `dev`, niet bij PR's naar `dev`, niet op `docs/**` of `chore/**`. Een `-dev`-tweeling wordt alleen getest als de naam met `feature/` of `fix/` begint.
- De rendertool en de MigrationTool worden niet gebouwd (staan niet in de solution).
- Op 11 november staan zeven CI-runs zonder uitkomst.

**Qodana**

- De workflow is in GitHub handmatig uitgeschakeld (`disabled_manually`). Ze verwijst naar een branch `styling` die niet bestaat. `qodana.yaml` is de standaardsjabloon.

**Pakketten** (`dotnet list package --outdated` en `--deprecated`)

- **Velopack 0.0.869 staat op NuGet als verouderd met reden "CriticalBugs"**; de nieuwste is 1.2.161. Dit is het updatemechanisme van de app.
- `Avalonia.Diagnostics` en `xunit` 2.x zijn als "Legacy" gemarkeerd.
- Grote sprongen beschikbaar: Avalonia 11.3.12 → 12.1.6, EF Core 9.0.9 → 11.0.0, Npgsql-provider 9.0.4 → 10.0.3, QuestPDF 2024.7.1 → 2026.9.2 (licentievoorwaarden nakijken), Huskui.Avalonia 0.10.3 → 2.2.1.
- Kleine sprongen: CommunityToolkit.Mvvm 8.4.0 → 8.4.2, ClosedXML 0.105.0 → 0.105.1, SQLitePCLRaw 3.0.3 → 3.0.5, Microsoft.NET.Test.Sdk 17.12.0 → 18.12.0.
- De versies van EF Core en SQLitePCLRaw staan in vier projectbestanden apart.
- `Tmds.DBus.Protocol 0.21.3` staat als directe verwijzing zonder commentaar en wordt nergens in de code gebruikt; vermoedelijk een beveiligingspin.

**Waarschuwingen van de compiler**

De gewone build is schoon. Met de aanbevolen analyzers erbij komen 229 meldingen naar boven: 64 × CA1848 en 34 × CA1873 (logging-prestaties), 39 × CA1305 (cultuur), 17 × CA1862, 16 × CA1822, 12 × CA1805, 3 × CA1001 (zie 3.3). Geen enkele is een fout; CA1305 en CA1001 zijn de moeite om na te lopen. In `.editorconfig` staat `CS8629` (mogelijk `null`) uitgeschakeld voor alle C#-bestanden.

### 3.8 Beveiliging en privacy (alleen melden)

1. **De repository is publiek** (GitHub-API: `private=false`).
2. **Adres van de productiedatabase staat erin:** `docs/CLOUD_ROLLOUT_RUNBOOK.md:11` en `:53` en `WorkflowService.Tests/DevDatabaseGuardTests.cs:9` bevatten host, poort, databasenaam en gebruikersnaam. Het runbook noemt dat "niet geheim". Het staat sinds twee commits in de geschiedenis (`git log -S`).
3. **Het runbook raadt Allowed IPs `0.0.0.0/0` aan**, op r. 20 als standaard en op r. 90 als oplossing bij verbindingsproblemen. Samen met punt 1 en 2 is het wachtwoord van de gebruiker `quadro` de enige bescherming tegen iedereen op het internet.
4. De verbinding gebruikt `SSL Mode=Require`. Dat versleutelt, maar controleert het certificaat van de server niet.
5. **Geen wachtwoorden in de repo of in de geschiedenis.** Gezocht op `Password=` over alle commits: alleen plaatshouders (`__SECRET__`, `CHANGE_ME`) en een lokaal ontwikkelwachtwoord `postgres`/`postgres` in het US-41-plan. `appsettings.json` staat in `.gitignore` en zit niet in de geschiedenis. Het databasewachtwoord wordt versleuteld per pc bewaard (`Service/Security/SecretStore.cs`).
6. **Standaard-admin:** het wachtwoord `quadro` staat in `Service/Security/AuthService.cs:20` en in `docs/AUTH.md:12`. De app verplicht een wijziging bij de eerste login (`MoetWachtwoordWijzigen`, `AuthService.cs:180`). Wachtwoorden worden gehasht met PBKDF2-SHA256, 210 000 iteraties (`PasswordHasher.cs:13`).
7. **Foutlog:** `Service/Foutlog/FoutLogFilter.cs` vervangt wachtwoorden, gebruikersnamen, e-mailadressen, bedragen, IBAN's en telefoonnummers voor een fout in de database komt, en er zijn testen voor. Het gewone Serilog-bestand heeft die filter niet; daarin komen gebruikersnamen bij mislukte logins (`AuthService.cs:47`). `crash.log` noteert op welke knoppen laatst geklikt is (`App.axaml.cs:96-98`).
8. **Echte bestelbon in de repo en in de app:** `Assets/2.Voorbeeld huidige bestelbon (2).pdf` is een scan van een bestelbon uit 2025 met voornamen in de omschrijving van het werk. Geen adres of familienaam, maar het bestand is publiek en zit ingebed in elke installatie (3.2).
9. `docs/.~lock.QuadroApp_Handleiding_v2.pdf#` bevat de naam van een werksessie en een bestandspad.
10. `Scripts/installatie_veerle.html` bevat lege invulvakken voor wachtwoorden, geen waarden.
11. `main` is niet beveiligd; iedereen met schrijfrecht kan rechtstreeks pushen, en een push naar `main` start de release-keten.

### 3.9 Documentatie

**Verouderd of tegenstrijdig**

| Plaats | Wat er staat | Wat klopt |
|---|---|---|
| `README.md:15` | "xUnit (119 tests)" | 562 |
| `README.md:11` | "ClosedXML + EPPlus" | EPPlus wordt niet gebruikt |
| `README.md:10` | "SQLite (PostgreSQL-ondersteuning aanwezig …)" | productie draait op PostgreSQL |
| `README.md:29` | "Feature-branch vanaf `main`" | features gaan naar `dev` |
| `README.md` ("Belangrijk om te weten") | `appsettings.example.json` als sjabloon | bestand is verwijderd |
| `README.md:72` | "enterprise-hardening: 9/10 afgerond" | niet na te gaan, stand van de zomer |
| `docs/backlog/US-87-95_Verkoopdossier_Plan.md:11, 12, 43, 56, 73` | stap 2 heet "Bestelling", `VerkoopStapFlags.Bestelling`, `BevestigAlsBestellingAsync` | afspraak en code: "Werkbon" |
| idem `:124` | "`OfferteViewModel` … nu 1 798 regels" | 1 902 |
| `App.axaml.cs:141-142` | "Init draait nog op EnsureCreatedAsync tot de Baseline gegenereerd is" | de Baseline bestaat sinds augustus |
| `.github/workflows/build.yml:48` | ".NET 9" | .NET 10 |
| `docs/cleanup-candidates.md` | verwijst naar `LoginWindow`, `LoginViewModel` | bestaan niet meer |
| `.github/copilot-instructions.md` | drie regels over Azure-tools | heeft niets met dit project te maken |
| `docs/US41_PG_MIGRATIONS_PLAN.md`, `docs/DEPLOYMENT_CHECKLIST.md` | staan in de hoofdmap van `docs` | US-41 is afgerond; hoort in `docs/archief` |
| `Views/AnnuleerVerkoopDialog.axaml:34` | schermtekst eindigt op "(zoals nu)" | lijkt een zin uit het ontwerp; zie 8 |

Er zijn twee handleidingen (v1 en v2), elk als `.docx` en `.pdf`. De map `docs/backlog` telt 20 documenten zonder index; welke stories af zijn, staat verspreid.

**Wat ontbreekt voor een nieuwe ontwikkelaar**

- Een actueel overzicht van de architectuur (het bestaande `SOLUTION_STRUCTURE.md` staat in het archief): welke laag wat mag, hoe het dossier de bestaande ViewModels hergebruikt.
- De branch- en releaseflow met `main` en `dev`, inclusief hoe een hotfix loopt en hoe een tag ontstaat nu auto-tag stuk is.
- Hoe je lokaal veilig werkt: wat `DevDatabaseGuard` doet, hoe je een lokale SQLite-database krijgt, hoe je tegen een test-PostgreSQL werkt.
- De rendertool staat alleen in de lokale werkafspraken; de README noemt hem niet.
- Een lijst van de domeinwoorden staat alleen in de lokale werkafspraken, niet in de README of de docs.
- Een overzicht van wat er bij de start gebeurt (patchers, migraties, seed, backup, import van `crash.log`).

### 3.10 `main` ↔ `dev`

**Verschil** (`git diff --stat origin/main...origin/dev`): 129 bestanden, 15 878 regels erbij, 2 364 eraf; 80 nieuw, 38 gewijzigd, 9 verwijderd, 2 hernoemd. `dev` loopt 66 commits voor; `main` heeft één commit die `dev` niet heeft: de merge-commit van PR #135, waarvan de inhoud via de tweeling-PR #136 al in `dev` zit.

**Kan het conflicteren?** Vandaag niet. `git merge-tree --write-tree origin/main origin/dev` geeft boom `65007c5c…`, en dat is exact de boom van `origin/dev` (`git rev-parse origin/dev^{tree}`). De merge is dus conflictvrij en het resultaat is identiek aan `dev`.

**Wat verdwijnt van `main`:**

| Bestand op `main` | Lot |
|---|---|
| `ViewModels/WerkBonLijstViewModel.cs` | verwijderd |
| `Views/WerkBonLijstView.axaml(.cs)` | verwijderd |
| `Views/WerkBonDetailWindow.axaml(.cs)` | verwijderd |
| `Views/PlanningCalendarWindow.axaml(.cs)` | verwijderd |
| `Views/BestelbonnenView.axaml` (+ `.cs`) | verwijderd (git ziet de `.cs` als hernoemd naar `StapOverslaanDialog.axaml.cs`, 54 % gelijkenis: toeval) |
| `Views/OfferteView.axaml(.cs)` | opgegaan in `Views/Offerte/OfferteRegelEditor.axaml` (61 % gelijkenis) |

Voor de gebruiker verdwijnen daarmee de aparte schermen Offerte, Werkbonnenlijst, Werkbon-venster, Bestelbonnen en het planningsvenster; ze zijn vervangen door het verkoopdossier (US-95).

**Waar het wél kan botsen:** elke nieuwe hotfix op `main` in één van die oude bestanden. Dat is al één keer gebeurd: `d1db875` wijzigde `Views/OfferteView.axaml` op `main` nadat `dev` dat bestand had opgesplitst; het is met de tweeling-PR opgevangen. Hoe langer de merge wacht, hoe vaker dit terugkomt. Verder: de twee extra migraties (3.5) en de snapshots, die bij elke databasewijziging op `main` opnieuw moeten worden gelijkgetrokken.

**Wat pas na de merge zin heeft:**

- Alles wat bestanden hernoemt, verplaatst of splitst (`OfferteViewModel`, `VerkoopDossierViewModel`, `App.axaml.cs`, mappenstructuur, centrale pakketversies). Vóór de merge maakt dat elke hotfix op `main` moeilijker terug te zetten naar `dev`.
- Dode code verwijderen: de drie oude import-dialogen bestaan ook op `main`; verwijder ze één keer, op de samengevoegde branch.
- De 24 branches die alleen in `dev` zitten, zijn technisch nu al veilig te verwijderen (ze zitten volledig in `dev`), maar wachten tot na de merge is het eenvoudigst uit te leggen.
- Een nieuwe Baseline (squash): pas als productie op de samengevoegde versie draait.

**Wat vóór de merge kan** (raakt geen applicatiecode): CI-herstel, branches opruimen die in `main` zitten, documentatie, beveiliging van de database.

---

## 4. Voorstellen

Leeswijzer: *Impact* = winst voor onderhoud of veiligheid. *Werk* = geschatte uren. *Gedrag?* = verandert er iets voor de gebruiker of in de database. Staat daar iets anders dan "nee", dan staat het voorstel ook in hoofdstuk 7. *Geldt voor*: `main`, `dev` of beide; bij "beide" is het advies om het één keer te doen na de merge.

### 4.1 Repository, CI en configuratie

| ID | Omschrijving | Waarom | Betrokken bestanden | Impact | Werk | Risico | Gedrag? | Bewijs dat niets verandert | Geldt voor |
|---|---|---|---|---|---:|---|---|---|---|
| V-01 | `dev` naar `main` mergen met een gewone merge-commit | conflictvrij vandaag; elke dag wachten vergroot de kans op botsende hotfixes | hele repo | hoog | 2 | middel | **ja**: de nieuwe verkoopflow komt bij Veerle (gepland) | testlijst `docs/backlog/Testlijst_Verkoopdossier.md`; upgrade proberen op een kopie van de productiedatabase (zie T-06) | main |
| V-02 | 69 gemergede branches verwijderen; in GitHub "Automatically delete head branches" aanzetten | 71 branches maken het overzicht onbruikbaar | GitHub-instellingen | middel | 0,5 | laag | nee | lijst en bewijs in hoofdstuk 6; tags blijven | beide |
| V-03 | `RELEASE_PAT` vernieuwen (fijnmazig token, alleen "Contents: write" op deze repo) en vóór de checkout een stap die het token test en een duidelijke Nederlandse fout geeft | auto-tag faalt minstens sinds 31 oktober op een onleesbare fout | `.github/workflows/auto-tag.yml` | hoog | 1 | laag | nee | run van auto-tag op een docs-vrije testcommit | main |
| V-04 | `fail-fast: false` in de release-matrix; testen draaien vóór het publiceren; commentaar ".NET 9" rechtzetten; controlestap met duidelijke melding als de Apple-geheimen ontbreken of de notarisatie weigert | Windows-release hangt nu af van Apple; release zonder test | `.github/workflows/build.yml` | hoog | 2 | laag | nee | handmatige run (`workflow_dispatch`) op een bestaande tag en assets vergelijken met `v1.3.27` | main |
| V-05 | CI ook op `dev` (push en PR), en op `docs/**`, `chore/**` voor PR's | PR's naar `dev` worden nu maar half getest | `.github/workflows/ci.yml` | hoog | 0,5 | laag | nee | een PR naar `dev` toont de CI-check | beide |
| V-06 | Rendertool en MigrationTool mee bouwen in CI (aparte stap of in de solution) | de rendertool is verplicht in de werkwijze maar kan ongemerkt breken | `ci.yml`, eventueel `Quadro.sln` | middel | 1 | laag | nee | CI-run groen; `dotnet build Tools/SchermAfbeeldingen` | beide |
| V-07 | Branchbeveiliging op `main` (en `dev`): PR verplicht, CI moet groen zijn | een rechtstreekse push naar `main` start een release | GitHub-instellingen | middel | 0,5 | laag | nee | proef-push wordt geweigerd | beide |
| V-08 | Qodana: workflow en `qodana.yaml` verwijderen, of herstellen zonder de branch `styling` | staat uit en verwijst naar iets dat niet bestaat | `.github/workflows/qodana_code_quality.yml`, `qodana.yaml` | laag | 0,5 | laag | nee | geen code geraakt | beide |
| V-09 | `actions/checkout` en `actions/setup-dotnet` naar een versie op Node 24 | waarschuwing in elke run | alle workflows | laag | 0,5 | laag | nee | CI-run groen | beide |
| V-10 | EPPlus verwijderen | ongebruikt pakket met eigen licentievoorwaarden | `QuadroApp.csproj`, `README.md` | middel | 0,5 | laag | nee | `git grep OfficeOpenXml` = 0; build + 562 testen; Excel-export eenmaal met de hand | beide |
| V-11 | Drukbestanden en voorbeeld-PDF niet meer inbedden: de glob beperken tot de vier gebruikte afbeeldingen, restregels in het projectbestand weg, bestanden verplaatsen naar `docs/huisstijl/` | dll van 7,8 MB bevat ± 5 MB ongebruikt materiaal en een echte bestelbon | `QuadroApp.csproj`, `Assets/DRUK/*`, `Assets/*.pdf` | middel | 1 | laag | nee | alle scenario's renderen vóór/na en pixelvergelijken; bestelbon-, werkbon- en weeklijst-PDF vóór/na (logo's) | beide |
| V-12 | Centrale pakketversies (`Directory.Packages.props`) | EF Core en SQLitePCLRaw staan in vier bestanden | alle `.csproj` | laag | 1,5 | laag | nee | `dotnet list package` vóór/na identiek | na merge |
| V-13 | Kleine pakketupdates (Mvvm, ClosedXML, SQLitePCLRaw, Test.Sdk) | opmerking van de testrunner; onderhoud | `.csproj` | laag | 1 | laag | nee | build, testen, kwetsbaarheden-scan | beide |
| V-14 | Repo-hygiëne: lockbestand weg en `.~lock.*` in `.gitignore`, handleiding v1 naar archief, `copilot-instructions.md` en `cleanup-candidates.md` weg, afgeronde plannen naar `docs/archief` | ruis | `docs/**`, `.gitignore`, `.github/copilot-instructions.md` | laag | 1 | laag | nee | alleen documenten | beide |
| V-15 | README en plan bijwerken (tabel in 3.9), flow `main`/`dev` en hotfixes beschrijven, rendertool vermelden, index voor `docs/backlog` | nieuwe ontwikkelaar wordt nu op het verkeerde been gezet | `README.md`, `docs/backlog/*` | middel | 3 | laag | nee | alleen documenten | beide |

### 4.2 Dode en dubbele code

| ID | Omschrijving | Waarom | Betrokken bestanden | Impact | Werk | Risico | Gedrag? | Bewijs dat niets verandert | Geldt voor |
|---|---|---|---|---|---:|---|---|---|---|
| V-16 | Drie oude import-dialogen verwijderen met hun vensters, ViewModels en rijmodellen | ± 450 regels zonder aanroeper | `Service/DialogService.cs`, `IDialogService.cs`, `Views/*ImportPreviewWindow*`, `ViewModels/Excel/KlantExcelPreviewViewModel.cs`, `AfwerkingExcelPreviewViewModel.cs`, `QuadroApp.Data/Model/Import/*PreviewRow.cs`, `ImportResult.cs`, testfake | middel | 2 | laag | nee | compiler (geen aanroeper); testen; de drie import-knoppen met de hand; rendervergelijking | na merge |
| V-17 | Losse dode klassen verwijderen: `LeverancierBestellingViewModel`, `ToastColorConverter`, `WerkBonStatusToBrushConverter`, `ImportCommitResult` | ± 270 regels | zie 3.2 | laag | 1 | laag | nee | `git grep -w` per naam = alleen eigen bestand; build; rendervergelijking | na merge |
| V-18 | Klantnaam via `Klant.VolledigeNaam`; de vier terugvalteksten blijven per plaats exact zoals nu | acht kopieën | bestanden uit 3.2 | laag | 1,5 | laag | nee | bestaande testen; PDF-tekstvergelijking; rendervergelijking van Verkopen en werkbon | na merge |
| V-19 | Eén berekening voor "nog te betalen" en één voor de bruto-totalen van de bestelbon, eerst met testen die de huidige uitkomsten vastleggen | vijf en drie varianten; risico op verschil tussen scherm en PDF | `Bestelbon.cs`, `IOntvangstService.cs`, `PdfBestelbonExporter.cs`, `VerkoopOverzichtService.cs`, `BestelbonWorkflowService.cs` | middel | 4 | middel | nee | eerst een tabeltest met randgevallen (voorschot groter dan totaal, korting, halve centen) op alle huidige varianten; alleen samenvoegen wat aantoonbaar gelijk is; PDF-tekstvergelijking | na merge |
| V-20 | Eén helper voor de uitvoermap met de terugval die `PdfWerkBonExporter` al heeft | vier exporters geven een relatief pad als Documenten ontbreekt | de vijf exporters uit 3.3 | middel | 1,5 | laag | nee op Windows en macOS; alleen anders waar Documenten ontbreekt (nu: verkeerd pad) | test met lege `HOME`; padtest op Windows; PDF's renderen | na merge |

### 4.3 Structuur en refactors

| ID | Omschrijving | Waarom | Betrokken bestanden | Impact | Werk | Risico | Gedrag? | Bewijs dat niets verandert | Geldt voor |
|---|---|---|---|---|---:|---|---|---|---|
| V-21 | Vangnet: karakteriseringstesten voor `OfferteViewModel` (bewaren → herladen → alle kolommen gelijk; `PropertyChanged` van de doorgeef-properties) | zonder dit is elke stap hieronder een gok | `WorkflowService.Tests` | hoog | 6 | laag | nee | alleen testen | beide |
| V-22 | `OfferteViewModel` opsplitsen in partial-bestanden, zuiver knippen en plakken: `.RegelProxy.cs`, `.Afwerkingen.cs`, `.Voorschot.cs`, `.Opslaan.cs`, `.Prijs.cs` | 1 902 regels in één bestand; nul gedragsrisico omdat de klasse dezelfde blijft | `ViewModels/Offerte/OfferteViewModel*.cs` | hoog | 2 | laag | nee | de gecompileerde klasse is identiek; build, testen, rendervergelijking stap 1 | na merge |
| V-23 | De 255 regels "Pricing helpers" (r. 1582–1837) naar een eigen klasse zonder toestand, met testen; `PricingEngine` blijft onaangeroerd | rekenwerk in schermcode is niet te testen | `OfferteViewModel.cs`, nieuw bestand | hoog | 5 | middel | nee | V-21; bestaande `PricingEngineTests`; vergelijking van totalen op de testgegevens vóór/na | na merge |
| V-24 | Opslaan van de offerte naar een service: rij laden, velden toewijzen, bewaren, in één transactie | haalt de stub-lijst weg (bron van "offertenummer 0") en de dubbele save | `OfferteViewModel.cs:1442-1580`, nieuwe service | hoog | 6 | middel | nee voor één gebruiker; zie 7 (A-06) voor gelijktijdig werken | V-21, `OfferteOpslaanHoudtNummerTests`, `OfferteNummerNulTests` | na merge |
| V-25 | Voorschot-blok (r. 73–200) naar een eigen deel-ViewModel; constructor van 18 naar ± 10 parameters | volgt het patroon van de drie bestaande deel-ViewModels | `OfferteViewModel.cs`, `Views/Offerte/*.axaml` (bindingpaden) | middel | 5 | middel | nee | V-21; `VoorschotVerwijderenCrashTests` (24 testen); rendervergelijking | na merge |
| V-26 | `VerkoopDossierViewModel`: `IServiceProvider` vervangen door geïnjecteerde fabrieken (`Func<OfferteViewModel>`, `Func<BestelbonnenViewModel>`) en `IPathOpener`; optionele parameters verplicht maken; de twee dubbele blokken samenvoegen | knoppen die stil niets doen als de container ontbreekt; dubbel werk | `ViewModels/Verkoop/*.cs`, `App.axaml.cs`, rendertool, testen | middel | 4 | middel | nee | `DossierStap1Tests`, `VerkoopDossierServiceTests`; alle dossierscenario's renderen | dev / na merge |
| V-27 | PDF-exporters achter een interface en in de container; PDF maken van de UI-thread halen waar dat nog niet zo is | vier keer `new`; niet te vervangen in testen; scherm bevriest kort | exporters, vier ViewModels, `App.axaml.cs` | middel | 4 | laag | nee (zelfde bestand, zelfde plaats) | eerst T-02; PDF-tekstvergelijking vóór/na | na merge |
| V-28 | `InstellingenWindow`: acht keer `App.Services` vervangen door commando's op `InstellingenViewModel`; idem `LijstenWindow`, `MainWindow` | service locator in code-behind; `async void` zonder vangnet | `Views/InstellingenWindow.axaml(.cs)`, `ViewModels/InstellingenViewModel.cs` | middel | 4 | middel | nee | scenario "instellingen" renderen; elke knop met de hand; nieuwe ViewModel-test | na merge |
| V-29 | `App.axaml.cs` splitsen: registratie van services, database-opstart en foutafhandeling elk in een eigen bestand | 782 regels, vijf taken | `App.axaml.cs` + drie nieuwe bestanden | middel | 3 | laag | nee | zuivere verplaatsing; app starten op SQLite en op test-PostgreSQL; `SeederTests`, `MigrationSafetyTests` | na merge |
| V-30 | Registraties eerlijk maken: `AddScoped` → `AddSingleton` of `AddTransient` naar wat er werkelijk gebeurt, en `ValidateOnBuild` aanzetten in Debug | de registratie belooft iets wat niet gebeurt | `App.axaml.cs:183-241` | laag | 2 | middel | nee | per service nakijken dat ze geen toestand bewaart; testen; app starten | na merge |
| V-31 | De drie hulpconstructors met `NoOp…`/`Fallback…` uit `WorkflowService` naar de testinfrastructuur | testhulp in productiecode | `Service/WorkflowService.cs`, testen, rendertool | laag | 2 | laag | nee | build van testen en rendertool; `WorkflowServiceTests` | na merge |
| V-32 | Eén hulpmethode in `AsyncViewModelBase` voor "bezig → actie → herladen", en de ViewModels er stap voor stap op overzetten | haalt het `IsBusy`-patroon bij de wortel weg | `AsyncViewModelBase.cs`, ViewModels uit 3.3 | middel | 6 | middel | nee | per scherm een test "na bewaren staat de nieuwe rij in de lijst" (zoals `FeedbackVeerle1211Tests`); rendervergelijking | na merge |
| V-33 | Context op `OfferteViewModel.cs:1303` met `await using` openen | wordt nu nooit vrijgegeven | één regel | laag | 0,5 | laag | nee | bestaande testen | beide |
| V-34 | Mappen en namen rechtzetten: `Service/Import/interfaces` → `Interfaces`, `FilePickerService` eruit, modelklassen uit `IBetalingsOverzichtService.cs`, `leverancier.cs` → `Leverancier.cs`, `KlantWeekBlock` weg uit de ViewModels zodat de exporter geen ViewModel meer kent | kleine drempels bij het zoeken | diverse | laag | 3 | laag | nee | zuivere verplaatsing; build en testen | na merge |

### 4.4 Tests

| ID | Omschrijving | Waarom | Betrokken bestanden | Impact | Werk | Risico | Gedrag? | Bewijs | Geldt voor |
|---|---|---|---|---|---:|---|---|---|---|
| T-01 | QuestPDF-licentie op één plaats voor het hele testproject (module-initialisatie); de vier losse regels weg | volgorde-afhankelijkheid | `WorkflowService.Tests` | middel | 0,5 | laag | nee | testen in willekeurige volgorde en klasse per klasse apart | beide |
| T-02 | Testen voor `PdfWeekLijstExporter`, `PdfBetalingsOverzichtExporter`, `PdfKlantenlijstExporter` (bestand bestaat, tekst bevat de verwachte namen en bedragen) | drie afdrukken zonder enige test | nieuw | middel | 4 | laag | nee | alleen testen | beide |
| T-03 | Eén `TestToastService`; de twee privékopieën weg | dubbel | drie testbestanden | laag | 0,5 | laag | nee | testen groen | beide |
| T-04 | InMemory-testen (13 bestanden) geleidelijk naar SQLite | InMemory controleert geen verwijzingen of unieke indexen | 13 testbestanden | middel | 6 | laag | nee | testen groen; mogelijk komen echte fouten boven | na merge |
| T-05 | PostgreSQL in CI: een job met een PostgreSQL-service die de Npgsql-migraties toepast, de drift-controle doet en een kleine set testen draait (bewaren en gelijktijdig wijzigen van offerte en werkbon) | productie draait op een database waar geen enkele test op loopt | `ci.yml`, nieuwe testklasse | hoog | 8 | laag | nee | alleen testen; kan echte fouten tonen (zie A-04) | beide |
| T-06 | Upgrade-test `main` → `dev`: database opbouwen met de migraties van `main`, daarna die van `dev` toepassen (uit volgorde), voor SQLite en PostgreSQL | twee migraties zijn ouder dan wat productie al heeft | `MigrationSafetyTests.cs` | hoog | 3 | laag | nee | alleen testen | dev, vóór V-01 |
| T-07 | Rendervergelijking als script: alle scenario's renderen op twee commits en de PNG's vergelijken; uitkomst als artefact in de PR | maakt "er verandert niets op het scherm" bewijsbaar voor elke refactor | `Tools/SchermAfbeeldingen`, `ci.yml` | hoog | 5 | laag | nee | het script zelf | na merge |

---

## 5. Voorgestelde volgorde

Elke regel is één kleine PR die op zichzelf veilig is. Fase 0 en 1 raken geen applicatiecode en kunnen dus vóór de merge van `dev`.

**Fase 0 — deze week, zonder code**

1. A-01: database afschermen (beslissing van Anthony, zie hoofdstuk 7). Geen PR, wel het dringendst.
2. V-03: nieuw `RELEASE_PAT` en duidelijke fout in auto-tag.
3. V-02 (deel A): de 45 branches verwijderen die in `main` zitten; automatisch opruimen aanzetten.
4. V-07: branchbeveiliging op `main` en `dev`.

**Fase 1 — vóór de merge, kleine PR's naar `dev`**

5. V-05 + V-06: CI op `dev`, rendertool mee bouwen.
6. V-04: `fail-fast: false` en testen in de release-build.
7. T-06: upgrade-test `main` → `dev`.
8. T-01: licentie op één plaats.
9. V-14 + V-15: documentatie en hygiëne.

**Fase 2 — de merge**

10. V-01: `dev` → `main`, na de testlijst en een proef op een kopie van de productiedatabase. Daarna V-02 (deel B): de overige 24 branches en de twee losse.

**Fase 3 — na de merge, veilig en klein**

11. V-10: EPPlus weg.
12. V-11: drukbestanden uit de binary.
13. V-33: context vrijgeven.
14. V-16, V-17: dode code weg.
15. T-02, T-03: PDF-testen, één toast-fake.
16. T-07: rendervergelijking als script.
17. V-20: uitvoermap.
18. V-18: klantnaam.

**Fase 4 — het vangnet en de grote klassen**

19. V-21: karakteriseringstesten.
20. V-22: `OfferteViewModel` in partial-bestanden (zuivere verplaatsing).
21. V-23: rekenhulp eruit.
22. V-26: `VerkoopDossierViewModel` zonder `IServiceProvider`.
23. V-27: exporters in de container.
24. V-24, V-25: opslaan naar een service, voorschot naar een deel-ViewModel.
25. V-29, V-31, V-34: `App.axaml.cs`, hulpconstructors, mappen.
26. V-28, V-30, V-32: instellingen, registraties, `IsBusy`-hulp.
27. V-19: betaalstand en totalen samenvoegen.

**Fase 5 — doorlopend**

28. T-05: PostgreSQL in CI. T-04: InMemory afbouwen. V-12, V-13, V-08, V-09: pakketten en workflows.

De punten uit hoofdstuk 7 lopen hier los van en wachten op een beslissing.

---

## 6. Git-opruimlijst

Niets hiervan is uitgevoerd.

**Bewijs.** Kolom "in main" en "in dev" komen uit `git branch -r --merged origin/main` en `git branch -r --merged origin/dev`, uitgevoerd op de volledige geschiedenis op 16 november. "Gemerged" betekent hier: de laatste commit van de branch is een voorouder van `main` of `dev`, dus er gaat geen enkele commit verloren.

- **A** = in `main` en in `dev` (45): mag nu weg.
- **B** = in `dev`, nog niet in `main` (24): gaat geen werk verloren; verwijderen na de merge is het eenvoudigst.
- **C** = niet gemerged (2): zie de nota onder de tabel.

| Branch | Laatste commit | Hash | In main | In dev | Voorstel |
|---|---|---|---|---|---|
| `feature/us51-werkbon-lijst-fase-a` | 2026-08-04 | `2f315cb` | ja | ja | A: verwijderen |
| `feature/us43-retry-execution-strategy` | 2026-08-04 | `5701aea` | ja | ja | A: verwijderen |
| `feature/us46b-offerte-fase-b` | 2026-08-04 | `48550bb` | ja | ja | A: verwijderen |
| `feature/us41-pg-ef-migrations` | 2026-08-05 | `736dbe3` | ja | ja | A: verwijderen |
| `feature/us50-datatransfer-dnd` | 2026-08-10 | `a2ee1a6` | ja | ja | A: verwijderen |
| `feature/us63-korting-per-offerteregel` | 2026-09-26 | `f9fa851` | ja | ja | A: verwijderen |
| `feature/us65-inleg-prijsberekening` | 2026-09-26 | `7039403` | ja | ja | A: verwijderen |
| `feature/us71-inleg-op-bestelbon` | 2026-09-27 | `97471c6` | ja | ja | A: verwijderen |
| `anthonymohamedap-patch-1` | 2026-09-27 | `11c99a3` | ja | ja | A: verwijderen |
| `feature/us67-overzicht-betalingen` | 2026-10-06 | `97f4d57` | ja | ja | A: verwijderen |
| `feature/us72-testfixes` | 2026-10-06 | `a8885bc` | ja | ja | A: verwijderen |
| `feature/us73-betalingen-bestelbon-pdf` | 2026-10-06 | `1859e70` | ja | ja | A: verwijderen |
| `feature/us74-wisselgeld` | 2026-10-06 | `d47341d` | ja | ja | A: verwijderen |
| `feature/us76-afrekenen-afhaling` | 2026-10-12 | `ab6cb85` | ja | ja | A: verwijderen |
| `feature/us77-78-live-prijs-korting` | 2026-10-13 | `545e3de` | ja | ja | A: verwijderen |
| `docs/sprint-november-2026` | 2026-10-26 | `5c4f864` | ja | ja | A: verwijderen |
| `feature/quirky-albattani-xpgdw2` | 2026-10-26 | `ceaa52e` | ja | ja | A: verwijderen |
| `fix/cve-cryptography-pakketten` | 2026-10-26 | `74b450d` | ja | ja | A: verwijderen |
| `feature/us81-offertenummer` | 2026-10-26 | `900e6f4` | ja | ja | A: verwijderen |
| `docs/verkoopdossier-plan` | 2026-10-27 | `59e6964` | ja | ja | A: verwijderen |
| `feature/us79-betaalwijzen` | 2026-10-27 | `3fcb4ec` | ja | ja | A: verwijderen |
| `feature/us82-bestelbon-afdrukken` | 2026-10-26 | `cbd1b38` | ja | ja | A: verwijderen |
| `feature/us83-stappenbalk` | 2026-10-26 | `0b0a81c` | ja | ja | A: verwijderen |
| `feature/us80-standaardkaders` | 2026-10-26 | `526b6a9` | ja | ja | A: verwijderen |
| `feature/us84-offertelijst-per-fase` | 2026-10-27 | `ea72aab` | ja | ja | A: verwijderen |
| `feature/us85-gewone-woorden` | 2026-10-27 | `af00406` | ja | ja | A: verwijderen |
| `feature/affectionate-gauss-jbyjcc` | 2026-10-27 | `5bc56f2` | ja | ja | A: verwijderen |
| `feature/bestelbon-als-offerte` | 2026-10-27 | `0c0435e` | ja | ja | A: verwijderen |
| `feature/us96-niet-plannen-in-verleden` | 2026-10-27 | `18196a4` | ja | ja | A: verwijderen |
| `docs/us99-factuur-uit-de-code` | 2026-10-31 | `b5c8257` | nee | nee | C: verwijderen, zie nota |
| `feature/us97-menu-werkbonnen` | 2026-10-31 | `5c66b69` | ja | ja | A: verwijderen |
| `feature/us99a-factuur-uit-teksten` | 2026-10-31 | `ae6609c` | ja | ja | A: verwijderen |
| `feature/us98-weeklijst-bij-werkbonnen` | 2026-10-31 | `311c794` | ja | ja | A: verwijderen |
| `feature/us100-prijs-vast-na-bevestiging` | 2026-10-31 | `ced192b` | ja | ja | A: verwijderen |
| `feature/us103-niet-terug-na-werkbon` | 2026-10-31 | `fc16c33` | ja | ja | A: verwijderen |
| `feature/us104-werkbon-i-p-v-bestelling` | 2026-10-31 | `1f2f0c4` | ja | ja | A: verwijderen |
| `feature/us-86-home-blocks-wba2ws` | 2026-10-31 | `525d848` | nee | nee | C: verwijderen, zie nota |
| `feature/us105-beginscherm-redesign` | 2026-10-31 | `616841d` | ja | ja | A: verwijderen |
| `feature/us99b-factuur-naar-bestelbon-code` | 2026-10-31 | `ba14680` | ja | ja | A: verwijderen |
| `feature/us99c-database-bestelbon` | 2026-10-31 | `a620033` | ja | ja | A: verwijderen |
| `docs/verkoopdossier-beslissingen` | 2026-11-02 | `2d175d6` | nee | ja | B: verwijderen na de merge |
| `docs/verkoopdossier-mock-stap2` | 2026-11-02 | `4cd4833` | nee | ja | B: verwijderen na de merge |
| `docs/verkoopdossier-mock-stap3` | 2026-11-02 | `5961e78` | nee | ja | B: verwijderen na de merge |
| `docs/us106-afvinklijst-werktaken` | 2026-11-02 | `c9c2e98` | nee | ja | B: verwijderen na de merge |
| `docs/verkoopdossier-mock-stap4-5` | 2026-11-02 | `f9cb29d` | nee | ja | B: verwijderen na de merge |
| `docs/verkoopdossier-mock-verkopen` | 2026-11-02 | `46fc95a` | nee | ja | B: verwijderen na de merge |
| `feature/us87-fundament-verkoopdossier` | 2026-11-02 | `0496eea` | nee | ja | B: verwijderen na de merge |
| `fix/reconciliatie-voorlopige-bestelbon` | 2026-11-02 | `6f82f54` | ja | ja | A: verwijderen |
| `feature/us89-scherm-verkopen` | 2026-11-02 | `3b0a751` | nee | ja | B: verwijderen na de merge |
| `feature/us90-dossier-schil` | 2026-11-02 | `fe11680` | nee | ja | B: verwijderen na de merge |
| `feature/us91-stap1-offerte` | 2026-11-03 | `642a57a` | nee | ja | B: verwijderen na de merge |
| `feature/us92-planning-pagina` | 2026-11-03 | `d775747` | nee | ja | B: verwijderen na de merge |
| `feature/us92-planner-stap2` | 2026-11-05 | `a85a480` | nee | ja | B: verwijderen na de merge |
| `fix/offertenummer-nul-na-archief` | 2026-11-05 | `9a87219` | ja | ja | A: verwijderen |
| `feature/us92-planning-nieuw` | 2026-11-05 | `15af26e` | nee | ja | B: verwijderen na de merge |
| `chore/dev-bijwerken-met-main` | 2026-11-08 | `076bc5a` | nee | ja | B: verwijderen na de merge |
| `feature/us93-stappen-3-5` | 2026-11-08 | `b499ca7` | nee | ja | B: verwijderen na de merge |
| `feature/us94-afdrukken` | 2026-11-08 | `4a003d3` | nee | ja | B: verwijderen na de merge |
| `feature/us95-navigatie` | 2026-11-08 | `47729dd` | nee | ja | B: verwijderen na de merge |
| `fix/offerte-opslaan-wist-nummer` | 2026-11-08 | `1a8c5eb` | ja | ja | A: verwijderen |
| `fix/nieuwe-verkoop-in-dossier` | 2026-11-08 | `6e49a39` | nee | ja | B: verwijderen na de merge |
| `feature/bestelbon-bij-bevestigen` | 2026-11-08 | `04eb21a` | nee | ja | B: verwijderen na de merge |
| `feature/us95-oude-schermen-weg` | 2026-11-08 | `bb6792f` | nee | ja | B: verwijderen na de merge |
| `fix/feedback-veerle-0911` | 2026-11-09 | `7cb2e1a` | ja | ja | A: verwijderen |
| `fix/voorschot-verwijderen-crash` | 2026-11-09 | `54832d0` | ja | ja | A: verwijderen |
| `fix/voorschot-verwijderen-crash-dev` | 2026-11-09 | `22df5d7` | nee | ja | B: verwijderen na de merge |
| `fix/feedback-veerle-0911-dev` | 2026-11-11 | `67fd59f` | nee | ja | B: verwijderen na de merge |
| `feature/foutlog-in-db-dev` | 2026-11-11 | `4b42e8b` | nee | ja | B: verwijderen na de merge |
| `feature/foutlog-in-db` | 2026-11-11 | `af904c0` | ja | ja | A: verwijderen |
| `fix/feedback-veerle-1211` | 2026-11-12 | `cff06a0` | ja | ja | A: verwijderen |
| `fix/feedback-veerle-1211-dev` | 2026-11-13 | `54be54d` | nee | ja | B: verwijderen na de merge |

**Nota bij C**

- `feature/us-86-home-blocks-wba2ws`: de enige commit die niet in `dev` zit, is een merge-commit zonder eigen inhoud; beide ouders zitten in `main` en `dev` (`git merge-base --is-ancestor 94545eb origin/main` en idem voor `149d6dd`). Mag weg.
- `docs/us99-factuur-uit-de-code`: bevat een oudere versie van een document dat in `dev` nieuwer is. Mag weg; wie zeker wil zijn, bekijkt eerst `git diff origin/dev origin/docs/us99-factuur-uit-de-code -- docs/backlog/US-99_Factuur_uit_de_code.md`.

**Commando's**

Eerst zelf het bewijs herhalen:

```bash
git fetch origin --prune
```

```bash
git branch -r --merged origin/main | grep -v -E 'origin/(HEAD|main|dev)$'
```

```bash
git branch -r --merged origin/dev | grep -v -E 'origin/(HEAD|main|dev)$'
```

Groep A verwijderen (45 branches, in `main` en `dev`):

```bash
git branch -r --merged origin/main | grep -v -E 'origin/(HEAD|main|dev)$' | sed 's# *origin/##' | xargs -n 10 git push origin --delete
```

Groep B verwijderen, na de merge van `dev` naar `main` (dan vallen ze ook onder het vorige commando; dit is de variant op `dev`):

```bash
git branch -r --merged origin/dev | grep -v -E 'origin/(HEAD|main|dev)$' | sed 's# *origin/##' | xargs -n 10 git push origin --delete
```

Groep C:

```bash
git push origin --delete feature/us-86-home-blocks-wba2ws docs/us99-factuur-uit-de-code
```

Lokale kopieën opruimen:

```bash
git fetch origin --prune
```

```bash
git branch --merged origin/main | grep -v -E '^\*|main|dev' | xargs -r git branch -d
```

Een verwijderde branch is terug te halen zolang je de hash uit de tabel hebt: `git push origin <hash>:refs/heads/<naam>`. Tags worden door geen van deze commando's geraakt.

Niet doen: de geschiedenis herschrijven of Git LFS invoeren (zie 3.6).

---

## 7. Vraagt akkoord

Alles wat gedrag, database, bedragen, geschiedenis of de buitenwereld raakt. Niets hiervan is uitgevoerd.

| ID | Wat | Wat verandert er | Advies |
|---|---|---|---|
| A-01 | **Productiedatabase afschermen.** (a) Allowed IPs in Scaleway beperken tot de adressen van het atelier en van Anthony, en de raad `0.0.0.0/0` uit het runbook halen; (b) het wachtwoord van `quadro` vernieuwen; (c) de repo privé zetten, of host en poort uit het runbook en de test halen | (a) een pc op een ander netwerk kan niet meer verbinden tot het adres is toegevoegd; (b) elke pc moet het nieuwe wachtwoord krijgen via `Scripts/set-db-secret`; (c) privé: de updates via GitHub Releases moeten dan blijven werken zonder aanmelding — nakijken vóór je omschakelt. De gegevens blijven hoe dan ook in de geschiedenis staan | (a) en (b) nu; (c) na de controle van de updates |
| A-02 | **Velopack bijwerken** van 0.0.869 (op NuGet gemarkeerd "CriticalBugs") naar een actuele versie | het updatemechanisme zelf; een fout hier betekent dat een pc geen updates meer krijgt | apart testen op een Windows- en een Mac-toestel, van oude naar nieuwe versie en nog een stap verder |
| A-03 | **Offertenummer uniek maken**: unieke index op `Offerte.OfferteNummer` en het nummer in dezelfde transactie toekennen | migratie voor SQLite en Npgsql; mislukt als er nu dubbele nummers in de database staan, dus eerst tellen | zinvol vóór er met twee pc's tegelijk gewerkt wordt |
| A-04 | **Gelijktijdig wijzigen van offertes en werktaken op PostgreSQL** laten werken (`xmin` ook voor `Offerte` en `WerkTaak`) | Npgsql-model en snapshot wijzigen; gebruikers kunnen de melding "iemand anders heeft deze offerte gewijzigd" krijgen die nu nooit verschijnt | eerst bevestigen met T-05 dat het nu niet werkt |
| A-05 | **Eén afrondingsregel** voor alle bedragen | bedragen op een halve cent kunnen één cent verschillen van vandaag; `PricingEngine` niet aanraken | alleen op vraag van Veerle of de boekhouder; anders laten |
| A-06 | **Niet meer de hele rij overschrijven** in de zes schermen uit 3.3 (laden, velden toewijzen, bewaren) | voor één gebruiker niets; bij twee pc's tegelijk overschrijft de laatste alleen nog de velden die hij wijzigde in plaats van de hele rij. Dat is veiliger, maar het is ander gedrag van de database | samen met V-24, na de vangnet-testen |
| A-07 | **Foutafhandeling van fire-and-forget**: `RunAsync` ook laten loggen; `try/catch` in de zes `async void`-handlers en in `NaBevestigenAsync` en `VernieuwNaPlanningAsync` | de gebruiker kan een andere (duidelijkere) melding zien dan de algemene "onverwachte fout"; er komen extra regels in de foutentabel | zinvol; tekst van de meldingen afstemmen |
| A-08 | **`IsBusy`-vlag in `LeveranciersViewModel` en `ExportCenterViewModel`** niet meer te vroeg uitzetten | knoppen blijven iets langer uitgeschakeld tijdens bewaren | samen met V-32 |
| A-09 | **Nieuwe Baseline (squash)** voor beide providers | de tabel `__EFMigrationsHistory` van elke bestaande database wordt herschreven | niet nu; pas als productie op de samengevoegde versie draait en bekend is welke databases bestaan |
| A-10 | **Herstelcode verwijderen**: `SqliteSchemaPatcher`, `BestelbonSchemaUpgrade`, `EnsureImportAuditTablesAsync` | een SQLite-database van vóór juli 2026 zou niet meer automatisch hersteld worden | alleen als vaststaat dat er geen oude SQLite-installaties meer zijn |
| A-11 | **Statussen in archief en Excel** via `StatusLabels` in plaats van de enumnaam | andere tekst in het archiefscherm en in geëxporteerde Excel-bestanden; reeds bewaarde archiefrijen behouden de oude tekst tenzij er een datamigratie komt | voorleggen aan Veerle |
| A-12 | **"KantKlaarKader" → "Standaardkader"** in code en eventueel in de database | alleen in code: 223 plaatsen, geen gedrag. In de database: tabel en kolommen hernoemen met een migratie voor beide providers, zoals bij US-99 | lage prioriteit; als het gebeurt, in één keer zoals US-99 |
| A-13 | **Decimale precisie gelijktrekken** op `OfferteRegel` | migratie voor beide providers; op PostgreSQL verandert het kolomtype van twee kolommen | laten tot er een andere reden is om die tabel te wijzigen |
| A-14 | **MigrationTool niet meer bij elke release** | 82 MB minder per release; het hulpmiddel is dan alleen nog via een oude release of een aparte build te krijgen | doen als de overstap naar PostgreSQL overal achter de rug is |
| A-15 | **Voorbeeld-bestelbon en drukbestanden** uit de repo verwijderen (niet alleen uit de binary, zie V-11) | de bestanden staan dan niet meer in `Assets`; ze blijven in de geschiedenis | de bestelbon weg; drukbestanden mogen blijven in `docs/huisstijl` |
| A-16 | **Grote versiesprongen**: Avalonia 12, EF Core 10/11, QuestPDF 2026 | uiterlijk, gedrag van queries en licentievoorwaarden kunnen wijzigen | elk als eigen story met volledige rendervergelijking |
| A-17 | **Schermtekst "(zoals nu)"** in het annuleervenster nakijken | tekst op het scherm | zie 8 |

---

## 8. Te controleren

Wat niet met zekerheid vast te stellen was.

1. **Werkt de bewaking tegen gelijktijdig wijzigen van een offerte op PostgreSQL?** Het model en het commentaar wijzen erop dat het niet werkt (3.5). Het is niet uitgeprobeerd op een echte PostgreSQL. Controle: T-05, of met twee pc's dezelfde offerte openen en na elkaar bewaren.
2. **Staat `v1.3.27` bij Veerle?** Volgens GitHub is de release op 13 november om 14:46 UTC gepubliceerd met bestanden voor Windows en macOS, na een tweede poging van de build. De opdracht ging ervan uit dat ze nog hing op de Apple-overeenkomst. Niet na te gaan of de update ook op haar Mac is binnengekomen.
3. **Is het `RELEASE_PAT` verlopen?** De foutmelding past daarbij, maar de waarde en de vervaldatum van een secret zijn niet uit te lezen.
4. **Waarom staan twee auto-tag-runs en zeven CI-runs van 11 november nog op "queued"?** Mogelijk een storing bij GitHub die dag; ze kunnen geannuleerd worden.
5. **Blijven de updates werken als de repo privé wordt?** `Service/VelopackUpdateChecker.cs` is niet in detail nagelezen op de bron van de updates.
6. **Bestaan er nog SQLite-installaties van vóór de Baseline?** Bepaalt of A-10 kan.
7. **Loopt de upgrade van een productiedatabase van `main` naar `dev` goed** met de twee oudere migraties? Verwacht van wel (alleen kolommen erbij), niet getest. Controle: T-06 en een proef op een kopie.
8. **Staan er nu dubbele of nul-offertenummers in productie?** Nodig vóór A-03. Controle met een telling per `OfferteNummer`.
9. **Is het de bedoeling dat voorraadmutaties en bestellijnen verdwijnen** uit lijsten zodra hun lijst gearchiveerd is (`AppDbContext.cs:67-68`)? En dat een oude offerte een gearchiveerde lijst als leeg toont?
10. **Houden de als "scoped" geregistreerde services echt geen toestand bij?** Niet per service nagelopen; nodig vóór V-30.
11. **De schermtekst "(zoals nu)"** in `Views/AnnuleerVerkoopDialog.axaml:34`: bewust of een rest uit het ontwerp?
12. **Bevatten de schermafbeeldingen in `docs/handleiding/img` echte klantgegevens?** Niet bekeken; relevant omdat de repo publiek is.
13. **Waarom staat `Tmds.DBus.Protocol` als directe verwijzing?** Vermoedelijk een beveiligingspin; er staat geen commentaar bij.
14. **Ongebruikte private methodes en velden**: de analyzer-regels daarvoor (IDE0051/IDE0052) lieten zich in deze omgeving niet inschakelen. De zoektocht is gebeurd op naam over de hele repo; binnen grote klassen kan er nog iets zitten. Controle: Rider of Visual Studio, "Find unused code" op de solution.
15. **De cijfers "gedekt door testen"** zijn gebaseerd op klassenamen die in testbestanden voorkomen, niet op een dekkingsmeting. Controle: `dotnet test --collect:"XPlat Code Coverage"`.
16. **QuestPDF 2024.7.1 met de Community-licentie**: of het atelier binnen de voorwaarden valt, is een vraag voor Anthony, niet iets wat uit de code blijkt.
17. **Ureninschattingen** zijn ruwe schattingen voor iemand die de code kent.

---

## 9. Uitkomst van de controle (16 november 2026)

Na het rapport zijn de punten uit hoofdstuk 8 nagegaan waar dat zonder de productiedatabase kon. Niets aan de app gewijzigd.

| # | Punt | Uitkomst | Bewijs |
|---|---|---|---|
| 1 | Gelijktijdig wijzigen op PostgreSQL | **Bevestigd: werkt niet voor `Offerte` en `WerkTaak`.** Twee verbindingen laden dezelfde rij, beide bewaren: de tweede overschrijft de eerste zonder melding. `WerkBon` (controle) geeft wel `DbUpdateConcurrencyException`. A-04 is dus een echte fout zodra er op twee pc's tegelijk gewerkt wordt. | Lokale PostgreSQL 16.15, database opgebouwd met de Npgsql-migraties van `dev` (`MigrateAsync`), proef met twee `AppDbContext`'s per entiteit. |
| 2 | Staat v1.3.27 bij Veerle | Niet na te gaan vanaf hier; release staat op GitHub (Windows + macOS). | — |
| 3 | `RELEASE_PAT` verlopen | Niet uit te lezen; vernieuwen blijft de oplossing (V-03). | — |
| 4 | Runs van 11/11 op "queued" | Elf runs hangen in een tussenstatus; annuleren via de API geeft 409 "has not been queued yet". Onschadelijk; met de hand te verwijderen in Actions. | `actions/runs/{id}/cancel` → 409 |
| 5 | Updates bij een privé repo | **Updates stoppen als de repo privé wordt.** De app haalt releases anoniem op: `GithubSource(repoUrl, accessToken: null, …)`. A-01(c) dus niet uitvoeren zonder eerst de updatebron te verplaatsen (bv. een aparte publieke repo enkel voor releases). | `Service/VelopackUpdateChecker.cs:31-34` |
| 6 | Oude SQLite-installaties | Vraag voor Anthony. | — |
| 7 | Upgrade `main` → `dev` | **SQLite: in orde** (test `UpgradeMainNaarDevTests`, PR #140). PostgreSQL: de twee migraties voegen enkel kolommen toe aan `Offertes`; een proef op een kopie van productie blijft aangeraden. | PR #140 |
| 8 | Dubbele of nul-offertenummers in productie | Vraag voor Anthony; query hieronder. | — |
| 9 | Voorraadmutaties/bestellijnen van gearchiveerde lijsten | Productvraag voor Veerle (zie hieronder). | `AppDbContext.cs:61-68` |
| 10 | Bewaren de "scoped" services toestand | **Nee.** Geen van de 19 heeft een veranderlijk veld of een eigen `AppDbContext`; alle gebruiken `IDbContextFactory`. Er wordt in de app geen scope per scherm gemaakt, dus ze leven als één gedeelde instantie — dat is veilig. V-30 heeft daarmee laag risico. | zoektocht naar niet-`readonly` velden en `AppDbContext`-parameters in `Service/` en `Validation/` |
| 11 | "(zoals nu)" in het annuleervenster | **Rest uit het ontwerp** (mock: "dit gebeurt nu al"). Komt twee keer voor; voor de gebruiker betekenisloos. Kan weg (schermtekst, kleine fix). | `Views/AnnuleerVerkoopDialog.axaml:31, 34`, sinds US-90 (`fe11680`) |
| 12 | Klantgegevens in de handleiding-afbeeldingen | **In orde:** testgegevens (Jan Peeters, Sofie Vermeulen, Art & Co BV uit `DatabaseSeeder`). | steekproef `klanten-lijst.png`, `bestelbon-pdf.png`, `klant-detail.png`, `offertes-overzicht.png` |
| 13 | `Tmds.DBus.Protocol` | **Bewuste beveiligingspin** ("override vulnerable transitive deps"), sinds mei 2026. Laten staan. | commit `da9da21` |
| 14 | Ongebruikte private leden | Niet nagegaan (vraagt Rider/Visual Studio). | — |
| 15 | Testdekking | **Gemeten (coverlet, 564 testen):** Service 59 %, ViewModels 39 %, Validation 45 %, Converters 35 %, QuadroApp.Data 86 %. Nul dekking op `AfwerkingenViewModel`, `KlantenViewModel`, `LeveranciersViewModel`, `LijstenViewModel`, `WerkBonArchiefService`, `App.axaml.cs`. `OfferteViewModel` 58 %, `VerkoopDossierViewModel` 66 %. Bevestigt de prioriteit van V-21 (vangnet vóór refactor). | `dotnet test --collect:"XPlat Code Coverage"` met tijdelijk `coverlet.collector` 6.0.4 (niet gecommit) |

**Query voor punt 8** (alleen lezen, op de productiedatabase):

```sql
SELECT "OfferteNummer", COUNT(*) AS aantal
FROM "Offertes"
GROUP BY "OfferteNummer"
HAVING COUNT(*) > 1 OR "OfferteNummer" = 0
ORDER BY "OfferteNummer";
```

Geen rijen = geen dubbele of nul-nummers; dan kan A-03 (unieke index) zonder voorbereiding.

**Vraag voor Veerle bij punt 9:** "Als je een lijst archiveert omdat ze niet meer verkocht wordt, mag ze dan ook verdwijnen uit de voorraadgeschiedenis en uit oude leveranciersbestellingen, of moet ze daar blijven staan?"

**Gevolgen voor de voorstellen**

- **A-04 wordt dringend** zodra twee pc's tegelijk werken: bevestigde fout, niet langer een vermoeden. Oplossing: `Offerte` en `WerkTaak` toevoegen aan de `xmin`-lijst in `AppDbContext.cs:467`, met een Npgsql-migratie en een test op PostgreSQL (T-05).
- **A-01(c) (repo privé)** breekt de updates; eerst de updatebron verplaatsen. A-01(a) en (b) (Allowed IPs, wachtwoord) staan daar los van en blijven dringend.
