# QuadroApp — Releaseplan 2-PC (PostgreSQL multi-user)

> **Doel van dit document.** Eén centraal plan dat alle openstaande stappen naar een veilige
> 2-PC-release (PC 1 + PC 2, gedeelde PostgreSQL-database) bundelt. Samengesteld uit:
> de codebase-audit (`docs/archief/CODEBASE_AUDIT_2026-07-22.md`), de enterprise-backlog
> (`UserStories_EnterpriseReady.md`), de klantfeedback van Veerle & Kurt
> (`UserStories_VeerleKurt_v2.md`), het UI-plan (`UI_OPTIMIZATION_PLAN.md`) en
> `docs/cleanup-candidates.md`.
>
> **Hoe te gebruiken.** Elk blok hieronder is één werksessie in een aparte chat. Bij elke stap staat
> een **kant-en-klare prompt** die je kan kopiëren. Werkwijze overal identiek: aparte feature-branch,
> `.\verify.ps1` groen, PR, merge. Werk na elke merge de statustabel onderaan bij.
>
> *Laatst bijgewerkt: 26 juli 2026.*

---

## 0. Waar staan we nu?

**Al afgerond en gemerged** (enterprise-hardening + eerste audit-acties):

- US-29 t/m US-37: tests+CI, EF-migratie-squash, auth met rollen + gebruikersbeheer, secrets (DPAPI),
  dagelijkse backups, structured logging (Serilog), audit trail, GDPR, security hardening.
- UI-optimalisatie fases 0–4 + planning-redesign (huisstijl overal, geen tekstoverflow/overlap).
- Auth-UI: wachtwoord wijzigen + gebruikersbeheer + rechten-overzicht; prijzen-guard op álle schrijfpaden.
- **Audit-acties reeds gedaan:** P1 (US-38 concurrency: `RowVersion` op `TypeLijst` + unieke
  factuurnummer-constraint), P2 (demo-data achter `QUADRO_SEED_DEMO`/DEBUG), P3 (autorisatie-gaten:
  archief-delete, afwerking-delete, factureren, klant-archiveren), P4 (StockService-testsuite).

**Nog te doen vóór de 2-PC-release** — dit document. Drie categorieën:

1. **Klantwensen (Veerle & Kurt)** — US-26/27/28, functionele wijzigingen die zij verwachten.
2. **PostgreSQL-hardening** — P5 (UTC), P6 (schemastrategie) uit de audit; verplicht bij multi-user.
3. **PostgreSQL-uitrol + release** — server opzetten, data migreren, tag/Velopack, macOS-check.

**Optioneel (blokkeert niets)** — P7–P10 hygiëne. Onderaan opgenomen, mag na de release.

---

## Categorie 1 — Klantwensen (Veerle & Kurt)

> Bron: `docs/backlog/UserStories_VeerleKurt_v2.md`. Deze horen in de release omdat het door de klant
> gevraagde gedragswijzigingen zijn. Aanbevolen volgorde: US-26 → US-28 → US-27.

### US-26 · Meerprijs niet meer afdrukken op de bestelbon
**Als** gebruiker **wil ik** dat een ingegeven meerprijs niet als aparte "Meerprijs"-regel op de
bestelbon verschijnt **zodat** de klant die losse regel niet ziet.

**Acceptatiecriteria**
- Geen "Meerprijs"-regel op de bestelbon-PDF én in de preview.
- Offerte zonder meerprijs verandert niet.
- Totaal blijft intern consistent (subtotaal − korting + BTW = totaal).

**Betrokken bestand:** `Service/FactuurWorkflowService.cs`, methode `BuildLijnen` (± regel 228).
**Te bevestigen keuze:** valt de meerprijs volledig weg (regel én totaal — standaardaanname), of moet
hij stil in het totaal verrekend blijven zonder eigen regel? → vraag bij de start van de sessie.
⏱ ± 30 min · laag.

**PROMPT:**
```
Voer US-26 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 1). Analyseer eerst
Service/FactuurWorkflowService.cs BuildLijnen + de bestelbon-preview/PDF, bevestig met mij of de
meerprijs volledig weg moet (regel én totaal) of stil verrekend, en implementeer daarna op branch
feature/us26-meerprijs-verbergen met een test die bewijst dat er geen Meerprijs-regel meer komt en
het totaal consistent blijft. Sluit af met .\verify.ps1 groen.
```

### US-28 · Korting aftrekken van het bedrag inclusief btw
**Als** gebruiker **wil ik** dat een kortingspercentage van het bedrag **inclusief** btw wordt
afgetrokken **zodat** de korting rekent zoals met de klant afgesproken.

**Acceptatiecriteria**
- Bij `KortingPct > 0`: eindtotaal incl. = totaal incl. × (1 − KortingPct/100).
- Kortingregel op de bestelbon toont het **incl.**-bedrag.
- BTW en excl. worden consistent herleid uit het verlaagde incl.-totaal (BTW = incl − excl).
- Zonder korting verandert er niets; BTW-vrijstelling (0%) → incl = excl.

**Betrokken bestanden:** `Service/FactuurWorkflowService.cs` (`HerberekenTotalen`),
`Service/PdfFactuurExporter.cs` (`DrawTotals`). Detailberekening staat uitgewerkt in
`UserStories_VeerleKurt_v2.md` (US-28). ⏱ 1–2 u · medium.

**PROMPT:**
```
Voer US-28 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 1) en de detailuitwerking in
docs/backlog/UserStories_VeerleKurt_v2.md. Analyseer HerberekenTotalen + DrawTotals, implementeer
korting op incl.-btw op branch feature/us28-korting-incl-btw met tests voor: korting>0, korting=0,
en BTW-vrijstelling. Let op afronding en de preview in FactuurPreviewWindow. .\verify.ps1 groen.
```

### US-27 · Plandatum = afhaaldatum op de bestelbon
**Als** gebruiker **wil ik** dat de datum waarop ik een inlijsting in de kalender plan automatisch de
afhaaldatum op de bestelbon wordt **zodat** ik ze niet dubbel hoef in te geven.

**Acceptatiecriteria**
- Inlijsting (offerteregel) gepland op datum X → `OfferteRegel.AfhaalDatum` = X.
- Bestelbon toont per inlijsting dezelfde datum als de planning ("afhalen op" = plandatum).
- Meerdere inlijstingen met eigen plandatums → elk zijn eigen gelijke afhaaldatum.
- Handmatig afwijkende afhaaldatum blijft mogelijk (sync gebeurt bij plannen, niet afdwingend daarna).
- Bij meerdaagse spreiding: de **laatste** werkdag is de afhaaldatum.

**Betrokken bestanden:** `Service/WerkBonWorkflowService.cs`
(`PlanRegelMetDagCapaciteitAsync`, `VoegPlanningToeVoorRegelAsync`), sync naar
`Service/FactuurWorkflowService.cs`. ⏱ 2–3 u · medium.

**PROMPT:**
```
Voer US-27 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 1) en de detailuitwerking in
docs/backlog/UserStories_VeerleKurt_v2.md. Zet na het plannen OfferteRegel.AfhaalDatum gelijk aan de
plandag (laatste werkdag bij spreiding) op branch feature/us27-plandatum-afhaaldatum, met tests voor
enkel- en meerdaagse planning en controle dat de bestelbon de datum overneemt. .\verify.ps1 groen.
```

---

## Categorie 2 — PostgreSQL-hardening (verplicht bij multi-user)

> Bron: audit P5 en P6. Deze zijn géén blocker voor single-PC/SQLite, maar **wél** voor een gedeelde
> PostgreSQL-server. Doen vóór de eigenlijke uitrol (categorie 3).

### REL-01 · `DateTime.Now` → `UtcNow` normaliseren (audit 2.3 / P5)
**Als** beheerder **wil ik** dat alle timestamps in UTC worden opgeslagen **zodat** ze kloppen op een
PostgreSQL-server (vaak UTC) en GDPR-bewaartermijnen correct berekend worden.

**Probleem:** `SaveChangesAsync` en `StockService` gebruiken al `UtcNow`, maar auth-login
(`AuthService.cs`), GDPR (`GdprService.cs`, incl. retentie-cutoff), imports en modeldefaults
gebruiken lokale `DateTime.Now` → inconsistent op een UTC-server.

**Acceptatiecriteria**
- Alle persistente timestamps via `DateTime.UtcNow`.
- De UI converteert bij weergave naar lokale tijd (Europe/Brussels).
- GDPR-retentiecutoff rekent in UTC.
- Bestaande SQLite-data blijft leesbaar (documenteer of eenmalige conversie nodig is).
⏱ M.

**PROMPT:**
```
Voer REL-01 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 2, audit 2.3). Vervang alle
persistente DateTime.Now door UtcNow (AuthService, GdprService incl. retentie-cutoff, imports,
modeldefaults) op branch fix/datetime-utc-normalization, met UI-conversie naar lokale tijd bij
weergave en een test op de GDPR-retentieberekening. Documenteer of bestaande SQLite-data
geconverteerd moet worden. .\verify.ps1 groen.
```

### REL-02 · PostgreSQL-schemastrategie gelijktrekken (audit 8.3 / P6)
**Als** beheerder **wil ik** dat PostgreSQL hetzelfde EF-migratiesysteem gebruikt als SQLite **zodat**
er geen schema-drift tussen providers ontstaat en schemawijzigingen beheerd blijven.

**Probleem:** PostgreSQL draait nu via `EnsureCreatedAsync` (geen migratiehistorie), SQLite via echte
EF-migraties (`SqliteSchemaPatcher`). Twee strategieën = driftrisico.

**Acceptatiecriteria**
- PostgreSQL gebruikt `MigrateAsync` met dezelfde migratieset als SQLite (of een bewuste,
  gedocumenteerde provider-scheiding met identiek eindschema).
- Verse PG-database en verse SQLite-database leveren een gelijkwaardig schema op.
- Migraties zijn provider-neutraal of hebben expliciete provider-takken waar nodig
  (bv. `RowVersion`: SQLite `BLOB` vs PG `xmin`/`bytea`).
- CI/test dekt dat er geen pending model changes zijn op beide providers.

> **Aandachtspunt** (grootste onbekende van de hele release): de bestaande migraties zijn met de
> SQLite-provider gegenereerd. Voor PostgreSQL kan een aparte migratie-assembly of provider-conditie
> nodig zijn. Start deze sessie met een **analyse-only fase** voordat er code verandert.
⏱ M–L.

**PROMPT:**
```
Voer REL-02 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 2, audit 8.3). Begin ALLEEN met
analyse: hoe verhouden de huidige SQLite-migraties zich tot PostgreSQL (provider-specifieke types,
RowVersion, EnsureCreated vs Migrate)? Lever eerst een voorstel (één migratiesysteem voor beide
providers, of gedocumenteerde scheiding met identiek eindschema) en wacht op mijn akkoord. Daarna
implementeren op branch feature/pg-migration-strategy met een test die 'geen pending model changes'
op beide providers bewaakt. .\verify.ps1 groen.
```

---

## Categorie 3 — PostgreSQL-uitrol & release

> Deels code, deels operationeel. Doe dit pas ná categorie 1 en 2 groen + gemerged.

### REL-03 · Concurrency-conflict-UX afronden (audit 2.1/6.2 vervolg)
De domeinlaag heeft nu `RowVersion` + unieke factuurnummer-constraint (P1, gemerged). Voor multi-user
moet de **gebruiker** een nette melding krijgen bij een botsing i.p.v. een crash/stille overschrijving.

**Acceptatiecriteria**
- Bij `DbUpdateConcurrencyException` op een bewerkbare entiteit: duidelijke melding
  "iemand anders heeft dit ondertussen gewijzigd — herlaad en probeer opnieuw".
- Bij dubbele factuurnummer-insert (unieke constraint): nummer opnieuw toekennen met retry.
- Test met twee parallelle contexts die dezelfde offerte/voorraad/factuur muteren.
⏱ M.

**PROMPT:**
```
Voer REL-03 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 3). Voeg gebruiksvriendelijke
afhandeling toe voor DbUpdateConcurrencyException (herlaad-melding via toast) en retry op de unieke
factuurnummer-constraint, op branch feature/rel03-concurrency-ux, met parallelle-context-tests.
.\verify.ps1 groen.
```

### REL-04 · PostgreSQL server opzetten + data migreren (operationeel)
Geen app-codewijziging; dit is de infrastructuurstap op PC 1.

**Stappen**
1. PostgreSQL installeren op PC 1; database `quadrodb` + gebruiker `quadro` aanmaken.
2. `appsettings.json` naast de exe op PC 1 en PC 2 met de juiste connection string en
   `Password=__SECRET__` (PC 2 met PC 1's LAN-IP).
3. Op elke PC eenmalig `.\Scripts\set-db-secret.ps1` draaien (DPAPI-secret).
4. Bestaande SQLite-data migreren met `Scripts/migrate_to_postgres.py` (259 regels, bestaat al —
   controleer/actualiseer vóór gebruik).
5. Dagelijkse `pg_dump`-backup inplannen via Windows Taakplanner met `Scripts/backup-postgres.ps1`.
6. Firewall PC 1: poort 5432 open voor het lokale netwerk.

**Referenties:** `docs/CONFIGURATION.md`, `docs/BACKUP_RESTORE.md`,
`Scripts/{migrate_to_postgres.py,set-db-secret.ps1,backup-postgres.ps1}`.

**PROMPT (voorbereiding/controle):**
```
Voer REL-04-voorbereiding uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 3). Controleer en
actualiseer Scripts/migrate_to_postgres.py tegen het huidige schema (incl. Gebruikers, AuditLogs,
RowVersion), en schrijf een stap-voor-stap runbook (PostgreSQL install, db/gebruiker, appsettings,
set-db-secret op beide PC's, datamigratie, pg_dump-taak, firewall 5432). Geen productie-acties
uitvoeren — alleen script/runbook klaarzetten en .\verify.ps1 groen houden.
```

### REL-05 · Release bouwen & verifiëren (operationeel)
**Stappen**
1. `<Version>` in `QuadroApp.csproj` bumpen (bv. 1.0.5 → 1.1.0).
2. Mergen naar `main` → auto-tag → release-workflow bouwt win-x64 + osx-arm64 (Velopack, notarisatie).
3. **Eerst op jouw PC** de Velopack-update testen met je échte database (backup draait als vangnet):
   bestaande DB krijgt de upgrade-flow (healer → Baseline → alle migraties).
4. PC 2 laten verbinden met PostgreSQL; test gelijktijdig werken (twee offertes, twee facturen).
5. macOS-check bij Veerle: login-overlay, backup-map, dagelijkse workflow, afdruk bestelbon.

**PROMPT (checklist genereren — zie ook REL-06):** gebruik REL-06.

### REL-06 · `DEPLOYMENT_CHECKLIST.md` opstellen (audit 8.4)
Er is nog geen afvinkbare deploymentchecklist. Deze bundelt alle handmatige stappen.

**Moet bevatten:** admin-wachtwoord wijzigen, account Veerle aanmaken, demo-data uit (verifiëren dat
`QUADRO_SEED_DEMO` niet gezet is), `Backup:Directory` naar tweede schijf/NAS, secret zetten,
PostgreSQL-migratie, firewall, smoke-test (app start, home, offertes, planning, werktaak, import,
bestelbon-PDF), en een geteste restore.

**PROMPT:**
```
Voer REL-06 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 3). Maak docs/DEPLOYMENT_CHECKLIST.md
als afvinkbare checklist voor de 2-PC-release (PostgreSQL): pre-release codestappen, server-setup,
per-PC config/secret, datamigratie, backup + geteste restore, firewall, en een smoke-test-lijst.
Baseer je op CONFIGURATION.md, BACKUP_RESTORE.md, AUTH.md en dit releaseplan. Commit op branch
docs/deployment-checklist.
```

---

## Categorie 4 — Optionele hygiëne (na de release, blokkeert niets)

Bron: audit P7–P10 + `docs/cleanup-candidates.md`.

- **REL-H1 — `AsSplitQuery()`** op de brede Include-ketens in `FactuurWorkflowService` (audit 3.2). S.
- **REL-H2 — Dode code verwijderen** na de smoke-checklist: `LoginWindow`/`LoginViewModel` (vervangen
  door het login-overlay), legacy import-dialogen + `IDialogService`-methodes; en uitzoeken of
  `EPPlus` nog nodig is naast `ClosedXML` (audit 5.1/5.2, `cleanup-candidates.md`). S.
- **REL-H3 — DI-validatie & warning-budget**: `ValidateOnBuild = true`, overweeg `TreatWarningsAsErrors`
  op nieuwe code, `!` null-forgiving gericht vervangen (audit 1.3/7.3). S–M.
- **REL-H4 — Naamgeving & doc-drift**: NL/EN consistent maken, `App.axaml.cs`-comment over
  "swap UseSqlite→UseNpgsql" bijwerken (provider wordt al autogedetecteerd) (audit 7.1/7.4). S.
- **REL-H5 — Business-writes uit ViewModels naar services** (audit 1.1): structurele opschoning zodat
  autorisatie definitief op de service-laag zit. L — grootste refactor, bewust ná de release.

**PROMPT (voorbeeld):**
```
Voer REL-H2 uit volgens docs/backlog/RELEASE_2PC_PLAN.md (categorie 4). Doorloop eerst de runtime-
smoke-checklist uit docs/cleanup-candidates.md, verwijder daarna de bevestigde dode code (LoginWindow,
LoginViewModel, legacy import-dialogen + IDialogService-methodes) en bepaal of EPPlus weg kan.
Branch chore/cleanup-dead-code. .\verify.ps1 groen.
```

---

## Aanbevolen volgorde & statustabel

Aanbevolen: **US-26 → US-28 → US-27** (klantwensen, snel zichtbaar resultaat) → **REL-01 → REL-02 →
REL-03** (PostgreSQL-hardening) → **REL-06** (checklist) → **REL-04 → REL-05** (uitrol + release) →
optioneel REL-H1..H5.

| Stap | Onderwerp | Type | Insp. | Branch | Status |
|---|---|---|---|---|---|
| US-26 | Meerprijs niet afdrukken | klantwens | S | `feature/us26-meerprijs-verbergen` | ⬜ |
| US-28 | Korting op incl. btw | klantwens | M | `feature/us28-korting-incl-btw` | ⬜ |
| US-27 | Plandatum = afhaaldatum | klantwens | M | `feature/us27-plandatum-afhaaldatum` | ⬜ |
| REL-01 | DateTime → UtcNow | PG-hardening | M | `fix/datetime-utc-normalization` | ⬜ |
| REL-02 | PG-schemastrategie | PG-hardening | M–L | `feature/pg-migration-strategy` | ⬜ |
| REL-03 | Concurrency-conflict-UX | PG-hardening | M | `feature/rel03-concurrency-ux` | ⬜ |
| REL-06 | DEPLOYMENT_CHECKLIST | docs | S | `docs/deployment-checklist` | ⬜ |
| REL-04 | PostgreSQL uitrol + datamigratie | operationeel | M | — (runbook) | ⬜ |
| REL-05 | Release bouwen + verifiëren | operationeel | M | — (tag/Velopack) | ⬜ |
| REL-H1 | AsSplitQuery | hygiëne | S | `perf/split-queries` | ⬜ |
| REL-H2 | Dode code + EPPlus | hygiëne | S | `chore/cleanup-dead-code` | ⬜ |
| REL-H3 | DI-validatie + warnings | hygiëne | S–M | `chore/di-validation-warnings` | ⬜ |
| REL-H4 | Naamgeving + doc-drift | hygiëne | S | `chore/naming-doc-drift` | ⬜ |
| REL-H5 | VM-writes → services | refactor | L | `refactor/vm-to-service-writes` | ⬜ |

*Vink af (✅ + datum + PR-nummer) na elke merge, zodat een volgende chat de stand kent.*

**Al gedaan vóór dit plan (referentie):** P1 US-38 concurrency-domein (`RowVersion` TypeLijst +
unieke factuurnummer), P2 demo-seed guard, P3 autorisatie-gaten, P4 StockService-tests.
```
