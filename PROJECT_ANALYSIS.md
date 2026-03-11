# QuadroApp — Project Analysis
*Generated: 2026-03-11 | Analyst: Claude (pre-change snapshot)*

---

## 1. Project Overview

**QuadroApp** is a Belgian desktop business application for a picture-framing / interior-finishing company. It manages the full order lifecycle: customer quotes (offertes) → work orders (werkbonnen) → invoices (facturen), plus stock tracking, planning, and supplier/product catalogue management.

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
    ├── ViewModels/              ← MVVM view models
    ├── Views/                   ← Avalonia AXAML views & windows
    ├── Converters/              ← IValueConverter implementations
    ├── Validation/              ← entity validators
    ├── Styles/                  ← custom Avalonia theme
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
        │     ├── WorkflowService      (offerte state machine)
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
| `TypeLijst` | Id, Artikelnummer, BreedteCm, PrijsPerMeter, VoorraadMeter, MinimumVoorraad | → `Leverancier`; used in `OfferteRegel`, `WerkTaak` |
| `AfwerkingsGroep` | Id, Code (G/P/D/O/R), Naam | → many `AfwerkingsOptie` |
| `AfwerkingsOptie` | Id, Naam, Volgnummer, KostprijsPerM2, WinstMarge, AfvalPercentage, WerkMinuten | → `AfwerkingsGroep`, optional `Leverancier` |
| `Offerte` | Id, KlantId, Status (enum), Datum, totalen, KortingPct, MeerPrijsIncl, VoorschotBedrag | → `Klant`, → many `OfferteRegel`, → 0..1 `WerkBon` |
| `OfferteRegel` | Id, OfferteId, AantalStuks, Breedte/HoogteCm, 6× AfwerkingsOptie FK, prijsvelden | → `TypeLijst`, 6× `AfwerkingsOptie` |
| `WerkBon` | Id, OfferteId, Status (enum), TotaalPrijsIncl, StockReservationProcessed | → `Offerte`, → many `WerkTaak` |
| `WerkTaak` | Id, WerkBonId, OfferteRegelId, GeplandVan/Tot, DuurMinuten, BenodigdeMeter, IsBesteld, IsOpVoorraad | → `WerkBon`, → `OfferteRegel` |
| `Factuur` | Id, WerkBonId, FactuurNummer, DocumentType, KlantNaam, Status (enum), totalen, Lijnen | → `WerkBon`, → many `FactuurLijn` |
| `FactuurLijn` | Id, FactuurId, Omschrijving, Aantal, PrijsExcl, BtwPct, totalen | → `Factuur` |
| `Instelling` | Sleutel (PK), Waarde | Key-value settings store |
| `ImportSession` | Id, EntityName, FileName, totals, Status | Audit log of imports → many `ImportRowLog` |
| `ImportRowLog` | Id, ImportSessionId, RowNumber, Key, Success, IssuesJson | Per-row import result |

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

### Database Initialization (App.axaml.cs)
- **⚠️ CRITICAL:** On every app startup, `EnsureDeletedAsync()` + `EnsureCreatedAsync()` is called — **this wipes the entire database on each launch**. Demo data is re-seeded via `DbSeeder.SeedDemoData()`. This is clearly marked as "demo" mode. Before any production use, this must be replaced with proper migration-based startup.
- The legacy `AppServices.Init()` (in `AppServices.cs`) still exists alongside the main DI path and also does `EnsureDeleted` — this appears to be a dead code remnant.

### DatabaseSeeder
Seeds: 4 Leveranciers (ICO, HOF, FRA, BOL), 3 demo Klanten, AfwerkingsGroepen with Opties, TypeLijsten, and sample Offertes.

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
| Import pipeline services | Transient | Parser, maps, validators, committers |
| Legacy import services | Transient | Candidates for removal |
| Validators | Scoped/Transient | Per-entity CRUD validators |
| ViewModels | Singleton (Main) / Transient | |

### WorkflowService (the state machine)
Implements strict `OfferteStatus` transitions via a lookup dictionary. Key behaviors:
- **Goedgekeurd transition:** Automatically creates a `WerkBon` if none exists (idempotent)
- **Stock reservation (`ReserveStockForWerkBonAsync`):** Validates `BenodigdeMeter > 0`, checks available stock per `WerkTaak`, decrements `VoorraadMeter`, sets `IsOpVoorraad`, raises warnings if below `MinimumVoorraad`; idempotent via `StockReservationProcessed` flag
- **`MarkLijstAsBesteldAsync`:** Sets `IsBesteld + BestelDatum` on a WerkTaak
- **Offerte→WerkBon→Factuur lifecycle** is managed across three specialized workflow services

### PricingEngine (Service/Pricing/)
Pure calculation class (no DB dependency). For each `OfferteRegel` it computes:
1. **Lijst price:** perimeter × PrijsPerMeter + waste% + labour (min/hr × uurloon) + VasteKost
2. **Afwerking price (×6 slots):** m² × KostprijsPerM2 + VasteKost + waste% + labour
3. **Extra costs/discounts** per regel
4. **Offerte-level:** discount (KortingPct), MeerPrijsIncl, BTW calculation, VoorschotBedrag clamping
5. Distinguishes Staaflijst vs. non-staaflijst for different waste/profit factor settings

### Import Pipeline (Service/Import/Enterprise/)
Generic, interface-driven pipeline:
- `IExcelParser` (`ClosedXmlExcelParser`) → reads rows as `Dictionary<string, string?>`
- `IExcelMap<T>` → defines columns, parsers, key extraction, `ApplyCell`
- `IImportValidator<T>` → async per-row validation against DB
- `IImportCommitter<T>` → upsert rows into DB with tracking
- `ImportService` orchestrates: **DryRun** (validation only) then **Commit** (transactional, with audit logging to `ImportSession`/`ImportRowLog`)
- Implemented for: `Klant`, `TypeLijst`, `AfwerkingsOptie`

**Legacy import services** (`ExcelImportService`, `KlantExcelImportService`, `AfwerkingsOptieExcelImportService`) still registered — candidates for cleanup once runtime verification is complete.

### Other Services
- **NavigationService:** Service-locator navigation using `IServiceProvider`; supports typed navigation with parameter passing via `IParameterReceiver<TParam>` and `IAsyncInitializable`
- **ToastService:** Shows timed in-app toast notifications (Success/Error/Warning/Info)
- **DialogService / KlantDialogService / LijstDialogService:** Modal dialog helpers
- **PdfFactuurExporter:** Uses QuestPDF (community license) to export invoices/orders as PDF
- **AppSettingsProvider / PricingSettingsProvider:** Read `Instelling` key-value pairs from DB

---

## 7. Presentation Layer (Views & ViewModels)

### Screen Inventory

| ViewModel | View/Window | Purpose |
|---|---|---|
| `MainWindowViewModel` | `MainWindow` | Shell, hosts navigation area |
| `HomeViewModel` | `HomeView` | Dashboard / landing page |
| `LoginViewModel` | `LoginWindow` | User authentication |
| `KlantenViewModel` | `KlantenView` | Customer list & management |
| `KlantDetailViewModel` | `KlantDetailView` | Single customer detail |
| `LeveranciersViewModel` | `LeveranciersView` | Supplier list |
| `LijstenViewModel` | `LijstenView` | TypeLijst (product/frame list) management |
| `AfwerkingenViewModel` | `AfwerkingenView` | Finishing options management |
| `OffertesLijstViewModel` | `OffertesLijstView` | Quote list |
| `OfferteViewModel` | `OfferteView` | Quote detail editor |
| `WerkBonLijstViewModel` | `WerkBonLijstView` | Work order list |
| `FacturenViewModel` | `FacturenView` | Invoice management |
| `PlanningCalendarViewModel` | `PlanningCalendarWindow` | Calendar/planning view |
| `WeekWerkLijstViewModel` | `WeekWerkLijstWindow` | Weekly work schedule |
| `BulkLijstenViewModel` | `BulkLijstenWindow` | Bulk list operations |
| `InstellingenViewModel` | `InstellingenWindow` | App settings |
| `ImportPreviewViewModel` | `ImportPreviewView/Window` | Generic import preview |
| `KlantExcelPreviewViewModel` | `KlantImportPreviewWindow` | Klant import preview |
| `AfwerkingExcelPreviewViewModel` | `AfwerkingImportPreviewWindow` | Afwerking import preview |

### Key MVVM Patterns
- `CommunityToolkit.Mvvm` (source-generated `[ObservableProperty]`, `[RelayCommand]`)
- Navigation is driven by `INavigationService`; views are data-template mapped to ViewModels
- `IAsyncInitializable` — ViewModels implement `InitializeAsync()` called after navigation
- `IParameterReceiver<T>` — ViewModels receive typed parameters (e.g., klantId)
- `Huskui.Avalonia` used for additional UI controls

---

## 8. Validation Layer (Validation/)

Each entity has an `ICrudValidator<T>` implementation:

| Validator | Entity | Key rules |
|---|---|---|
| `KlantValidator` | `Klant` | Required: Voornaam/Achternaam; email format if provided |
| `TypeLijstValidator` | `TypeLijst` | Required: Artikelnummer, Levcode, LeverancierId, BreedteCm > 0 |
| `AfwerkingsOptieValidator` | `AfwerkingsOptie` | Required: Naam, AfwerkingsGroepId |
| `OfferteValidator` | `Offerte` | Custom `IOfferteValidator` interface |

---

## 9. Testing (WorkflowService.Tests/)

xUnit test project targeting `net9.0`. Uses **in-memory EF Core** (`UseInMemoryDatabase`) for fast, isolated tests.

### Test Coverage

| Test Class | Tests | What's covered |
|---|---|---|
| `WorkflowServiceTests` | 9 tests | Status transitions (valid/invalid), WerkBon creation idempotency, stock reservation (success/insufficient/minimum warning), besteld marking, duplicate reservation prevention, validation exceptions |
| `PricingEngineTests` | 2 tests | Staaflijst vs. non-staaflijst pricing paths |
| `ImportServiceTests` | (present) | Import pipeline testing |
| `TypeLijstImportCommitterTests` | (present) | Committer upsert logic |
| `PricingSettingsProviderTests` | (present) | Settings reading |
| `MigrationSafetyTests` | (present) | Schema compatibility checks |

---

## 10. NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| Avalonia (+ Controls.DataGrid, Desktop, Themes.Fluent, Fonts.Inter) | 11.3.12 | UI framework |
| Avalonia.Diagnostics | 11.3.12 | Debug inspector (Debug only) |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source generators |
| Microsoft.EntityFrameworkCore + SQLite + SqlServer + InMemory | 9.0.9 | ORM + providers |
| Microsoft.Extensions.DependencyInjection | 9.0.9 | DI container |
| Microsoft.Extensions.Logging | 9.0.0 | Logging |
| ClosedXML | 0.105.0 | Excel reading (import pipeline) |
| EPPlus | 8.4.1 | Excel (legacy import path) |
| QuestPDF | 2024.7.1 | PDF invoice export |
| Huskui.Avalonia | 0.10.3 | Extra Avalonia controls |
| EfCore.SchemaCompare | 8.2.0 | Migration safety tests |
| xunit (assert, core, abstractions) | 2.9.3 | Testing |

---

## 11. Key Observations & Risks

### ⚠️ Critical Issues

1. **Database wiped on every startup** — `EnsureDeletedAsync` + `EnsureCreatedAsync` runs in `OnFrameworkInitializationCompleted`. All data is lost and re-seeded from scratch on every run. This must be changed before any production use. The proper migrations (`Migrations/` folder) are present but currently unused at runtime.

2. **Dead code / dual DI path** — `AppServices.cs` (`AppServices.Init()`) contains a second, parallel DI registration and also calls `EnsureDeleted`. It is never called from `App.axaml.cs` and appears to be an obsolete remnant.

### ⚠️ Moderate Issues

3. **Legacy import services still registered** — `ExcelImportService`, `KlantExcelImportService`, `AfwerkingsOptieExcelImportService` are still DI-registered alongside the new Enterprise pipeline. These should be reviewed and removed when the new pipeline is fully verified.

4. **Two Excel libraries** — Both `ClosedXML` (Enterprise pipeline) and `EPPlus` (legacy path) are present. Once legacy is removed, EPPlus can be dropped.

5. **`Klant` has no explicit MaxLength on most string fields** — unlike other entities. This may cause issues if switching to SQL Server (SQLite is lenient).

6. **`OfferteRegel` stores computed price fields** (`SubtotaalExBtw`, `BtwBedrag`, `TotaalInclBtw`) redundantly — these are re-derived by `PricingEngine`. Risk of stale data if not kept in sync.

7. **`ToastColorConverter` is defined inside `App` class** — it is also registered separately in `Converters/`. This duplication should be resolved.

### ℹ️ Minor / Cosmetic

8. **`Levcode` field on `TypeLijst`** — max 50 chars but `Leverancier.Naam` is max 3 chars. The relationship + meaning may need clarification.

9. **`WerkTaak` uses local `DateTime`** — comment in code notes future migration to `DateTimeOffset` if timezone support is needed.

10. **`AppServices.cs` global static** — uses a static `IServiceProvider` pattern (`AppServices.Db`) which can cause issues in tests or parallel scenarios. The main DI path in `App.axaml.cs` correctly uses constructor injection.

---

## 12. File Count Summary

| Area | Files |
|---|---|
| Views (AXAML + code-behind) | ~40 files |
| ViewModels | 19 files |
| Services | ~35 files |
| Model/DB entities | 14 files |
| Model/Import models | 10 files |
| Converters | 5 files |
| Validators | 5 files |
| Data layer | 4 files |
| Tests | 6 files |
| **Total source files** | **~138** |

---

*This document was generated as a pre-change snapshot. Update after significant refactoring sessions.*
