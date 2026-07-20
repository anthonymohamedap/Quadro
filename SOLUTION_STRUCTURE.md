# Solution Structure Analysis

This repository contains a single desktop application solution built with **.NET 9**, **Avalonia UI**, and **Entity Framework Core**.

## 1) Solution and project layout

- `Quadro.sln`: single-project solution containing `QuadroApp.csproj`.
- `QuadroApp.csproj`: executable Avalonia app targeting `net9.0` with EF Core, MVVM Toolkit, Excel/import dependencies, and Avalonia packages.

## 2) Top-level architectural areas

### Application bootstrap
- `Program.cs`: app entry point and Avalonia host builder.
- `App.axaml` / `App.axaml.cs`: application resources and startup wiring.
- `MainWindow.axaml` / `MainWindow.axaml.cs`: shell window.

### Data access and persistence
- `Data/AppDbContext.cs.cs`: EF Core DbContext model configuration, precision/index/relationship rules, and save hooks.
- `Data/AppDbContextFactory.cs`: design-time context creation support.
- `Data/DatabaseSeeder.cs`: data seeding utilities.
- `Data/Migrations/*`: EF migrations and model snapshot.
- `AppDbContext.sql`, `AppDbContext.dgml`: schema/support artifacts.

### Domain model
- `Model/DB/*`: core entities (e.g., `Offerte`, `OfferteRegel`, `WerkBon`, `WerkTaak`, `Klant`, `TypeLijst`, etc.).
- `Model/Import/*`: import-preview and import-result models.

### Presentation layer (MVVM)
- `Views/*`: Avalonia views/windows (`.axaml` + code-behind).
- `ViewModels/*`: screen/workflow logic for login, offers, planning, clients, lists, import previews, etc.
- `Converters/*`: UI value converters.
- `Styles/QuadroTheme.axaml`: custom theme resources.

### Services and business workflows
- `Service/Interfaces/*`: service contracts.
- `Service/*`: concrete implementations for dialogs, navigation, pricing, workflows, imports, and app DI wiring.
- `Service/Import/*`: excel import pipeline abstractions and mappers.
- `Service/Toast/*`: toast message models/service/converters.
- `Security/AppSecurity.cs`: application security support.

### Validation
- `Validation/*`: validators and validation result models for core entities.

### Assets and deployment
- `Assets/*`: logos, images, print assets.
- `app.manifest`: Windows application manifest.

## 3) Dependency flow (high level)

- `Views` bind to `ViewModels`.
- `ViewModels` orchestrate `Service` interfaces.
- `Service` layer uses `Data/AppDbContext` and domain `Model` classes.
- `Validation` supports service/viewmodel-level input checks.
- `Converters` and `Styles` support view rendering concerns.

## 4) Notable technical characteristics

- Single executable desktop app (not multi-project yet).
- EF Core configured for SQLite at runtime (with migrations present).
- Rich workflow around offers/work orders/planning/imports.
- Clear folder-based layering despite single-project packaging.

## 5) Potential maintenance observations

- `Data/AppDbContext.cs.cs` has a duplicated extension in its filename; functionally valid but easy to misread.
- A few filenames include stray spaces before `.cs` (e.g., `RegelPlanItem .cs`, `BoolToDoubleConverter .cs`), which can impact tooling ergonomics.
- Both runtime initialization/seeding and migrations exist; worth keeping startup DB lifecycle strategy explicit in future changes.
