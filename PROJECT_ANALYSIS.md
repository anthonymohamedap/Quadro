# QuadroApp — Project Analysis
*Updated: 2026-03-17 | Branch: `styling` | Analyst: Claude*

---

## 1. Project Overview

**QuadroApp** is a Belgian desktop business application for a picture-framing / interior-finishing company. It manages the full order lifecycle: customer quotes (offertes) → work orders (werkbonnen) → invoices (facturen), plus stock tracking, planning, supplier/product catalogue management, and a weekly work schedule.

- **Platform:** .NET 9, Avalonia UI 11.3 (cross-platform desktop)
- **Architecture pattern:** MVVM (Model-View-ViewModel)
- **Database:** SQLite via Entity Framework Core 9 (`quadro.db`)
- **DI framework:** Microsoft.Extensions.DependencyInjection
- **Language:** C# 13 with `Nullable enable`
- **Publishing:** Single-file, self-contained executable

---

## 2. Solution Layout

```
Quadro.sln
└── QuadroApp/                   ← main project (single executable)
    ├── App.axaml / App.axaml.cs ← DI bootstrap, startup, global error handling
    ├── Program.cs               ← entry point (STA thread, Avalonia host)
    ├── MainWindow.axaml         ← shell window
    ├── Data/                    ← EF Core data layer
    ├── Model/                   ← domain entities + import models
    ├── Service/                 ← business logic + service interfaces
    ├── ViewModels/              ← MVVM view models (20 files)
    ├── Views/                   ← Avalonia AXAML views & windows
    ├── Converters/              ← IValueConverter implementations (7 files)
    ├── Validation/              ← entity validators
    ├── Styles/                  ← custom Avalonia theme (QuadroTheme.axaml)
    └── Assets/                  ← logos, images
WorkflowService.Tests/           ← xUnit test project (separate .csproj)
```

---

## 3. Dependency Graph (High Level)

```
Program.cs
  └── App.axaml.cs  (DI container built here)
        ├── Data/AppDbContext  (EF Core, SQLite)
        ├── Service/*          (business logic)
        │     ├── Pricing/PricingEngine
        │     ├── Import/Enterprise/*  (Excel import pipeline)
        │     │     ├── Klant pipeline
        │     │     ├── TypeLijst pipeline
        │     │     └── AfwerkingsOptie pipeline
        │     ├── WorkflowService      (offerte state machine + stock + besteld)
        │     ├── OfferteWorkflowService
        │     ├── WerkBonWorkflowService
        │     └── FactuurWorkflowService
        └── ViewModels/*
              └── bound to Views/* (AXAML)
```

---

## 4. Domain Model (Model/DB/)

### Core Entities

| Entity | Key Fields | Relations |
|---|---|---|
| `Klant` | Id, Voornaam, Achternaam, Email, Telefoon, Adres, BtwNummer | → many `Offerte` |
| `Leverancier` | Id, Naam (max 3 chars, unique) | → many `TypeLijst` |
| `TypeLijst` | Id, Artikelnummer, BreedteCm, PrijsPerMeter, VoorraadMeter, MinimumVoorraad, Soort | → `Leverancier`; used in `OfferteRegel`, `WerkTaak` |
| `AfwerkingsGroep` | Id, Code (G/P/D/O/R), Naam | → many `AfwerkingsOptie` |
| `AfwerkingsOptie` | Id, Naam, Volgnummer, KostprijsPerM2, WinstMarge, AfvalPercentage, WerkMinuten | → `AfwerkingsGroep`, optional `Leverancier` |
| `Offerte` | Id, KlantId, Status (enum), Datum, totalen, KortingPct, MeerPrijsIncl, VoorschotBedrag | → `Klant`, → many `OfferteRegel`, → 0..1 `WerkBon` |
| `OfferteRegel` | Id, OfferteId, AantalStuks, Breedte/HoogteCm, 6× AfwerkingsOptie FK, prijsvelden, ExtraWerkMinuten | → `TypeLijst`, 6× `AfwerkingsOptie` |
| `WerkBon` | Id, OfferteId, Status (enum), TotaalPrijsIncl, StockReservationProcessed | → `Offerte`, → many `WerkTaak` |
| `WerkTaak` | Id, WerkBonId, OfferteRegelId, GeplandVan/Tot, DuurMinuten, BenodigdeMeter, IsBesteld, BestelDatum, IsOpVoorraad, **WeekNotitie** | → `WerkBon`, → `OfferteRegel` |
| `Factuur` | Id, WerkBonId, FactuurNummer, DocumentType, KlantNaam, Status (enum), totalen, Lijnen | → `WerkBon`, → many `FactuurLijn` |
| `FactuurLijn` | Id, FactuurId, Omschrijving, Aantal, PrijsExcl, BtwPct, totalen | → `Factuur` |
| `Instelling` | Sleutel (PK), Waarde | Key-value settings store |
| `ImportSession` | Id, EntityName, FileName, totals, Status | Audit log of imports → many `ImportRowLog` |
| `ImportRowLog` | Id, ImportSessionId, RowNumber, Key, Success, IssuesJson | Per-row import result |

**New since last analysis:** `WerkTaak.WeekNotitie` (`[MaxLength(2000)]`) — per-task free-text note shown and saved from the weekly work list screen. `TypeLijst.Soort` — used for list-type display in planning week rows.

### Status Enums

**OfferteStatus:** `Concept → Verzonden → Goedgekeurd → InProductie → Afgewerkt → Gefactureerd → Betaald` (or `Geannuleerd`)

**WerkBonStatus:** `Gepland → InUitvoering → Afgewerkt → Afgehaald`

**FactuurStatus:** `Draft → KlaarVoorExport → Geexporteerd → Betaald → Geannuleerd`

---

## 5. Data Layer (Data/)

### AppDbContext
- **DbSets:** TypeLijsten, AfwerkingsGroepen, AfwerkingsOpties, Offertes, WerkBonnen, WerkTaken, OfferteRegels, Klanten, Leveranciers, ImportSessions, ImportRowLogs, Facturen, FactuurLijnen, Instellingen
- **Precision** configured for all decimal fields (10,2 or 18,2)
- **Delete behaviors:** Cascade for parent-child (Offerte→WerkBon, WerkBon→WerkTaak, etc.), NoAction for the 6 AfwerkingsOptie FKs on OfferteRegel (to avoid multiple cascade paths in SQLite)
- **SaveChangesAsync override:** Auto-sets `WerkTaak.GeplandTot = GeplandVan + max(1, DuurMinuten)` and updates `WerkBon.BijgewerktOp` on modification
- **Status columns:** Stored as strings via `.HasConversion<string>()`
- **Optimistic concurrency:** `[Timestamp] RowVersion` on `WerkBon`, `WerkTaak`, `Factuur`
- **Indices:** `WerkTaak` indexed on `GeplandVan` and `WerkBonId` for fast planning queries

### Database Initialization (App.axaml.cs)
- **⚠️ CRITICAL:** On every app startup, `EnsureDeletedAsync()` + `EnsureCreatedAsync()` is called — **this wipes the entire database on each launch**. Demo data is re-seeded via `DbSeeder.SeedDemoData()`. This must be replaced with proper migration-based startup before production use.
- The legacy `AppServices.Init()` (in `AppServices.cs`) still exists alongside the main DI path and also does `EnsureDeleted` — dead code remnant.

### DatabaseSeeder
Seeds: 4 Leveranciers (ICO, HOF, FRA, BOL), demo Klanten, AfwerkingsGroepen with Opties, TypeLijsten, and sample Offertes.

---

## 6. Service Layer (Service/)

### Dependency Injection Lifetimes

| Service | Lifetime | Notes |
|---|---|---|
| `INavigationService` | Singleton | Holds current VM reference |
| `IOfferteNavigationService` | Singleton | Offerte-specific navigation |
| `IWindowProvider` | Singleton | Manages window references |
| `IFilePickerService` | Singleton | File open/save dialogs |
| `IToastService` | Singleton | In-app notifications |
| `IDialogService` | Singleton | Generic modal dialogs |
| `IKlantDialogService` | Transient | Klant create/edit dialogs |
| `ILijstDialogService` | Transient | TypeLijst create/edit dialogs |
| `IAfwerkingenService` | Scoped | |
| `IWorkflowService` | Scoped | Main state machine |
| `IOfferteWorkflowService` | Scoped | |
| `IWerkBonWorkflowService` | Scoped | |
| `IFactuurWorkflowService` | Scoped | |
| `IFactuurExportService` | Scoped | |
| `IFactuurExporter` (PDF) | Scoped | QuestPDF implementation |
| `PricingEngine` | Singleton | Pure calculation, stateless |
| `IPricingSettingsProvider` | Singleton | Reads settings from DB |
| `IPricingService` | Singleton | Wraps PricingEngine |
| `IAppSettingsProvider` | Singleton | App-level key-value settings |
| Import pipeline services | Transient | Parser, maps, validators, committers (×3 entities) |
| Legacy import services | Transient | Candidates for removal |
| Validators | Scoped/Transient | Per-entity CRUD validators |
| ViewModels | Singleton (Main) / Transient | |

### WorkflowService (the state machine)
Implements strict `OfferteStatus` transitions via a lookup dictionary. Key behaviors:
- **Goedgekeurd transition:** Automatically creates a `WerkBon` if none exists (idempotent)
- **Stock reservation (`ReserveStockForWerkBonAsync`):** Validates `BenodigdeMeter > 0`, checks available stock per `WerkTaak`, decrements `VoorraadMeter`, sets `IsOpVoorraad`, raises warnings if below `MinimumVoorraad`; idempotent via `StockReservationProcessed` flag
- **`MarkLijstAsBesteldAsync`:** Sets `IsBesteld + BestelDatum` on a WerkTaak; used from both WeekWerkLijstWindow and WerkBonLijstView
- **Offerte→WerkBon→Factuur lifecycle** is managed across three specialized workflow services

### WerkBonWorkflowService
- **`VoegPlanningToeVoorRegelAsync`:** Creates a `WerkTaak` for a given WerkBon + OfferteRegel at a specific `DateTime` with a calculated duration. Called from `PlanningCalendarViewModel`.

### PricingEngine (Service/Pricing/)
Pure calculation class (no DB dependency). For each `OfferteRegel` it computes:
1. **Lijst price:** perimeter × PrijsPerMeter + waste% + labour (min/hr × uurloon) + VasteKost
2. **Afwerking price (×6 slots):** m² × KostprijsPerM2 + VasteKost + waste% + labour
3. **Extra costs/discounts** per regel (including `ExtraWerkMinuten`)
4. **Offerte-level:** discount (KortingPct), MeerPrijsIncl, BTW calculation, VoorschotBedrag clamping
5. Distinguishes Staaflijst vs. non-staaflijst for different waste/profit factor settings

### Import Pipeline (Service/Import/Enterprise/)
Generic, interface-driven pipeline:
- `IExcelParser` (`ClosedXmlExcelParser`) → reads rows as `Dictionary<string, string?>`
- `IExcelMap<T>` → defines columns, parsers, key extraction, `ApplyCell`
- `IImportValidator<T>` → async per-row validation against DB
- `IImportCommitter<T>` → upsert rows into DB with tracking
- `ImportService` orchestrates: **DryRun** (validation only) then **Commit** (transactional, with audit logging to `ImportSession`/`ImportRowLog`)
- **Implemented for all 3 entity types:** `Klant`, `TypeLijst`, `AfwerkingsOptie`

The legacy import services have been removed; the Enterprise import pipeline is now the active import path.

### Other Services
- **NavigationService:** Service-locator navigation using `IServiceProvider`; supports typed navigation with parameter passing via `IParameterReceiver<TParam>` and `IAsyncInitializable`
- **ToastService:** Shows timed in-app toast notifications (Success/Error/Warning/Info)
- **DialogService / KlantDialogService / LijstDialogService:** Modal dialog helpers
- **PdfFactuurExporter:** Uses QuestPDF (community license) to export invoices/orders as PDF
- **AppSettingsProvider / PricingSettingsProvider:** Read `Instelling` key-value pairs from DB
- **LegacyAfwerkingCode:** Utility for encoding/decoding the G-P-P-D-O-R afwerking code string

---

## 7. Presentation Layer (Views & ViewModels)

### Screen Inventory

| ViewModel | View/Window | Purpose |
|---|---|---|
| `MainWindowViewModel` | `MainWindow` | Shell, hosts navigation area |
| `HomeViewModel` | `HomeView` | Dashboard / landing page |
| `LoginViewModel` | `LoginWindow` | User authentication |
| `KlantenViewModel` | `KlantenView` | Customer list & management (with Excel import) |
| `KlantDetailViewModel` | `KlantDetailView` | Single customer detail |
| `LeveranciersViewModel` | `LeveranciersView` | Supplier list |
| `LijstenViewModel` | `LijstenView` | TypeLijst (product/frame list) management |
| `AfwerkingenViewModel` | `AfwerkingenView` | Finishing options management |
| `OffertesLijstViewModel` | `OffertesLijstView` | Quote list (Concept status) |
| `OfferteViewModel` | `OfferteView` | Quote detail editor (klant, regels, afwerkingen, pricing) |
| `WerkBonLijstViewModel` | `WerkBonLijstView` | Work order list with detail panel + bestel-datum workflow |
| `FacturenViewModel` | `FacturenView` | Invoice management |
| `PlanningCalendarViewModel` | `PlanningCalendarWindow` | Full month calendar with day/week detail, task planning |
| `PlanningTijdDialogViewModel` | `PlanningTijdDialog` | Modal dialog for selecting planning time (van/tot) |
| `WeekWerkLijstViewModel` | `WeekWerkLijstWindow` | Weekly work schedule with notities + besteld-marking |
| `BulkLijstenViewModel` | `BulkLijstenWindow` | Bulk list operations |
| `InstellingenViewModel` | `InstellingenWindow` | App settings |
| `ImportPreviewViewModel` | `ImportPreviewView/Window` | Generic import preview |
| `KlantExcelPreviewViewModel` | `KlantImportPreviewWindow` | Klant import preview |
| `AfwerkingExcelPreviewViewModel` | `AfwerkingImportPreviewWindow` | Afwerking import preview |

### PlanningCalendarViewModel — Notable Design
- Displays a **35-tile month grid** (DayTile objects with utilization color coding)
- Week summary sidebar (WeekSummary) and week day-row detail (DayRow, WeekRow)
- Capacity constant: **8 hours (480 min)** per day; tiles colored LimeGreen/Goldenrod/OrangeRed/Red by utilization %
- **Planning flow:** Select OfferteRegels from list → `PlanGeselecteerdeRegelsAsync` → opens `PlanningTijdDialog` → calls `WerkBonWorkflowService.VoegPlanningToeVoorRegelAsync`
- **Herplannen:** Opens time dialog to reschedule an existing WerkTaak in-place
- **`OpenWeekWerkLijstCommand`:** Opens `WeekWerkLijstWindow` for the selected ISO week
- Support classes declared in same file: `DayTile`, `WeekSummary`, `DayRow`, `WeekRow`

### WeekWerkLijstViewModel — Notable Design
- Groups WerkTaken by klant name (`KlantWeekBlock` + `WeekWerkItem`)
- **`MarkeerBesteldCommand`:** Sets `IsBesteld + BestelDatum` on a WerkTaak item (uses `BestelDatumInput` from the item itself)
- **`SaveNotitieCommand`:** Persists `WeekNotitie` text to DB for a specific WerkTaak
- `WeekWerkItem` is a partial `ObservableObject` with observable `Notitie`, `IsBesteld`, `BestelDatum`, `IsOpVoorraad`, `BestelDatumInput`

### Key MVVM Patterns
- `CommunityToolkit.Mvvm` (source-generated `[ObservableProperty]`, `[RelayCommand]`)
- Navigation driven by `INavigationService`; views data-template-mapped to ViewModels
- `IAsyncInitializable` — ViewModels implement `InitializeAsync()` called after navigation
- `IParameterReceiver<T>` — ViewModels receive typed parameters (e.g., klantId)
- **Compiled bindings (`x:DataType`)** used throughout; inside DataTemplates, parent DataContext bindings use explicit type casts: `$parent[T].((VMType)DataContext).Property`
- `Huskui.Avalonia` used for additional UI controls

---

## 8. Converters (Converters/)

| Converter | Purpose |
|---|---|
| `BooleanConverters` | Bool↔visibility, bool inversion |
| `IntToBoolInverseConverter` | `int == 0 → true` |
| `CapacityToBrushConverter` | Capacity % → colored brush (planning calendar tiles) |
| `BoolToThicknessConverter` | Bool → Thickness (slide panel animation) |
| `BoolToDoubleConverter` | Bool → double (opacity animation) |
| `ToastColorConverter` (in `App`) | ToastType → SolidColorBrush |
| `ToastColorConverter` (in `Service/Toast/`) | Duplicate — candidate for consolidation |

---

## 9. Validation Layer (Validation/)

Each entity has an `ICrudValidator<T>` implementation:

| Validator | Entity | Key rules |
|---|---|---|
| `KlantValidator` | `Klant` | Required: Voornaam/Achternaam; email format if provided |
| `TypeLijstValidator` | `TypeLijst` | Required: Artikelnummer, Levcode, LeverancierId, BreedteCm > 0 |
| `AfwerkingsOptieValidator` | `AfwerkingsOptie` | Required: Naam, AfwerkingsGroepId |
| `OfferteValidator` | `Offerte` | Custom `IOfferteValidator` interface |

---

## 10. Testing (WorkflowService.Tests/)

xUnit test project targeting `net9.0`. Uses **in-memory EF Core** (`UseInMemoryDatabase`) for fast, isolated tests.

### Test Coverage

| Test Class | Tests | What's covered |
|---|---|---|
| `WorkflowServiceTests` | 9 tests | Status transitions (valid/invalid), WerkBon creation idempotency, stock reservation (success/insufficient/minimum warning), besteld marking, duplicate reservation prevention, validation exceptions |
| `PricingEngineTests` | 2 tests | Staaflijst vs. non-staaflijst pricing paths |
| `ImportServiceTests` | present | Import pipeline testing |
| `TypeLijstImportCommitterTests` | present | Committer upsert logic |
| `PricingSettingsProviderTests` | present | Settings reading |
| `MigrationSafetyTests` | present | Schema compatibility checks |

---

## 11. NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| Avalonia (+ Controls.DataGrid, Desktop, Themes.Fluent, Fonts.Inter) | 11.3.12 | UI framework |
| Avalonia.Diagnostics | 11.3.12 | Debug inspector (Debug only) |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source generators |
| Microsoft.EntityFrameworkCore + SQLite + SqlServer + InMemory | 9.0.9 | ORM + providers |
| Microsoft.Extensions.DependencyInjection | 9.0.9 | DI container |
| Microsoft.Extensions.Logging | 9.0.0 | Logging |
| ClosedXML | 0.105.0 | Excel reading (Enterprise import pipeline) |
| EPPlus | 8.4.1 | Excel (legacy import path — candidate for removal) |
| QuestPDF | 2024.7.1 | PDF invoice export |
| Huskui.Avalonia | 0.10.3 | Extra Avalonia controls |
| EfCore.SchemaCompare | 8.2.0 | Migration safety tests |
| xunit (assert, core, abstractions) | 2.9.3 | Testing |

---

## 12. Key Observations & Risks

### ⚠️ Critical Issues

1. **Database wiped on every startup** — `EnsureDeletedAsync` + `EnsureCreatedAsync` runs in `OnFrameworkInitializationCompleted`. All data is lost and re-seeded from scratch on every run. Must be replaced with migration-based startup before any production use. Migrations folder exists but is unused at runtime.

2. **Dead code / dual DI path** — `AppServices.cs` (`AppServices.Init()`) contains a second, parallel DI registration and also calls `EnsureDeleted`. It is never called from `App.axaml.cs` and appears to be an obsolete remnant.

### ⚠️ Moderate Issues

3. **Two Excel libraries** — Both `ClosedXML` (Enterprise) and `EPPlus` are present. With the legacy import path removed, `EPPlus` should be reviewed and removed if no other runtime path still needs it.

4. **`Klant` has no explicit MaxLength on most string fields** — unlike other entities. May cause issues if switching to SQL Server.

6. **`OfferteRegel` stores computed price fields** (`SubtotaalExBtw`, `BtwBedrag`, `TotaalInclBtw`) redundantly — re-derived by `PricingEngine`. Risk of stale data if not kept in sync.

7. **Duplicate `ToastColorConverter`** — Defined in both `App` class and `Service/Toast/ToastColorConverter.cs`. Should be consolidated to one.

8. **`PlanningCalendarViewModel` creates ViewModels directly** — `WeekWerkLijstViewModel` and `PlanningTijdDialogViewModel` are `new`'d inside the VM instead of being resolved via DI. This bypasses the DI container and makes testing harder.

### ℹ️ Minor / Cosmetic

9. **`Levcode` field on `TypeLijst`** — max 50 chars but `Leverancier.Naam` is max 3 chars. The relationship and meaning may need clarification.

10. **`WerkTaak` uses local `DateTime`** — comment in code notes future migration to `DateTimeOffset` if timezone support is needed.

11. **`AppServices.cs` global static** — uses a static `IServiceProvider` pattern (`AppServices.Db`) which can cause issues in tests or parallel scenarios. The main DI path in `App.axaml.cs` correctly uses constructor injection.

12. **`WeekNotitie` not yet in migration** — The field was added to the model but since the app uses `EnsureCreated` (no migrations at runtime), this works in demo mode. A proper migration must be authored before switching to migration-based startup.

---

## 13. Recent Changes (since 2026-03-11)

| Area | Change |
|---|---|
| **Model** | `WerkTaak.WeekNotitie` field added (`[MaxLength(2000)]`); `TypeLijst.Soort` property in use |
| **Planning** | `PlanningCalendarViewModel` fully implemented: month-grid (DayTile), week summaries, week rows, day-detail, task planning + herplannen + verwijderen |
| **Planning** | `PlanningTijdDialogViewModel` added: van/tot time picker with duration preview and validation |
| **WeekWerkLijst** | `SaveNotitieCommand` (persist per-task notes) and `MarkeerBesteldCommand` added to `WeekWerkLijstViewModel` |
| **WerkBonLijst** | `MarkeerLijstAlsBesteldCommand` + `GeselecteerdeBestelDatum` added to `WerkBonLijstViewModel` |
| **Import** | Enterprise pipeline extended to `TypeLijst` and `AfwerkingsOptie` (was Klant-only) |
| **Converters** | `BoolToThicknessConverter`, `BoolToDoubleConverter`, `CapacityToBrushConverter` added |
| **XAML** | Compiled-binding parent-DataContext casts fixed across 4 views (KlantenView, OffertesLijstView, WeekWerkLijstWindow, WerkBonLijstView) |
| **XAML** | Explicit `Grid.Column="0"` / `Grid.Row="0"` setters added throughout `OfferteView` to clear analyzer warnings |
| **Styling** | `App.axaml`, `QuadroTheme.axaml`, `OfferteView.axaml` actively modified on `styling` branch |

---

## 14. File Count Summary

| Area | Files |
|---|---|
| Views (AXAML + code-behind) | ~44 files |
| ViewModels | 20 files |
| Services | ~40 files |
| Model/DB entities | 14 files |
| Model/Import models | 10 files |
| Converters | 7 files |
| Validators | 5 files |
| Data layer | 4 files |
| Tests | 6 files |
| **Total source files** | **~150** |

---

*Update this document after significant refactoring sessions or new feature additions.*
