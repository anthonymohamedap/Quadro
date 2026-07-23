# Codebase-audit QuadroApp — 22 juli 2026

**Scope:** read-only audit, geen codewijzigingen. Stack: .NET 10 / Avalonia 11.3.12 / EF Core 9.0.9 / SQLite (+ Npgsql voor PostgreSQL), MVVM met CommunityToolkit.Mvvm 8.4.0.
**Branch:** `main` (schone working tree, up-to-date met origin), laatste merge PR #43 (auth-ui).
**Methode:** git-status, gerichte grep/read over ViewModels/Service/Data/Model/Migrations, verificatie per bestand:regel.

> **Beperking bij deze audit:** de sandbox waarin de audit draaide heeft **geen .NET SDK**, dus `dotnet build`, `dotnet list package --vulnerable/--outdated` en `verify.ps1` konden hier **niet** worden uitgevoerd. Die checks draaien wél in CI (`.github/workflows/ci.yml`: build+test op elke push/PR, plus een `security-scan`-job die `--vulnerable --include-transitive` afdwingt en faalt bij kwetsbaarheden). Waar ik uitspraken doe over packages/warnings is dat op basis van statische inspectie van `QuadroApp.csproj` en de broncode, niet van een lokale build. Draai `verify.ps1` op Windows voor de definitieve build-/warning-telling.

---

## Managementsamenvatting

De codebase is in **goede tot zeer goede** conditie. De enterprise-hardening (US-29 t/m US-37) is zichtbaar en degelijk uitgevoerd: gestructureerde logging, globale exception-handling, DPAPI-secretstore, dagelijkse SQLite-backups, append-only audit trail, PBKDF2-wachtwoorden, soft-delete query filters, en een CI die build+test+vulnerability-scan afdwingt. De God-class-zorg rond `OfferteViewModel` is grotendeels achterhaald: die is opgesplitst in samenwerkende deel-VM's (Prijzen/Workflow/KlantSelectie/Regelbeheer). TODO/HACK-markers zijn vrijwel afwezig (2 stuks). Secrets en `.db` staan correct in `.gitignore`.

De belangrijkste openstaande risico's clusteren rond twee thema's: **(1) concurrency/multi-user** — exact het domein van de nog openstaande US-38, en **(2) autorisatie die inconsistent op de UI-laag i.p.v. de service-laag zit**, met een paar echte gaten. Daarnaast is er één concrete deployment-blokker: er wordt **demo-klantdata in productie geseed** bij elke start.

### Algehele gezondheid: **B+ / groen met kanttekeningen**

### Top 5 prioriteiten
1. **Voorraad-concurrency dichttimmeren (US-38):** `TypeLijst` mist een `RowVersion`-token terwijl `StockService` read-modify-write doet op voorraadvelden → lost-update-risico onder multi-user/PostgreSQL. Plus geen unieke constraint op `FactuurNummer` → dubbele factuurnummers bij gelijktijdig aanmaken. **(Kritiek/Hoog, blokkeert PostgreSQL-migratie.)**
2. **Demo-klanten niet in productie seeden:** `DbSeeder.SeedDemoData` voegt fictieve klanten (Jan Peeters, Sofie Vermeulen …) toe bij elke lege-DB start (`App.axaml.cs:480`). **(Hoog, deployment-blokker.)**
3. **Autorisatie-gaten sluiten:** permanent verwijderen in het archief, afwerkingsoptie verwijderen en klant archiveren zijn niet permissie-gated; `Permissie.Factureren` wordt nergens afgedwongen. Bij voorkeur de gating naar de service-laag verplaatsen. **(Hoog.)**
4. **Testdekking voor `StockService`/voorraadflows:** de meest toestand- en concurrency-gevoelige logica heeft geen enkele test. **(Hoog.)**
5. **`DateTime.Now`→`UtcNow` normaliseren + PostgreSQL-schemastrategie gelijktrekken + `DEPLOYMENT_CHECKLIST` opstellen.** **(Middel.)**

---

## Bevindingen per domein

Legenda severity: **Kritiek / Hoog / Middel / Laag**. Inspanning: **S** (< ½ dag) / **M** (½–2 dagen) / **L** (> 2 dagen).
Elke bevinding is gemarkeerd als **[bug/risico]** of **[smaak/nice-to-have]**.

### 1. Architectuur & lagen

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 1.1 | Middel — [risico] | `ViewModels/**` (o.a. `Offerte/OfferteViewModel.cs:975-1040`, `Klanten/KlantenViewModel.cs:327`, `Planning/PlanningUitvoeringViewModel.cs:364-373`, `Planning/PlanningCalendarViewModel.cs:312-340`) | Meerdere ViewModels gebruiken **rechtstreeks `IDbContextFactory`/`AppDbContext`** voor business-schrijfacties (regels verwijderen, klant archiveren, taak verwijderen, geblokkeerde dagen) i.p.v. via een domeinservice. Dit doorbreekt de MVVM/Service/Data-scheiding en is precies waarom autorisatie in de VM's verspreid zit (zie domein 4). | Business-writes achter services zetten (bv. `IKlantService`, uitbreiding `IWorkflowService`), VM's alleen laten lezen/binden. | L |
| 1.2 | Laag — [smaak] | `ViewModels/Offerte/OfferteViewModel.cs` (1361 regels) | "God-class" is grotendeels al **opgesplitst** in deel-VM's (`Prijzen`, `Workflow`, `KlantSelectie`, `Regelbeheer` — regels 91-99). Blijft een grote coördinator, maar de zorg uit eerdere analyses is grotendeels geadresseerd. | Optioneel: `SaveCoreAsync`/`RefreshLijstPrijzenAsync` verder naar een service tillen. | M |
| 1.3 | Middel — [risico] | `App.axaml.cs:98-212` | **DI-lifetimes:** domeinservices staan als `AddScoped`, maar er is één root-provider en VM's zijn `Transient`; zonder expliciete scope resolven scoped services als de-facto singletons. Onschadelijk zolang álle services hun context via `IDbContextFactory` maken (dat is het geval), maar de `Scoped`-labels zijn misleidend. `BuildServiceProvider()` draait zonder `validateScopes`/`validateOnBuild`. | Overweeg `AddTransient` voor de stateless services óf expliciete scopes; zet `ServiceProviderOptions{ ValidateOnBuild = true }` aan om registratiefouten vroeg te vangen. | S |
| 1.4 | Laag — [observatie] | `App.axaml.cs:122-210` | DI-registraties zijn compleet en consistent; geen dubbele of ontbrekende registraties gevonden. `AuthService` terecht `Singleton` (app-brede `CurrentUser`). | — | — |

### 2. Correctheid & risico's

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 2.1 | **Hoog** — [risico] | `Model/DB/TypeLijst.cs` (geen `RowVersion`) vs `Service/StockService.cs:67-68,121,308,360-361` | **Lost-update op voorraad.** `Offerte/Factuur/WerkBon/WerkTaak` hebben een `[Timestamp] RowVersion`, maar `TypeLijst` niet. `StockService` doet overal read-modify-write op `VoorraadMeter`/`GereserveerdeVoorraadMeter`/`InBestellingMeter`. De `catch (DbUpdateConcurrencyException)` in `StockService` kan voor `TypeLijst` dus **nooit** afgaan. Onder PostgreSQL/multi-user (US-38) kunnen twee gebruikers dezelfde voorraad dubbel verbruiken/reserveren. | `RowVersion` toevoegen aan `TypeLijst` (SQLite `BLOB`/PG `xmin`), of voorraad muteren via atomaire `UPDATE ... SET x = x - @n WHERE ...`. | M |
| 2.2 | **Hoog** — [risico] | `Service/FactuurWorkflowService.cs:415` + `Data/AppDbContext.cs:246-256` | **Race op factuurnummer.** `VolgNr = MAX(VolgNr)+1` per jaar, zonder unieke DB-constraint op `FactuurNummer` of `(Jaar, VolgNr)`. Twee gelijktijdige facturen krijgen hetzelfde nummer. | Unieke index op `(Jaar, VolgNr)` én `FactuurNummer`; nummer toekennen binnen transactie/met retry. | S |
| 2.3 | Middel — [risico] | `Service/Security/AuthService.cs:57`, `Service/GdprService.cs:60,101,134`, `Service/Import/TypeLijstImportCommitter.cs:68,83`, `Model/DB/Gebruiker.cs:28`, `Model/DB/TypeLijst.cs:64` | **`DateTime.Now` vs `UtcNow` inconsistent.** `SaveChangesAsync` (`AppDbContext.cs:416`) en `StockService` gebruiken `UtcNow`; auth-login, GDPR (incl. retentie-cutoff), imports en modeldefaults gebruiken lokale `DateTime.Now`. Fout op een UTC-server (PostgreSQL) en bij GDPR-bewaartermijnen. | Overal `DateTime.UtcNow`; UI converteert naar lokale tijd bij weergave. | M |
| 2.4 | Laag — [smaak] | `Views/*.axaml.cs` (`FactuurPreviewWindow:17`, `InstellingenWindow:30,39`, `PlanningCalendarWindow:47,56`, `WerkBonLijstView:15`) | 6× `async void` in code-behind event-handlers. Aanvaardbaar voor UI-handlers, maar exceptions leunen op de globale handler. | Waar mogelijk `try/catch` binnen de handler of commands binden i.p.v. events. | S |
| 2.5 | Laag — [observatie] | `WorkflowService.Tests/*`, `ViewModels/Offerte/OffertePrijsViewModel.cs:72`, `Data/AppDbContext.cs:436` | Lege `catch`-blokken zijn beperkt tot test-cleanup, een bewuste `TaskCanceledException`-swallow (debounce) en de gedocumenteerde audit-swallow. Geen problematische lege catches in productiepaden. | — | — |
| 2.6 | Laag — [observatie] | `App.axaml.cs:63-72, 218-223, 260-281` | Startup/exception-handling is degelijk: DB-init draait op de achtergrond ná het tonen van het venster (geen UI-freeze/deadlock meer), met foutmelding via `InitError`. Geen `.Result`/`.GetAwaiter().GetResult()` in productiecode (de 2 treffers zijn dialoog-`tcs.TrySetResult(dlg.Result)`, geen blocking). | — | — |

### 3. Datalaag

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 3.1 | Middel — [risico] | `Data/AppDbContext.cs:246-256` | Geen unieke constraint op `FactuurNummer` (alleen `MaxLength(20)`). Zie 2.2. | Unieke index. | S |
| 3.2 | Middel — [risico/perf] | `Service/FactuurWorkflowService.cs:36-51, 335-352` | **Brede Include-ketens** (14× `.Include(...).ThenInclude(r => …)` op de `Regels`-collectie) zonder `AsSplitQuery()`. Bij meerdere regels een cartesiaanse rij-explosie in één query → trager en veel dubbele rijen. Functioneel correct (geen N+1), maar schaalt slecht. | `AsSplitQuery()` op deze zware laadpaden. | S |
| 3.3 | Laag — [smaak] | `Data/AppDbContext.cs:207-209` vs rest | `Offerte` gebruikt `HasColumnType("decimal(18,2)")` terwijl alle andere entiteiten `HasPrecision(...)` gebruiken. Cosmetisch inconsistent (op SQLite sowieso als TEXT opgeslagen). | Uniform `HasPrecision`. | S |
| 3.4 | Laag — [observatie] | `Data/AppDbContext.cs:54-60, 314-397` | Soft-delete query filters zijn compleet aanwezig; `OfferteRegel` gebruikt correct 6× `NoAction` om multiple-cascade-paths te vermijden. `LeveranciersViewModel:401` gebruikt bewust `IgnoreQueryFilters()`. Geen accidentele soft-delete-omzeiling gevonden. Filters op `LeverancierBestelLijn`/`VoorraadMutatie` verwijzen naar `.TypeLijst.IsGearchiveerd` (navigatie) — werkt omdat de FK required/`Restrict` is. | — | — |
| 3.5 | Laag — [smaak] | brede sweep (`AsNoTracking` 52×) | Read/export-paden gebruiken `AsNoTracking`, maar sommige lijst-VM's laden tracked en bewerken daarna. Kleine geheugen-/perf-winst mogelijk op puur-lees-lijsten. | `AsNoTracking` op alle read-only lijstladingen. | S |

### 4. Beveiliging & autorisatie

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 4.1 | **Hoog** — [risico] | `ViewModels/ArchiefViewModel.cs:149-163` | **Permanent verwijderen** van een gearchiveerde offerte zonder enige permissie-check. Onomkeerbare verwijdering van financiële/offerte-records staat open voor elke ingelogde gebruiker. | Gate achter een permissie (bv. nieuwe `ArchiefBeheer` of hergebruik `LijstVerwijderen`), bij voorkeur in een service. | S |
| 4.2 | Middel — [risico] | `ViewModels/Afwerkingen/AfwerkingenViewModel.cs:515-536` (`DeleteOptieAsync`) | Verwijderen van een afwerkingsoptie (prijs-masterdata) is **niet** gegate door `PrijzenWijzigen`; alleen een prijs-edit-pad (regel 401) is gegate. | `HeeftPermissie(PrijzenWijzigen)` toevoegen aan verwijderpad. | S |
| 4.3 | Middel — [risico] | `ViewModels/Klanten/KlantenViewModel.cs:327-357` | Klant archiveren zonder permissie-check (er bestaat geen `KlantVerwijderen`-permissie). GDPR-anonimisering is wél gegate, gewone archivering niet. | Permissie invoeren of hergebruiken; overweeg service-laag. | S |
| 4.4 | Middel — [risico] | `Service/Security/Permissie.cs:12`, `Service/FactuurWorkflowService.cs`, `ViewModels/Factuur/FacturenViewModel.cs` | **`Permissie.Factureren` wordt nergens afgedwongen** (enkel in tests en als UI-label). De factuur-aanmaakpaden checken hem niet. Nu benign (Medewerker mag toch factureren), maar het is een dode permissie en een gat zodra men factureren wil beperken. | Check toevoegen op het factuur-aanmaakpad (service-laag). | S |
| 4.5 | Middel — [risico] | `ViewModels/Lijsten/LijstenViewModel.cs:315,430`, `Leverancier/LeveranciersViewModel.cs:387`, `InstellingenViewModel.cs:63`, `Afwerkingen/AfwerkingenViewModel.cs:401` | **Autorisatie zit op de UI-laag** (`HeeftPermissie` in VM's), terwijl GDPR/gebruikersbeheer het correct op de **service-laag** afdwingen (`VereisPermissie` → throw, `GdprService.cs:39,79,125` / `AuthService.cs:109,116,145`). Inconsistent; elk nieuw/alternatief schrijfpad kan de VM-guard omzeilen. | Prijs-/verwijder-/factuur-checks naar de service-laag (`VereisPermissie`) tillen. | M |
| 4.6 | Middel — [risico] | `Service/Security/AuthService.cs:19-20,180` | Standaard-admin `admin`/`quadro` hardcoded; `MoetWachtwoordWijzigen=true` forceert wijziging bij eerste login (`MainWindowViewModel.cs:153-157`, waarschuwings-toast). Restrisico als de geforceerde wijziging in de UI overslaanbaar is. | Zorg dat de app vergrendeld blijft tot het wachtwoord daadwerkelijk gewijzigd is; documenteer in checklist. | S |
| 4.7 | Laag — [observatie] | `Data/SqliteSchemaPatcher.cs:60-291`, `Service/Import/ImportService.cs:229-231`, `Scripts/MigrationTool/Program.cs:124-130` | Raw SQL is uitsluitend `ExecuteSqlRawAsync` met statische/compile-time strings; interpolatie enkel met interne constanten (`baseline`, `historyTable`, schema-tabelnamen) — **geen user-input** → geen SQLi. Correct gemarkeerd met `#pragma warning disable EF1002` + toelichting. | — | — |
| 4.8 | Laag — [observatie] | `.gitignore` (+ `git ls-files`) | `appsettings.json`, `appsettings.*.json` en `*.db` staan in `.gitignore` en zijn **niet** getrackt (geverifieerd). Alleen `appsettings.example.json` is gecommit. US-33 secretstore + `__SECRET__`-placeholder. Login logt enkel gebruikersnaam, geen wachtwoord. | — | — |

### 5. Redundantie & dode code

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 5.1 | Laag — [nice-to-have] | `docs/cleanup-candidates.md` | Dode code is netjes gedocumenteerd maar nog niet verwijderd: `Views/LoginWindow.axaml(.cs)` + `ViewModels/LoginViewModel.cs` (vervangen door het login-overlay in `MainWindow`), plus legacy import-dialogen (`ImportPreviewWindow`, `KlantImportPreviewWindow`, `AfwerkingImportPreviewWindow`) en de bijbehorende `IDialogService`-methodes. | Na de runtime-smoke-checklist verwijderen. | S |
| 5.2 | Laag — [nice-to-have] | `QuadroApp.csproj:32,34` | **Twee Excel-libraries**: `ClosedXML 0.105.0` én `EPPlus 8.4.1`. De unified import gebruikt `ClosedXmlExcelParser`; `cleanup-candidates.md` markeert EPPlus voor review. | Bevestig of EPPlus nog gebruikt wordt (export?); anders verwijderen. | S |
| 5.3 | Laag — [observatie] | `ViewModels/Planning/*` | Slechts één week-overzicht aanwezig (`WeekWerkLijstViewModel`); de eerder vermoede overlappende week-overzichten lijken al geconsolideerd. Wel enige functionele overlap tussen `PlanningCalendarViewModel`/`PlanningUitvoeringViewModel`/`WeekWerkLijstViewModel` — bewust opgesplitst, geen duplicaat gevonden. | Geen actie; bij twijfel handmatig verifiëren. | — |

### 6. Tests

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 6.1 | **Hoog** — [risico] | `WorkflowService.Tests/` (18 bestanden, ~109 `[Fact]`/`[Theory]`) | **Geen enkele test voor `StockService`/voorraadflows** (`Reserve/Consume/Release`, `PlaceSupplierOrder`, `ReceiveSupplierOrderLine`). Dit is de meest toestand- en concurrency-gevoelige logica en tegelijk de minst geteste. Ook `WerkBonWorkflowService` en de planning-VM's zijn ongetest. | Testsuite voor de voorraad-lifecycle toevoegen (SQLite in-memory/file), inclusief negatieve paden. | L |
| 6.2 | Hoog — [risico] | idem | **Geen concurrency-/lost-update-tests** (US-38). Zie 2.1/2.2. | Test die twee contexts dezelfde voorraad/factuur laat muteren en de `RowVersion`/unique-constraint verifieert. | M |
| 6.3 | Laag — [observatie] | `PricingEngineTests`, `OffertePricingDraftTests`, `FactuurWorkflowServiceTests`, `AuthServiceTests`, `GdprServiceTests`, `AuditTrailTests`, `MigrationSafetyTests`, `ImportServiceTests`, `BackupServiceTests` | Kernlogica pricing/facturatie/auth/gdpr/audit/migratie/import is degelijk gedekt (positief + enkele negatieve gevallen). Test-cleanup gebruikt temp-dirs met lege `catch` — aanvaardbaar. Contexttelling (~109) ligt iets onder de vermelde "119+"; `[Theory]`-cases verklaren wellicht het verschil (`verify.ps1` geeft de exacte telling). | — | — |

### 7. Consistentie & onderhoudbaarheid

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 7.1 | Laag — [smaak] | codebreed | **NL/EN-mengeling** in naamgeving (`StockService` op `Voorraad*`-velden, `DeleteAsync` vs `VerwijderAsync`, `BuildLijnen`). Consistent binnen domein, maar gemengd. | Conventie kiezen (NL voor domein, EN voor infra) en geleidelijk toepassen. | M |
| 7.2 | Laag — [observatie] | `Service/PdfFactuurExporter.cs:25` | Slechts **2** TODO/HACK/FIXME in de hele codebase (1 echte: "bevestig juiste uren met Veerle"; 1 is een doc-voorbeeld). Zeer schoon. | TODO afhandelen op de meeting. | S |
| 7.3 | Laag — [risico] | `QuadroApp.csproj:5` (Nullable enable), geen `TreatWarningsAsErrors`, ~50 `!` null-forgiving in app-code | Nullable staat aan, maar warnings falen de build niet (CI toont ze enkel: `ci.yml` "warnings visible"). ~50 null-forgiving operators (vooral in Include-ketens) verbergen mogelijk echte null-gaten. Kon hier niet worden geteld door ontbrekende SDK. | Warning-budget invoeren; overweeg `TreatWarningsAsErrors` op nieuwe code; `!` gericht vervangen door guards. | M |
| 7.4 | Laag — [smaak] | `App.axaml.cs:292-296` | Doc-drift: comment zegt "swap `UseSqlite → UseNpgsql` in de DI-registratie", maar de provider wordt al **automatisch** gedetecteerd (`App.axaml.cs:96-111`). | Comment bijwerken. | S |

### 8. Deployment-gereedheid

| # | Severity | Bestand:regel | Bevinding | Voorgestelde fix | Insp. |
|---|----------|---------------|-----------|------------------|-------|
| 8.1 | **Hoog** — [risico] | `App.axaml.cs:480` + `Data/DatabaseSeeder.cs:33-` | **Demo-klantdata in productie.** `DbSeeder.SeedDemoData` seedt fictieve klanten (Jan Peeters, Sofie Vermeulen, …) bij elke start met lege `Klanten`-tabel. Een verse productie-installatie toont zo nep-klanten. (De leveranciers ICO/HOF/FRA/BOL zijn wél legitieme referentiedata; `Leverancier.Naam` `MaxLength(3)` is een bewuste 3-letter-code.) | Klant-seeding achter een dev/DEBUG-flag zetten of verwijderen; enkel referentiedata (leveranciers/afwerkingsgroepen) seeden. | S |
| 8.2 | Hoog — [risico] | US-38 (open) | Concurrency/multi-user is nog niet af — zie 2.1, 2.2, 6.2. **Blokkeert** veilige PostgreSQL-multi-user-go-live. | US-38 afronden vóór PostgreSQL live gaat. | L |
| 8.3 | Middel — [risico] | `App.axaml.cs:495-517` | **Divergente schemastrategie tussen providers:** PostgreSQL via `EnsureCreatedAsync` (geen migratiehistorie, "schema-wijzigingen als raw SQL patches"), SQLite via echte EF-migraties (`SqliteSchemaPatcher`). Risico op schema-drift tussen beide providers en op onbeheerde PG-schemawijzigingen. | Eén strategie kiezen (bij voorkeur EF-migraties ook voor PostgreSQL) vóór de migratie. | M |
| 8.4 | Middel — [nice-to-have] | `docs/` | **Geen `DEPLOYMENT_CHECKLIST`** aanwezig (wel README/CONFIGURATION/AUTH/AUDIT/GDPR/BACKUP_RESTORE). | Checklist opstellen: wachtwoord wijzigen, demo-data uit, backupdir, secret zetten, PG-migratie, smoke-test. | S |
| 8.5 | Laag — [observatie] | `.github/workflows/*.yml`, `QuadroApp.csproj:9-13,55` | Release-pipeline is solide: multi-RID self-contained publish (win-x64 + osx-arm64), Velopack auto-update, auto-tag, CI met vuln-scan. macOS-paden (Application Support) correct afgehandeld. `--outdated` draait níét in CI (alleen `--vulnerable`). | Optioneel `--outdated` als niet-blokkerende CI-stap. | S |

---

## Prioriteitentabel (severity × inspanning) — suggestie voor vervolg-branches

| Prioriteit | Bevinding(en) | Severity | Insp. | Voorgestelde branch |
|-----------|---------------|----------|-------|---------------------|
| P1 | 2.1 `TypeLijst.RowVersion` + 2.2/3.1 unieke `FactuurNummer` + 6.2 concurrency-tests | Kritiek/Hoog | M | `feature/us38-concurrency-stock-factuur` |
| P2 | 8.1 demo-klanten uit productie-seed | Hoog | S | `fix/seed-demo-data-guard` |
| P3 | 4.1/4.2/4.3/4.4/4.5 autorisatie-gaten + naar service-laag | Hoog | M | `feature/authz-serverside-gating` |
| P4 | 6.1 `StockService`-testsuite | Hoog | L | `test/stockservice-coverage` |
| P5 | 2.3 `DateTime.UtcNow`-normalisatie | Middel | M | `fix/datetime-utc-normalization` |
| P6 | 8.3 PostgreSQL-schemastrategie gelijktrekken | Middel | M | `feature/pg-migration-strategy` |
| P7 | 3.2 `AsSplitQuery` op factuur-/offerte-laadpaden | Middel | S | `perf/split-queries` |
| P8 | 1.1 business-writes uit VM's naar services | Middel | L | `refactor/vm-to-service-writes` |
| P9 | 8.4 `DEPLOYMENT_CHECKLIST` + 7.4 doc-drift | Middel/Laag | S | `docs/deployment-checklist` |
| P10 | 5.1/5.2 dode code + EPPlus-review, 1.3 DI-validatie, 7.3 warning-budget | Laag | S–M | `chore/cleanup-and-hygiene` |

---

## Onderscheid bug/risico vs smaak/nice-to-have (samenvattend)

**Echte bugs/risico's:** 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 6.1, 6.2, 8.1, 8.2, 8.3.
**Smaak/nice-to-have:** 1.2, 3.3, 3.5, 5.1, 5.2, 7.1, 7.2, 7.4, 8.5, en de architectuur-verbeteringen 1.1/1.3 (structureel, geen acute bug).

---

## De 5 belangrijkste acties (afsluitend)

1. **US-38 afmaken:** `RowVersion` op `TypeLijst` + unieke constraint op `FactuurNummer`/`(Jaar,VolgNr)`, met concurrency-tests. Blokkeert PostgreSQL-multi-user.
2. **Demo-klantdata uit de productie-seed** halen/achter een dev-flag zetten.
3. **Autorisatie-gaten sluiten** (permanent verwijderen, afwerking verwijderen, klant archiveren, `Factureren` afdwingen) en de gating naar de service-laag verplaatsen.
4. **`StockService`-testsuite** bouwen voor de voorraad-lifecycle.
5. **`DateTime.UtcNow` normaliseren** en de **PostgreSQL-schemastrategie** gelijktrekken, plus een `DEPLOYMENT_CHECKLIST` toevoegen.

> **Vraag aan jou:** welke van deze vijf wil je als eerste als aparte werkbranch laten oppakken? Mijn advies is **P1 (`feature/us38-concurrency-stock-factuur`)**, omdat het zowel een echt datarisico afdekt als de PostgreSQL-migratie deblokkeert — maar **P2 (demo-data)** is een snelle, losstaande win die je er meteen naast kunt doen.
