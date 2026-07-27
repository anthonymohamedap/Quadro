# User Stories — Vervolgfuncties (na de release)

Losse functionele stories die niet in de 2-PC-release hoeven, maar wel op de backlog staan.
Elke story bevat acceptatiecriteria, een technische uitwerking en een kant-en-klare prompt.

---

## US-40 · Audit-leesscherm in de app (admin-only)

**Als** zaakvoerder/beheerder **wil ik** het audit-logboek rechtstreeks in de app kunnen inzien en
filteren **zodat** ik kan herleiden wie een offerte, prijs, factuur of leverancier heeft aangepast,
zonder een externe database-tool te gebruiken.

**Achtergrond:** de audit trail (US-36) wordt weggeschreven naar de tabel `AuditLogs` (kolommen
`Tijdstip` [UTC], `Gebruiker`, `EntiteitType`, `EntiteitId`, `Actie`, `Wijzigingen` [JSON oud→nieuw]).
Er is nu nog géén leesscherm; raadplegen kan alleen via bv. DB Browser for SQLite. Zie `docs/AUDIT.md`.

### Acceptatiecriteria
- Nieuw scherm/venster, alleen bereikbaar voor Admin (nieuwe of bestaande permissie — zie hieronder).
- Toont de audit-records in een tabel, nieuwste eerst, met kolommen: tijdstip (in **lokale** tijd via
  `UtcToLocalConverter`), gebruiker, entiteit-type, entiteit-id, actie.
- De `Wijzigingen`-JSON wordt leesbaar getoond (bv. per veld "oud → nieuw"), niet als ruwe JSON.
- Filterbaar op: gebruiker, entiteit-type, actie en datumbereik.
- Paginering of virtualisatie zodat het scherm ook met tienduizenden records vlot blijft.
- Alleen-lezen: geen bewerk-/verwijderknoppen (het auditlog is onveranderbaar).
- Bereikbaar via Instellingen → (nieuwe knop) "Auditlogboek", naast Gebruikersbeheer.

### Technische uitwerking
- **Permissie:** hergebruik `Permissie.GebruikersBeheren` óf voeg `Permissie.AuditInzien` toe
  (Admin-only in `RolPermissies`). Aanbeveling: aparte `AuditInzien` zodat het losstaat van
  accountbeheer; werk dan ook het rechten-overzicht in Gebruikersbeheer bij (dat leest `RolPermissies`).
- **Service:** `IAuditService.ZoekAsync(filter, skip, take)` met `VereisPermissie(...)`, die
  `AppDbContext.AuditLogs` bevraagt met `AsNoTracking()`, gefilterd + gepagineerd, `OrderByDescending(Tijdstip)`.
- **ViewModel:** `AuditLogViewModel` met filtervelden (gebruiker/type/actie/van-tot), een
  `ObservableCollection<AuditRegelWeergave>` en volgende/vorige-pagina. Parse de `Wijzigingen`-JSON
  naar een leesbare lijst "Veld: oud → nieuw".
- **View:** `AuditLogWindow.axaml` in de huisstijl (QuadroHeader, `Card`, tokens, `TextTrimming` +
  tooltips, DataGrid met `*`-kolommen + `MinWidth`, `ScrollViewer`). Volgt alle enterprise-UI-regels.
- **Weergave tijd:** `Tijdstip` staat in UTC → toon via `UtcToLocalConverter` (bestaat sinds REL-01).
- **Tests:** service-test op filtering/paginering/permissie-guard; een parser-test die JSON → leesbare
  regels omzet (incl. lege/verwijderd-record `{}`).

⏱ Schatting: 1–1,5 dag · medium.

**PROMPT:**
```
Voer US-40 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Bouw een admin-only audit-
leesscherm: IAuditService.ZoekAsync (AsNoTracking, gefilterd + gepagineerd, VereisPermissie),
AuditLogViewModel met filters (gebruiker/type/actie/datumbereik) + paginering en JSON→leesbaar
"veld: oud → nieuw", en AuditLogWindow in de huisstijl (QuadroHeader, tokens, trimming/tooltips,
DataGrid met star-kolommen, ScrollViewer), bereikbaar via Instellingen. Toon Tijdstip lokaal via
UtcToLocalConverter. Voeg Permissie.AuditInzien toe (Admin-only) en werk het rechten-overzicht bij.
Tests: service (filter/paginering/permissie) + JSON-parser. Branch feature/us40-audit-leesscherm,
.\verify.ps1 groen.
```

---

## US-41 · Volledige EF-migraties voor PostgreSQL (Optie C)

**Als** ontwikkelaar **wil ik** dat PostgreSQL hetzelfde EF-migratiesysteem gebruikt als SQLite
**zodat** schemawijzigingen beheerd en driftvrij zijn, zonder `EnsureCreatedAsync`.

**Achtergrond:** in de 2-PC-release (REL-02, Optie A) draait PostgreSQL op `EnsureCreatedAsync` zonder
migratiehistorie; optimistic concurrency werkt via `xmin` (zie `AppDbContext.OnModelCreating` en
`docs/archief/REL02_PG_SCHEMA_ANALYSE.md`). Dat deblokkeert de release maar laat drift-risico op
toekomstige schemawijzigingen bestaan.

### Acceptatiecriteria
- PostgreSQL initialiseert via `MigrateAsync` met een provider-passende migratieset.
- `EnsureCreatedAsync` is uitgefaseerd voor PostgreSQL.
- Verse PG- en verse SQLite-database leveren een gelijkwaardig eindschema op.
- Een test/CI-check bewaakt "geen pending model changes" op beide providers.
- De xmin-concurrency (REL-02) blijft werken; byte[] RowVersion blijft SQLite-only.

### Technische uitwerking
- Tweede design-time configuratie / migratie-assembly voor Npgsql (de huidige migraties zijn
  SQLite-smaak met `Sqlite:Autoincrement`-annotaties). Overweeg gescheiden migratiemappen per provider.
- Vereist een draaiende PostgreSQL om volledig te valideren → plan dit met de PG-server erbij.

⏱ Schatting: L. **Post-release.**

**PROMPT:**
```
Voer US-41 uit volgens docs/backlog/UserStories_VervolgFuncties.md en de analyse in
docs/archief/REL02_PG_SCHEMA_ANALYSE.md. Begin met analyse: hoe zetten we PostgreSQL op echte
EF-migraties (provider-specifieke migratie-assembly, MigrateAsync, EnsureCreated uitfaseren) met
behoud van de xmin-concurrency? Lever een voorstel en wacht op akkoord. Vereist een draaiende
PostgreSQL voor validatie. Branch feature/us41-pg-ef-migrations.
```

---

## US-42 · Statussynchronisatie offerte ↔ werkbon ↔ bestelbon

**Als** gebruiker **wil ik** dat de status van een offerte, haar werkbon en de bijhorende bestelbon
altijd samenhangen en overal gelijk bijwerken **zodat** ik op elk scherm hetzelfde, kloppende beeld
van een job zie.

**Achtergrond (geanalyseerd 26 juli 2026, `Service/WorkflowService.cs`):** er zijn drie statusmachines
die nu maar **half en één-richting** gekoppeld zijn.

Werkt wel (offerte → werkbon):
- Offerte → *Goedgekeurd*: maakt WerkBon (Gepland) + reserveert voorraad.
- Offerte → *Geannuleerd*: archiveert werkbon + geeft voorraad vrij.
- WerkBon → *Afgewerkt*: verbruikt voorraad + maakt de bestelbon (factuur) aan.

Ontbreekt (terugkoppeling naar de offerte):
1. WerkBon → *InUitvoering* zet de offerte niet op *InProductie*.
2. WerkBon → *Afgewerkt* zet de offerte niet op *Afgewerkt*.
3. Bestelbon → *Betaald* zet de offerte niet op *Gefactureerd*/*Betaald*.

Gevolg: een offerte kan "Goedgekeurd" tonen terwijl de werkbon "Afgewerkt" is en er een betaalde
bestelbon bestaat. Elk scherm leest zijn eigen status (`Offerte.Status`, `WerkBon.Status`,
`Factuur.Status`) → drie beelden van dezelfde job. De offerte-statussen *InProductie / Afgewerkt /
Gefactureerd / Betaald* worden daardoor bijna nooit bereikt (deels dode enum-waarden).

### Acceptatiecriteria
- Werkbon- en bestelbon-transities **propageren** naar de offertestatus volgens een vastgelegde mapping:
  - WerkBon *InUitvoering* → Offerte *InProductie*
  - WerkBon *Afgewerkt* → Offerte *Afgewerkt*
  - Bestelbon aangemaakt → Offerte *Gefactureerd* (of bij *Afgewerkt* → *Gefactureerd* zodra de bestelbon bestaat)
  - Bestelbon *Betaald* → Offerte *Betaald*
- De propagatie gebeurt in één transactie met de bron-transitie (consistent, geen half-bijgewerkte staat).
- Bestaande transitievalidatie (`OfferteTransitions`) wordt uitgebreid zodat deze afgeleide overgangen
  geldig zijn; ongeldige combinaties blijven geblokkeerd.
- Alle lijsten (offertes, werkbonnen, facturen) tonen na een statuswijziging hetzelfde, actuele beeld
  (reload/refresh of gedeelde bron).
- Multi-user veilig: propagatie respecteert de optimistic-concurrency (RowVersion/xmin, REL-02).

### Technische uitwerking
- **Bron van waarheid:** de werkbon/bestelbon sturen de offertestatus (de offerte "volgt" de productie).
  Overweeg een afgeleide/berekende status als alternatief, maar expliciet propageren is eenvoudiger te
  auditen (US-36 logt de wijziging) en te tonen.
- Centraliseer de mapping in `WorkflowService` (naast `OfferteTransitions`/`WerkBonTransitions`):
  breid `ChangeWerkBonStatusAsync` en de factuur-transities (`FactuurWorkflowService`:
  KlaarVoorExport/Betaald + factuur-aanmaak) uit met een `PropageerNaarOfferteAsync(...)`.
- Let op de bestaande neveneffecten (voorraad verbruiken, bestelbon aanmaken) — die blijven, de
  offertestatus komt er als extra, transactioneel, bovenop.
- **Tests:** volledige keten — offerte goedkeuren → werkbon InUitvoering/Afgewerkt → bestelbon betaald,
  en verifieer dat de offertestatus elke stap correct meebeweegt; plus ongeldige overgangen blijven
  geblokkeerd.

> **Belangrijk:** dit is een functionele kernwijziging (raakt voorraad + facturatie) — **niet** in de
> release-branch. Post-release, met de volledige testketen groen.

⏱ Schatting: M–L. **Post-release.**

**PROMPT:**
```
Voer US-42 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Begin met een ontwerpanalyse:
bevestig de statusmapping (werkbon/bestelbon → offerte) en de uitgebreide transitievalidatie, en
wacht op mijn akkoord. Implementeer daarna in WorkflowService + FactuurWorkflowService de
transactionele propagatie naar Offerte.Status, met tests over de volledige keten (goedkeuren →
InUitvoering → Afgewerkt → bestelbon betaald) en behoud van de bestaande voorraad-/factuur-
neveneffecten en de optimistic concurrency. Branch feature/us42-statussync. .\verify.ps1 groen.
```

## US-43 · Retry-on-failure terugbrengen via execution strategy (PostgreSQL)

**Als** ontwikkelaar **wil ik** dat PostgreSQL weer automatisch herstelt van een korte netwerk-blip
(retry-on-failure) **zonder** dat de transacties in de app breken **zodat** de 2-PC-opstelling
veerkrachtiger is bij Wi-Fi-haperingen.

**Achtergrond (gevonden in de dry-run, 26 juli 2026):** `EnableRetryOnFailure`
(`NpgsqlRetryingExecutionStrategy`) botst met door de gebruiker geopende transacties
(`db.Database.BeginTransactionAsync`). De app doet dat op **13 plekken**: `ImportService`,
`StockService` (7×), `OfferteArchiefService` (2×), `WerkBonArchiefService`, `WorkflowService` en de
import-commit. Met retry aan faalt elk van die met *"The configured execution strategy
'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions."* Voor de release is
`EnableRetryOnFailure` daarom **weggehaald** (zie `App.axaml.cs`) — simpel, lost alle 13 plekken in
één keer op, laagste risico. Op een bekabeld/LAN-Postgres is transient-retry weinig waard.

### Acceptatiecriteria
- Retry-on-failure staat weer aan voor PostgreSQL.
- Alle 13 transactie-sites werken door ze in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`
  te wikkelen (de héle transactie als één herhaalbare eenheid).
- Elke ingepakte transactie is **idempotent/herstelbaar**: bij een retry mag er geen dubbele import,
  dubbele voorraadmutatie of dubbel factuurnummer ontstaan.
- Tests per pad die een transient fout simuleren en bevestigen dat de operatie precies één keer landt.
- SQLite blijft ongewijzigd (geen retry-strategie daar).

### Technische uitwerking
- Helper op `AppDbContext` of een extension: `ExecuteInTransactionAsync(Func<...> work)` die
  `CreateExecutionStrategy().ExecuteAsync` combineert met `BeginTransactionAsync` + commit/rollback.
- De 13 sites één voor één omzetten en testen; let op state die vóór de transactie wordt opgebouwd
  (moet binnen de retriable lambda opnieuw geldig zijn).
- Vereist een draaiende PostgreSQL om de retry echt te valideren.

⏱ Schatting: M. **Post-release.**

**PROMPT:**
```
Voer US-43 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Zet EnableRetryOnFailure weer aan
voor PostgreSQL in App.axaml.cs en wikkel alle 13 BeginTransactionAsync-sites (ImportService,
StockService, OfferteArchiefService, WerkBonArchiefService, WorkflowService) in
db.Database.CreateExecutionStrategy().ExecuteAsync(...), met een herbruikbare helper. Zorg dat elke
transactie idempotent is bij een retry (geen dubbele import/voorraad/factuurnummer) en voeg tests toe
die een transient fout simuleren. Branch feature/us43-retry-execution-strategy. .\verify.ps1 groen.
```

---

## US-45 · Facturen/bestelbonnen als export-dataset in het Exportcenter

**Als** zaakvoerder **wil ik** mijn facturen/bestelbonnen ook als Excel-overzicht kunnen exporteren
vanuit het Exportcenter **zodat** ik een volledig financieel overzicht heb zonder elke bestelbon apart
als PDF te openen.

**Achtergrond (cross-check 27 juli 2026):** het Exportcenter (`CentralExcelExportService`) heeft
datasets voor Klanten, Leveranciers, Lijsten, Offertes en Afwerkingen — maar **niet voor Facturen**.
Facturen kunnen nu enkel per stuk als PDF via `FactuurExportService`. Er is geen Excel-dataset met alle
bestelbonnen (met bedragen, status, klant, datum) en hun regels.

### Acceptatiecriteria
- Nieuwe dataset "Facturen" (of "Bestelbonnen") in `GetDatasetDefinitions()`, met kolommen o.a.:
  FactuurNummer, DocumentType, KlantNaam, FactuurDatum, VervalDatum, Status, TotaalExclBtw, TotaalBtw,
  TotaalInclBtw, VoorschotBedrag, KortingPct, IsBtwVrijgesteld, AfhaalDatum.
- Relatie "Factuurregels" (`FactuurLijn`) als extra werkblad, net als bij offertes.
- Optioneel: één of twee presets (bv. "Boekhouding" met de financiële kernkolommen).
- Volgt exact het bestaande patroon (typed `Col(...)`-lambda's, `DatasetDefinitie`, `RelatieDefinitie`);
  geen wijziging aan de exportmotor zelf.
- Tests analoog aan de bestaande export-tests (dataset verschijnt, kolommen kloppen, export draait).

### Technische uitwerking
- Nieuw partial-bestand `Service/Export/CentralExcelExportService.Facturen.cs` met
  `BuildFacturenDefinition()`, geregistreerd in `GetDatasetDefinitions()`.
- Bron: `db.Facturen.Include(x => x.Lijnen)` (+ eventueel WerkBon/Offerte voor koppeling).
- Enum `ExcelExportDataset` uitbreiden met `Facturen`.

⏱ Schatting: S–M. **Post-release.**

**PROMPT:**
```
Voer US-45 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Voeg een Facturen-dataset toe aan
CentralExcelExportService (nieuw partial BuildFacturenDefinition, ExcelExportDataset.Facturen,
db.Facturen.Include(Lijnen)) met financiële kernkolommen + een Factuurregels-relatie, volgens het
bestaande typed Col(...)-patroon. Geen wijziging aan de exportmotor. Tests analoog aan de bestaande
export-tests. Branch feature/us45-facturen-export. .\verify.ps1 groen.
```

---

### Status
| Story | Onderwerp | Prioriteit | Status |
|---|---|---|---|
| US-40 | Audit-leesscherm in de app | Medium (na release) | 🟡 branch `feature/us40-audit-leesscherm` — verify + merge nog te doen |
| US-41 | Volledige EF-migraties voor PostgreSQL | Medium (na release) | ⬜ |
| US-42 | Statussync offerte ↔ werkbon ↔ bestelbon | Hoog (na release) | ⬜ |
| US-43 | Retry-on-failure via execution strategy | Medium (na release) | ⬜ |
| US-44 | Export Center → enterprise wizard (View-only) | Medium (na release) | 🟡 branch `feature/us44-export-wizard` (bevat ook export-kolomfixes) — verify + merge nog te doen |
| US-45 | Facturen als export-dataset | Medium (na release) | ⬜ |
