# User Stories — QuadroApp Enterprise-Ready
## Versie 1.0 · Technische hardening (exclusief design/UI-wijzigingen)

Deze user stories beschrijven wat er technisch nodig is om QuadroApp van een werkende single-/dual-PC desktop-app naar een **enterprise-waardige** applicatie te brengen: betrouwbaar, veilig, onderhoudbaar en schaalbaar. Design-/UI-wijzigingen vallen **buiten** scope (die zijn elders belegd). Doorgenummerd vanaf de bestaande stories.

> Context uit de huidige codebase: schema wordt via raw SQL-patches + voor-gemarkeerde migraties beheerd, `PendingModelChangesWarning` wordt onderdrukt, het testproject is uitgesloten van de build, logging gebeurt deels via `Console.WriteLine`, en `appsettings.json` bevat een plaintext DB-wachtwoord-placeholder. Dit zijn de belangrijkste hefbomen.

---

## US-29 · Geautomatiseerde tests + CI heractiveren

**Als:** ontwikkelaar **wil ik** een werkende, geautomatiseerde testsuite met CI **zodat** wijzigingen niet stilletjes bestaande functionaliteit breken.

**Acceptatiecriteria**
- `WorkflowService.Tests` is geen uitgesloten map meer maar een echt, los testproject dat meebouwt.
- Verouderde tests (o.a. `Calculate_AfgesprokenPrijs_OverridesCalculatedRegel`) zijn bijgewerkt naar het huidige gedrag (incl→excl `/1.21`).
- Kerndomeinlogica is gedekt: `PricingEngine`, `FactuurWorkflowService` (totalen, korting, afgesproken prijs, afronding), `WerkBonWorkflowService` (planning/capaciteit), stock-mutaties.
- Een CI-pijplijn (GitHub Actions) draait `dotnet build` + `dotnet test` op elke push/PR en faalt bij rode tests.

**Technische uitwerking:** los `QuadroApp.Tests.csproj` (xUnit, EF Core InMemory/SQLite), verwijder de `<Compile Remove="WorkflowService.Tests\**" />` uitsluiting uit `QuadroApp.csproj`, voeg `.github/workflows/ci.yml` toe.
⏱ 1–2 dagen · Hoog

---

## US-30 · EF-migraties saneren (raw-patch strategie vervangen)

**Als:** ontwikkelaar **wil ik** één betrouwbaar, reproduceerbaar migratiesysteem **zodat** schema-wijzigingen veilig en consistent zijn op SQLite én PostgreSQL.

**Acceptatiecriteria**
- De `AppDbContextModelSnapshot` is weer in lijn met het model; `PendingModelChangesWarning` hoeft niet meer onderdrukt te worden.
- De ad-hoc `ALTER TABLE`-patches in `App.axaml.cs` (`ApplyPendingMigrationsAsync`) en `FactuurSchemaUpgrade.cs` zijn vervangen door echte EF-migraties (incl. de soft-delete-kolommen en korting-kolommen die nu via raw SQL worden toegevoegd).
- Migraties draaien identiek op een verse en een bestaande database, zonder de "pre-mark"-hack.
- De Designer-loze soft-delete-migraties (`20260520000001..4`) krijgen volwaardige migratie-bestanden of worden vervangen.

**Technische uitwerking:** snapshot regenereren, migratiegeschiedenis opschonen, `EnsureCreated`-pad voor Postgres heroverwegen, CI-check op "geen pending model changes".
⏱ 2–3 dagen · Hoog

---

## US-31 · Gestructureerde logging, crash- & foutrapportage

**Als:** beheerder **wil ik** centrale, gestructureerde logging en automatische crash-rapportage **zodat** problemen op afstand te diagnosticeren zijn.

**Acceptatiecriteria**
- Alle `Console.WriteLine`/`Debug.WriteLine` (o.a. in `App.axaml.cs`, `WeekWerkLijstViewModel`, `PdfFactuurExporter`) zijn vervangen door `ILogger`.
- Logs worden naar een roterend bestand én (optioneel) een centrale sink geschreven, met niveau-configuratie.
- Onafgevangen excepties worden gelogd met stacktrace en (geanonimiseerde) context; bestaande `crash.log` wordt geformaliseerd.
- Gevoelige data (DB-wachtwoord, klantgegevens) komt nooit in de logs.

**Technische uitwerking:** Serilog of `Microsoft.Extensions.Logging` met file-sink; global exception handlers op `AppDomain`/`Dispatcher`.
⏱ 1 dag · Medium

---

## US-32 · Authenticatie, gebruikersaccounts & rollen

**Als:** zaakvoerder **wil ik** dat medewerkers met een eigen account inloggen, met rechten per rol **zodat** niet iedereen alles kan wijzigen en acties herleidbaar zijn.

**Acceptatiecriteria**
- Echte authenticatie i.p.v. de huidige minimale `LoginViewModel` (wachtwoorden gehasht, bv. PBKDF2/bcrypt).
- Minstens de rollen "Eigenaar/Admin" en "Medewerker"; gevoelige acties (leverancier/lijst verwijderen, prijzen wijzigen, factureren) zijn rol-afhankelijk.
- Sessiebeheer met automatische vergrendeling na inactiviteit.

**Technische uitwerking:** `Gebruiker`-entiteit + rollen, hashing, autorisatiechecks in services/commands.
⏱ 2–3 dagen · Hoog

---

## US-33 · Configuratie- & secretsbeheer (geen plaintext wachtwoord)

**Als:** beheerder **wil ik** dat het PostgreSQL-wachtwoord en andere secrets niet als platte tekst naast de .exe staan **zodat** de installatie veilig is.

**Acceptatiecriteria**
- `appsettings.json` bevat geen plaintext DB-wachtwoord meer (nu `Password=CHANGE_ME`).
- Secrets komen uit een veilige bron (Windows DPAPI/Credential Manager, omgevingsvariabele of versleutelde config).
- Onderscheid tussen omgevingen (lokaal/SQLite vs. server/PostgreSQL) is expliciet en gedocumenteerd.

**Technische uitwerking:** connection string-opbouw met versleutelde secret store; `System.Security.Cryptography.ProtectedData` (al als dependency aanwezig) voor DPAPI.
⏱ 0,5–1 dag · Hoog

---

## US-34 · Automatische back-ups & herstelprocedure

**Als:** zaakvoerder **wil ik** dagelijkse automatische back-ups van de database met een geteste herstelprocedure **zodat** offertes, bestelbonnen en klantdata nooit verloren gaan.

**Acceptatiecriteria**
- Geplande dagelijkse back-up (SQLite-bestand kopie / PostgreSQL `pg_dump`), met retentie (bv. 30 dagen).
- Back-ups staan op een andere locatie dan de live-database.
- Een gedocumenteerde, geteste restore-procedure.

**Technische uitwerking:** geplande taak/service; voor Postgres `pg_dump`-script; voor SQLite veilige online-backup-API.
⏱ 1 dag · Hoog

---

## US-35 · Beveiligingshardening & dependency-scanning

**Als:** ontwikkelaar **wil ik** dat de applicatie en haar afhankelijkheden gecontroleerd veilig zijn **zodat** er geen bekende kwetsbaarheden of injectierisico's zijn.

**Acceptatiecriteria**
- Ongebruikte/dubbele dependencies verwijderd (o.a. `Microsoft.EntityFrameworkCore.SqlServer`, `InMemory` indien niet nodig buiten tests) → kleinere, veiligere publish.
- Raw SQL met string-interpolatie (bv. `pragma_table_info('{table}')` checks) gereviseerd; geen door-gebruiker-beïnvloedbare SQL-concatenatie.
- `dotnet list package --vulnerable` draait in CI; kwetsbare transitieve packages worden gepatcht.
- Dode/back-up-bestanden verwijderd (`*.quadro_backup`, `*.Backup.tmp`).

⏱ 1 dag · Medium

---

## US-36 · Audit trail (wie wijzigde wat, wanneer)

**Als:** zaakvoerder **wil ik** een onveranderbaar logboek van belangrijke wijzigingen **zodat** ik kan herleiden wie een offerte/prijs/bestelling aanpaste.

**Acceptatiecriteria**
- Wijzigingen aan offertes, facturen/bestelbonnen, prijzen en leveranciers worden vastgelegd (wie, wat, oud→nieuw, wanneer).
- Het auditlog is alleen-lezen voor gebruikers.
- Bestaande velden (`AangemaaktOp`/`BijgewerktOp`) worden hierin meegenomen en uitgebreid met gebruiker.

**Technische uitwerking:** `SaveChangesAsync`-interceptor in `AppDbContext` die wijzigingen naar een `AuditLog`-tabel schrijft (gekoppeld aan de ingelogde gebruiker uit US-32).
⏱ 1–2 dagen · Medium

---

## US-37 · GDPR: dataretentie, export & verwijdering

**Als:** verwerkingsverantwoordelijke **wil ik** klantgegevens conform GDPR beheren **zodat** we voldoen aan privacywetgeving.

**Acceptatiecriteria**
- Een klant kan op verzoek geëxporteerd (inzage) en definitief geanonimiseerd/verwijderd worden, met behoud van boekhoudkundig verplichte documenten.
- Retentiebeleid voor oude offertes/klanten is configureerbaar.
- De bestaande soft-delete (`IsGearchiveerd`) wordt uitgebreid met echte anonimisering waar wettelijk vereist.

⏱ 1–2 dagen · Medium

---

## US-38 · Concurrency & multi-user robuustheid (PostgreSQL)

**Als:** gebruiker op PC 2 **wil ik** veilig tegelijk werken met PC 1 **zodat** gelijktijdige bewerkingen geen data corrumperen of overschrijven.

**Acceptatiecriteria**
- Optimistic concurrency (`RowVersion`) consistent op alle bewerkbare kernentiteiten (nu o.a. op `Offerte`/`Factuur`), met duidelijke "iemand anders heeft dit gewijzigd"-melding.
- Robuuste afhandeling van verbindingsonderbrekingen (Npgsql retry is aanwezig; uitbreiden + transacties waar nodig).
- Geen race conditions in startup/stock-flows (de bekende `VoorraadAlerts`-startup-race wordt netjes opgelost i.p.v. retry).

⏱ 1–2 dagen · Hoog

---

## Overzicht & prioriteit

| Story | Onderwerp | Prioriteit | Schatting |
|---|---|---|---|
| US-33 | Secrets/configuratiebeheer | Hoog | 0,5–1 d |
| US-34 | Back-ups & herstel | Hoog | 1 d |
| US-29 | Tests + CI | Hoog | 1–2 d |
| US-30 | EF-migraties saneren | Hoog | 2–3 d |
| US-32 | Auth, accounts & rollen | Hoog | 2–3 d |
| US-38 | Concurrency/multi-user | Hoog | 1–2 d |
| US-31 | Logging & crashrapportage | Medium | 1 d |
| US-35 | Security hardening | Medium | 1 d |
| US-36 | Audit trail | Medium | 1–2 d |
| US-37 | GDPR/dataretentie | Medium | 1–2 d |

**Indicatieve totaalinspanning:** ± 12–18 werkdagen.

Aanbevolen eerste blok (laag risico, hoge waarde): US-33 → US-34 → US-29 → US-30.
