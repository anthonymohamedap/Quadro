# QuadroApp

Desktop-applicatie voor inlijstwerkplaats **Quadro**: klantbeheer, offertes, werkbonnen, planning, facturatie/bestelbonnen en voorraadbeheer. Draait op Windows en macOS (Apple Silicon), met automatische updates via Velopack.

## Tech stack

| Laag | Technologie |
|---|---|
| UI | Avalonia 11.3 (Fluent, CompiledBindings) + CommunityToolkit.Mvvm |
| Data | EF Core 9 + SQLite (PostgreSQL-ondersteuning aanwezig voor gedeeld gebruik) |
| PDF / Excel | QuestPDF · ClosedXML + EPPlus |
| Logging | Serilog (roterend bestand, 14 dagen) |
| Auth | Eigen login met PBKDF2-wachtwoorden en rollen (Admin/Medewerker) |
| Updates | Velopack (GitHub Releases, tag-triggered) |
| Tests / CI | xUnit (119 tests) · GitHub Actions: build + test + CVE-scan op elke push/PR |

## Snel starten (ontwikkelaar)

```powershell
git clone https://github.com/anthonymohamedap/Quadro.git
cd Quadro
.\verify.ps1          # restore + build + alle tests
```

Openen in Rider of Visual Studio kan via `Quadro.sln`. De app maakt bij de eerste start zelf een SQLite-database aan in `%LOCALAPPDATA%\QuadroApp\` en seed een standaard admin-account — zie [docs/AUTH.md](docs/AUTH.md).

## Dagelijkse workflow

1. Feature-branch vanaf `main`
2. Wijzigingen + tests → `.\verify.ps1` moet groen zijn
3. Schemawijziging? → `.\Scripts\add-migration.ps1 <Naam>` (nooit handmatige SQL-patches)
4. Push → CI draait build/test/security-scan → PR → merge

## Mappenstructuur

```
Data/           EF Core: AppDbContext, migratie-runner (SqliteSchemaPatcher), audit-writer, seeder
Migrations/     EF-migraties (Baseline-squash van juli 2026 + vervolg)
Model/DB/       Entiteiten (Klant, Offerte, WerkBon, Factuur, Gebruiker, AuditLog, ...)
Model/Import/   Import-preview- en resultaatmodellen
Service/        Businesslogica: workflows, pricing, export, backup, GDPR, security (auth)
Validation/     ICrudValidator<T> per entiteit
ViewModels/     MVVM-schermlogica (per domein gegroepeerd)
Views/          Avalonia views en dialoogvensters
Styles/         QuadroTheme (huisstijl: geel #F5C242 / donkergrijs #444A50)
Scripts/        Hulpscripts: add-migration, set-db-secret, backups, Velopack-test
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
| [docs/backlog/](docs/backlog/) | User stories + status (enterprise-hardening: 9/10 afgerond) |
| [docs/specs/](docs/specs/) | Functionele specificaties |
| [docs/archief/](docs/archief/) | Historische analyses (niet meer actueel) |
| [docs/voorbeelden/](docs/voorbeelden/) | Voorbeeld-importbestanden (o.a. lijsten.xlsx) |

## Releases

Merge naar `main` → auto-tag workflow bumpt de patch-versie → tag-push triggert de release-workflow die Windows- en macOS-builds maakt (incl. Apple-notarisatie) en publiceert via Velopack. Versie bumpen voor major/minor: `<Version>` in `QuadroApp.csproj`.

## Belangrijk om te weten

- **Nooit** wachtwoorden of connection strings in git — `appsettings.json` is gitignored, gebruik `appsettings.example.json` als sjabloon en `Scripts\set-db-secret.ps1` voor het DB-wachtwoord.
- Schemabeheer gaat uitsluitend via EF-migraties; een test faalt automatisch bij model-drift.
- De backup van vandaag staat in `%LOCALAPPDATA%\QuadroApp\Backups`; logs in `%LOCALAPPDATA%\QuadroApp\logs`.
