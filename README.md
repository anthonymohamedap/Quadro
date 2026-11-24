# QuadroApp update

Desktop-applicatie voor inlijstwerkplaats **Quadro**: klantbeheer, offertes, werkbonnen, planning, bestelbonnen en voorraadbeheer. Draait op Windows en macOS (Apple Silicon), met automatische updates via Velopack.

## Tech stack

| Laag | Technologie |
|---|---|
| UI | Avalonia 11.3 (Fluent, CompiledBindings) + CommunityToolkit.Mvvm |
| Data | EF Core 9 · PostgreSQL in productie (Scaleway, gedeeld door de pc's van het atelier) · SQLite lokaal en in de testen |
| PDF / Excel | QuestPDF · ClosedXML |
| Logging | Serilog (roterend bestand, 14 dagen) |
| Auth | Eigen login met PBKDF2-wachtwoorden en rollen (Admin/Medewerker) |
| Updates | Velopack (GitHub Releases, tag-triggered) |
| Tests / CI | xUnit (`WorkflowService.Tests`, ruim 560 testen) · GitHub Actions: build + test + CVE-scan op elke push en PR naar `main` en `dev` |

## Snel starten (ontwikkelaar)

```powershell
git clone https://github.com/anthonymohamedap/Quadro.git
cd Quadro
.\verify.ps1          # restore + build + alle tests
```

Openen in Rider of Visual Studio kan via `Quadro.sln`. De app maakt bij de eerste start zelf een SQLite-database aan in `%LOCALAPPDATA%\QuadroApp\` en seed een standaard admin-account — zie [docs/AUTH.md](docs/AUTH.md).

## Dagelijkse workflow

**Branches.** `main` is wat het atelier gebruikt; elke merge naar `main` kan een release worden.
`dev` bevat het werk dat nog getest en aanvaard moet worden (nu: het verkoopdossier, US-87 → US-95).

- Nieuwe functie of verkoopdossier: branch `feature/usNN-korte-naam` vanaf `dev`, PR naar `dev`.
- Dringende fix voor het atelier: branch `fix/…` vanaf `main`, PR naar `main`, en dezelfde fix daarna ook naar `dev`
  (aparte branch `fix/…-dev` vanaf `dev`).
- `dev` gaat pas naar `main` na akkoord van Anthony.

1. Branch zoals hierboven
2. Wijzigingen + tests → `.\verify.ps1` moet groen zijn
3. Schemawijziging? → `.\Scripts\add-migration.ps1 <Naam>` (nooit handmatige SQL-patches).
   Het script genereert dezelfde migratie voor beide providers (geen draaiende database nodig):
   ```powershell
   # SQLite (QuadroApp.Data/Migrations)
   dotnet ef migrations add <Naam> --project QuadroApp.Data --startup-project QuadroApp.Data
   # PostgreSQL (QuadroApp.Migrations.Npgsql/Migrations); QUADRO_PG_DESIGN_CS mag een dummy zijn
   $env:QUADRO_PG_DESIGN_CS = "Host=localhost;Database=x;Username=x;Password=x"
   dotnet ef migrations add <Naam> --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql
   ```
   Bij een pure datamigratie (model ongewijzigd) mag een herschreven `AppDbContextModelSnapshot.cs` met enkel
   herordende entiteiten teruggedraaid worden. Ongedaan maken: `dotnet ef migrations remove` met dezelfde
   `--project`/`--startup-project` in beide projecten.
4. Verandert er iets op het scherm of in een PDF? Render het met de rendertool (buiten beeld, met testgegevens)
   en vergelijk voor en na:
   ```powershell
   dotnet run --project Tools/SchermAfbeeldingen -- <uitvoermap> [scenario ...]
   ```
   De scenario's staan in `Tools/SchermAfbeeldingen/Program.cs`.
5. Push → CI draait build/test/security-scan → PR → merge

## Mappenstructuur

```
QuadroApp.Data/  EF Core: AppDbContext, migratie-runner (SqliteSchemaPatcher), audit-writer, seeder,
                 SQLite-migraties in Migrations/ (Baseline-squash van juli 2026 + vervolg)
QuadroApp.Migrations.Npgsql/  PostgreSQL-migraties + NpgsqlDesignTimeFactory (US-41)
QuadroApp.Data/Model/DB/      Entiteiten (Klant, Offerte, WerkBon, Bestelbon, Gebruiker, AuditLog, ...)
QuadroApp.Data/Model/Import/  Import-preview- en resultaatmodellen
Service/        Businesslogica: workflows, pricing, export, backup, GDPR, security (auth)
Validation/     ICrudValidator<T> per entiteit
ViewModels/     MVVM-schermlogica (per domein gegroepeerd)
Views/          Avalonia views en dialoogvensters
Styles/         QuadroTheme (huisstijl: geel #F5C242 / donkergrijs #444A50)
Scripts/        Hulpscripts: add-migration, set-db-secret, backups, Velopack-test, MigrationTool
Tools/SchermAfbeeldingen/  Rendertool: schermen en PDF's buiten beeld renderen met testgegevens
WorkflowService.Tests/  xUnit-testsuite
docs/           Documentatie (zie hieronder)
```

## Documentatie

| Document | Inhoud |
|---|---|
| [docs/CONFIGURATION.md](docs/CONFIGURATION.md) | Connection strings, secrets (DPAPI), logging-configuratie |
| [docs/AUTH.md](docs/AUTH.md) | Login, rollen & rechten, auto-lock, standaard admin |
| [docs/BACKUP_RESTORE.md](docs/BACKUP_RESTORE.md) | Automatische backups + geteste herstelprocedure |
| [docs/AUDIT.md](docs/AUDIT.md) | Audit trail: wie wijzigde wat, wanneer |
| [docs/GDPR.md](docs/GDPR.md) | Inzage-export, anonimisering, retentiebeleid |
| [docs/backlog/](docs/backlog/) | User stories en plannen (o.a. `US-87-95_Verkoopdossier_Plan.md`, `Testlijst_Verkoopdossier.md`) |
| [docs/analyse/](docs/analyse/) | Analyse van codebase en repository (november 2026) met voorstellen |
| [docs/CLOUD_ROLLOUT_RUNBOOK.md](docs/CLOUD_ROLLOUT_RUNBOOK.md) | De app op de cloud-database (Scaleway) zetten |
| [docs/specs/](docs/specs/) | Functionele specificaties |
| [docs/archief/](docs/archief/) | Historische analyses (niet meer actueel) |
| [docs/voorbeelden/](docs/voorbeelden/) | Voorbeeld-importbestanden (o.a. lijsten.xlsx) |

## Releases

Merge naar `main` → auto-tag workflow bumpt de patch-versie → tag-push triggert de release-workflow: eerst de testen, daarna Windows- en macOS-builds (incl. Apple-notarisatie), gepubliceerd via Velopack. Versie bumpen voor major/minor: `<Version>` in `QuadroApp.csproj`.

- Auto-tag heeft het secret `RELEASE_PAT` nodig om de release-workflow te starten. Werkt dat niet, zet de tag dan met de hand:
  `git tag v1.3.NN origin/main` en `git push origin v1.3.NN` (of Actions → "Release QuadroApp" → Run workflow, met de tag als ref).
- Faalt de Mac-build op de notarisatie, kijk dan of er een Apple-overeenkomst te aanvaarden is (developer.apple.com/account)
  en start daarna "Re-run all jobs". De Windows-build loopt intussen gewoon verder.

## Belangrijk om te weten

- **Nooit** wachtwoorden of connection strings in git — `appsettings.json` is gitignored (zie [docs/CONFIGURATION.md](docs/CONFIGURATION.md) voor de inhoud) en `Scripts\set-db-secret.ps1` bewaart het DB-wachtwoord versleuteld per pc.
- Schemabeheer gaat uitsluitend via EF-migraties; een test faalt automatisch bij model-drift.
- De backup van vandaag staat in `%LOCALAPPDATA%\QuadroApp\Backups`; logs in `%LOCALAPPDATA%\QuadroApp\logs`.
