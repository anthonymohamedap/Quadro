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

> **Bijgewerkte analyse + beslissing (28-07-2026):** EF past *alle* migraties in één project toe voor
> een context, dus SQLite- en Npgsql-migraties naast elkaar in hetzelfde project botsen. De nette oplossing
> vereist een **apart migratie-project per provider** (structurele verbouwing). Bovendien draait de live
> Postgres al op `EnsureCreated` (tabellen bestaan), dus overschakelen naar `MigrateAsync` vereist een
> eenmalige, delicate **baseline-markering** van de bestaande DB (anders probeert EF de tabellen opnieuw
> aan te maken). **Beslissing:** Postgres blijft voorlopig op `EnsureCreatedAsync` — dat werkt en het
> schema is stabiel na de release. US-41 pas oppakken wanneer iemand het op een dev-machine kan opzetten
> en itereren (kan niet blind/zonder draaiende EF-tooling). Waarde is laag zolang het schema niet wijzigt.

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

## US-46 · OfferteView (Offertebeheer) redesign naar begeleide ERP-werkruimte

**Als** verkoopmedewerker **wil ik** offertes maken en beheren via een gestructureerde, overzichtelijke
interface **zodat** ik sneller werk, minder fouten maak en de status van een offerte in één oogopslag zie.

**Achtergrond (review 27 juli 2026):** de OfferteView is functioneel het krachtigste scherm van de app,
maar combineert 11+ workflows op één pagina (klantselectie/-aanmaak, regels, regelbewerking,
productconfiguratie/afwerkingen, legacy-codes, prijsaanpassingen, berekening, opslaan, factureren,
planning, totalen). Dat verhoogt de cognitieve last. **Feitelijke omvang:** `Views/OfferteView.axaml`
≈ 762 regels, `ViewModels/Offerte/OfferteViewModel.cs` ≈ 1361 regels, **101 bindings, 15 commands**.
Het is het meest gebruikte dagelijkse scherm.

> **Belangrijk (risico):** dit is géén Export-Center-klus. Dit is het kritische kernscherm met 101
> bindings. Eén subtiel gebroken binding verstoort het dagelijkse werk. Daarom: **gefaseerd**, elke fase
> apart met `verify.ps1` + visuele klik-test op Windows, en **pas oppakken ná een stabiele 2-PC-deployment**
> — niet ertussen.

### Gunstige uitgangspunten (uit de review)
- Code-behind is leeg (geen event-handlers of named controls) → geen verborgen koppeling die breekt.
- Het ViewModel is al opgesplitst in sub-VM's: `KlantSelectie`, `Regelbeheer`, `Workflow`.
- Klantselectie heeft al `KlantZoekterm` + `GefilterdeKlanten` (ObservableCollection<Klant>) + `SelectedKlant`
  → moderne zoekbare selector is grotendeels een View-klus.
- `Offerte.Status` bestaat en bereikt sinds **US-42** écht de juiste waarden → statusbadge wordt nu zinvol.
- Financiële modelvelden bestaan: `SubtotaalExBtw`, `BtwBedrag`, `KortingPct`, `MeerPrijsIncl`,
  `VoorschotBedrag`, `IsVoorschotBetaald`, `TotaalInclBtw`.

### Randvoorwaarden uit het datamodel (NIET leverbaar zoals in de originele story)
- **Geen status per regel:** `OfferteRegel` heeft geen eigen status; alleen de offerte heeft er een.
  De "status-indicator per regelkaart" vervalt (of toont hooguit een selectie-/geldigheidsindicator).
- **Geen thumbnail/preview:** er is geen afbeeldingsdata in het model. De "preview op de regelkaart" vervalt.

### Ontwerpbeslissingen (vooraf vast te leggen)
1. **Secties i.p.v. harde tabs.** Bij een offerte weeg je klant, regels en prijs voortdurend tegen
   elkaar af; tabs die inhoud verbergen schaden de workflow. Kies één scrollpagina met duidelijke secties
   + een **sticky compacte samenvatting** (klant, aantal regels, totaal, afhaaldatum, status).
2. **Toegestane minimale VM-toevoegingen (read-only, presentatie):**
   - Berekende property `ResterendSaldo` (= `TotaalInclBtw − VoorschotBedrag`) voor het prijs-dashboard.
   - Eventueel 1–2 zichtbaarheidsvlaggen voor conditionele acties (Factureren/Bevestigen/Planning),
     voor zover die niet al via `Workflow` bestaan.
   Géén wijziging aan businessregels, commands of bestaande bindings.

> **Samenhang met US-47 (hand in hand).** US-46 en US-47 worden gekoppeld uitgevoerd: de
> View-redesign en de `OfferteViewModel`-decompositie lopen per fase samen. Concreet: **Fase A** van US-46
> (prijs-dashboard) gaat samen met de extractie van een `PrijsberekeningViewModel`/`TotalenViewModel`
> uit US-47 — de nieuwe `ResterendSaldo` en de gebundelde prijsweergave landen meteen in dat sub-VM
> i.p.v. los in `OfferteViewModel`. Zo verandert Fase A van "strikt View-only" naar "View + gecoördineerde,
> gedrag-behoudende VM-refactor", gedekt door de bestaande prijs-/workflow-tests (o.a.
> `PricingEngineTests`, `OffertePricingDraftTests`). Elke fase blijft groen via `verify.ps1` + visuele test.

### Fasering (elke fase = eigen commit(s), `verify.ps1` + visuele QA, mergebaar los)
- **Fase A — Overzicht & acties (hoogste waarde, laagste risico).** Sticky compacte samenvatting;
  statusbadge (`Offerte.Status` → kleur/label via converter); prijs-samenvatting bundelen (subtotaal,
  korting, meerprijs, btw, voorschot, resterend saldo, totaal) i.p.v. versnipperd; footer-actie-hiërarchie
  (primair **Offerte opslaan**; secundair Berekenen/Regel dupliceren; conditioneel Factureren/Bevestigen/
  Planning); legacy-code in een inklapbare "Geavanceerd"-sectie.
- **Fase B — Afwerkingen als kaarten.** Glas/Passe-partout(1&2)/Diepte/Opkleven/Rug elk in een Expander;
  variant-selector pas tonen/actief wanneer een type gekozen is; minder verticale ruis.
- **Fase C — Regels & regeldetails.** Regellijst als rijkere kaarten (titel, maat, lijst-type, prijs,
  selectie-indicator); regeldetail-formulier groeperen in secties (Algemeen / Lijst / Afmetingen / Prijs /
  Notities).
- **Fase D — Klantselector-polish.** Zoekbare selector met klantkaarten en snelacties; geselecteerde klant
  als compacte profielkaart (bouwt op bestaande `KlantZoekterm`/`GefilterdeKlanten`/`SelectedKlant`).

### Acceptatiecriteria
- Alle 101 bindings en 15 commands blijven werken; geen functionele regressie (business ongewijzigd).
- Duidelijke workflow-secties + altijd zichtbare compacte samenvatting.
- Afwerkingen als inklapbare kaarten; varianten enkel zichtbaar na typekeuze.
- Rijkere regelkaarten (binnen de grenzen van het datamodel) + gegroepeerd regeldetail-formulier.
- Prijs-dashboard met live waarden incl. resterend saldo.
- Statusbadge met de offerte-levenscyclus.
- Legacy in "Geavanceerd"; nette primair/secundair/conditioneel-actie-indeling.
- Enterprise-UI-regels: MDI-iconen (geen emoji), 8pt-spacing, trimming+tooltips, `{DynamicResource}`-tokens,
  toetsenbordnavigatie + zichtbare focus, responsive.

### Definition of Done
Verbeterde informatie-architectuur, minder scrollen, betere hiërarchie, moderne klantselectie, betere
regelkaarten, professionele actie-indeling, prijs-dashboard, toegankelijkheid — en **nul regressies**,
per fase geverifieerd op Windows.

⏱ Schatting: **L** (grootste scherm; \~40–50 dev-uur, gefaseerd). **Post-release, na stabiele deployment.**

**PROMPT (per fase uitvoeren, niet in één keer):**
```
Voer US-46 FASE A uit volgens docs/backlog/UserStories_VervolgFuncties.md. View-only redesign van
Views/OfferteView.axaml: sticky compacte samenvatting, statusbadge (Offerte.Status via converter),
gebundeld prijs-dashboard (incl. berekende ResterendSaldo — enige toegestane VM-toevoeging, read-only),
footer-actie-hiërarchie (primair Opslaan; secundair Berekenen/Dupliceren; conditioneel Factureren/
Bevestigen/Planning), legacy in Geavanceerd-Expander. Behoud ALLE bindings/commands; geen businesslogica.
MDI-iconen, 8pt-spacing, trimming+tooltips. Branch feature/us46-offerte-fase-a. .\verify.ps1 groen +
visuele klik-test. Wacht daarna op akkoord voor Fase B.
```

---

## US-47 · OfferteViewModel decompositie (god-object opsplitsen) — hand in hand met US-46

**Als** ontwikkelaar **wil ik** `OfferteViewModel` verder opsplitsen in gerichte sub-viewmodels
**zodat** het scherm onderhoudbaar blijft en de US-46-redesign schoon kan aanhaken, zonder dat er
businessgedrag verandert.

**Achtergrond (codebase-statistiek 27 juli 2026):** `OfferteViewModel.cs` is met **1.361 regels** het
grootste handgeschreven bestand en het enige echte "god-object". Het is al deels opgesplitst in
`KlantSelectieViewModel`, `Regelbeheer` en `Workflow`, maar de **prijs-/totalenlogica** zit er nog in.
De rest van de codebase (Service/Model/Data/Validatie) is gezond verdeeld — hier valt de enige
architecturale winst te halen.

> **Koppeling met US-46.** Deze story loopt **hand in hand** met de OfferteView-redesign: de extracties
> hieronder ondersteunen telkens de bijhorende US-46-fase. Ze delen branch en fasering; niet los uitvoeren.

### Aanpak (gedrag-behoudend, gedekt door bestaande tests)
- **Stap 1 (samen met US-46 Fase A) — `PrijsberekeningViewModel` / `TotalenViewModel`.**
  Verplaats de prijs-/totalen-eigenschappen en -berekening (subtotaal, btw, korting, meerprijs, voorschot,
  totaal + nieuwe `ResterendSaldo`) naar een eigen sub-VM, geëxposeerd op `OfferteViewModel` (zoals
  `KlantSelectie`/`Regelbeheer` nu). Bindings in de view wijzen naar het sub-VM; commands blijven.
- **Stap 2 (optioneel, samen met US-46 Fase B/C) — afwerkings-/regeldetailstukken** die nog in het
  hoofd-VM zitten verhuizen naar `Regelbeheer` of een nieuw `AfwerkingConfiguratieViewModel`, mits dat de
  redesign vereenvoudigt.
- Puur **structureel**: geen wijziging aan berekeningen, validaties of commands. Elke stap moet de
  bestaande suite groen houden (o.a. `PricingEngineTests`, `OffertePricingDraftTests`, `WorkflowServiceTests`).

### Acceptatiecriteria
- `OfferteViewModel` wordt merkbaar kleiner; prijs-/totalenlogica zit in een eigen, getest sub-VM.
- Geen enkele binding/command breekt; identiek gedrag (zelfde totalen, zelfde workflow).
- Bestaande tests blijven groen; waar logica verhuist, verhuizen/behouden de tests mee.
- Geen nieuwe businessregels.

⏱ Schatting: **M**, gefaseerd samen met US-46. **Post-release, na stabiele deployment.**

**PROMPT:**
```
Voer US-47 Stap 1 uit, samen met US-46 Fase A, volgens docs/backlog/UserStories_VervolgFuncties.md.
Extraheer de prijs-/totalenlogica uit OfferteViewModel naar een PrijsberekeningViewModel (sub-VM,
geëxposeerd zoals KlantSelectie/Regelbeheer), inclusief de berekende ResterendSaldo. Puur structureel,
gedrag-behoudend: geen wijziging aan berekeningen/commands, alle bindings blijven werken. Houd de
bestaande tests groen (PricingEngineTests, OffertePricingDraftTests). Branch feature/us46-offerte-fase-a
(gedeeld met US-46). .\verify.ps1 groen + visuele test.
```

---

## US-48 · Eenmalige status-reconciliatie van bestaande offertes

**Als** gebruiker **wil ik** dat bestaande offertes hun juiste status krijgen op basis van hun werkbon en
bestelbon **zodat** de lijst geen offertes meer toont op "Concept" terwijl de werkbon al afgewerkt en de
bestelbon al betaald is.

**Achtergrond (gemeld 28 juli 2026):** US-42 propageert de offertestatus **op het moment van een
overgang** (werkbon → InUitvoering/Afgewerkt, bestelbon → Betaald). Offertes waarvan die overgangen
al gebeurd waren **vóór** US-42 live ging, zijn nooit bijgewerkt — hun `Offerte.Status` is stil blijven
staan (bv. Concept/Goedgekeurd). De lenient forward-only propagatie (`OfferteStatusPropagation`) lost dit
niet op, want die raakt offertes vóór Goedgekeurd bewust niet aan. Er is dus een **eenmalige
reconciliatie** nodig die de status afleidt uit de werkelijke staat.

### Acceptatiecriteria
- Een eenmalige actie werkt alle offertes bij op basis van hun werkbon + bestelbon (Factuur):
  - Bestelbon **Betaald** → Offerte **Betaald**
  - Bestelbon bestaat (aangemaakt) of werkbon **Afgewerkt** → Offerte **Gefactureerd**
  - Werkbon **InUitvoering** → Offerte **InProductie**
  - Werkbon bestaat (Gepland) → minstens **Goedgekeurd**
- **Nooit terug** (alleen omhoog corrigeren) en **Geannuleerde offertes blijven ongemoeid**.
- In tegenstelling tot de gewone propagatie corrigeert dit óók offertes die nu vóór Goedgekeurd staan
  (Concept/Verzonden) maar aantoonbaar verder in de productie zitten.
- Idempotent: nogmaals draaien verandert niets meer.
- Multi-user/transactioneel veilig; respecteert de optimistic concurrency.
- Tests over de mapping (elke bron-combinatie → verwachte offertestatus) + idempotentie.

### Technische uitwerking
- Nieuwe `IOfferteStatusReconciliatieService.ReconcilieerAlleAsync()` die per offerte de doelstatus
  bepaalt uit `WerkBon.Status` + de bijhorende `Factuur.Status`, en `Offerte.Status` naar boven bijstelt.
- Aparte reconciliatie-mapping (niet `OfferteStatusPropagation.TryAdvanceTo`, want die negeert pre-Goedgekeurd).
- **Uitvoering:** admin-only actie in de app ("Herbereken offertestatussen", bv. in Instellingen of
  Gebruikersbeheer) — expliciet en herhaalbaar. Alternatief: eenmalig bij opstarten met een vlag, maar
  een expliciete knop is veiliger en auditbaar (US-36 logt de wijzigingen).
- Draai het één keer na de uitrol; daarna houdt US-42 alles vanzelf in sync.

⏱ Schatting: S–M. **Losstaand; kan vóór of los van US-46.**

**PROMPT:**
```
Voer US-48 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Bouw IOfferteStatusReconciliatie-
Service.ReconcilieerAlleAsync die per offerte de doelstatus afleidt uit werkbon + bestelbon (Betaald→
Betaald, bestelbon/afgewerkt→Gefactureerd, InUitvoering→InProductie, werkbon→min. Goedgekeurd), alleen
omhoog, Geannuleerd ongemoeid, idempotent, transactioneel. Admin-only actie "Herbereken offertestatussen"
in de UI. Tests over de mapping + idempotentie. Branch feature/us48-status-reconciliatie. .\verify.ps1 groen.
```

---

## US-50 · Drag-drop planning migreren naar nieuwe Avalonia DataTransfer-API

**Waarom.** De drag-drop in de Planningskalender (regel → dagtegel, US-49) gebruikt de
klassieke Avalonia drag-drop API: `DataObject`, `DragDrop.DoDragDrop(...)` en
`DragEventArgs.Data`. Avalonia 11.3 heeft die soft-deprecated (CS0618) t.v.v. de nieuwe
`DataTransfer`-API (`DoDragDropAsync`, `DragEventArgs.DataTransfer`, `DataFormat`/
`DataTransferItem`). De klassieke API werkt volledig; de waarschuwingen zijn nu gericht
onderdrukt met `#pragma warning disable CS0618` rond het DnD-blok in
`Views/PlanningCalendarWindow.axaml.cs`. Dit is technische schuld: bij een toekomstige
Avalonia-upgrade kan de klassieke API verdwijnen.

**Wat.** Het DnD-blok herschrijven naar de nieuwe API en het `#pragma` weghalen:
- sleep-start: `DataTransfer` opbouwen met een typed `DataFormat` voor `planRegelId` en
  `DragDrop.DoDragDropAsync(...)` aanroepen;
- `OnDragOver`/`OnDrop`: lezen via `DragEventArgs.DataTransfer` (`TryGetValue`/`Contains`)
  i.p.v. `e.Data`.

**Risico / aanpak.** Laag-medium, maar de nieuwe lees-API (`DataTransfer`-extensies) is
nieuw en niet in de sandbox te compileren. Doen op een aparte branch, op Windows testen
dat slepen van een regel naar een dag nog steeds plant (incl. de betaald/afgewerkt-guard),
`.\verify.ps1` groen. Puur interne refactor — geen gedrags- of UI-wijziging.

**Prompt.**
```
Migreer de drag-drop in Views/PlanningCalendarWindow.axaml.cs van de klassieke Avalonia
DnD-API (DataObject/DoDragDrop/DragEventArgs.Data) naar de nieuwe DataTransfer-API
(DoDragDropAsync/DragEventArgs.DataTransfer/DataFormat). Verwijder daarna het
#pragma warning disable/restore CS0618. Gedrag identiek houden: regel slepen naar dagtegel
plant via vm.PlanRegelOpDatumAsync, betaald/afgewerkt geblokkeerd. Branch
feature/us50-datatransfer-dnd. .\verify.ps1 groen, 0 warnings.
```

---

## US-49 · Planning-kalender redesign naar productieplanning-werkruimte (gefaseerd)

**Als** productieplanner **wil ik** werkbonnen beheren via een overzichtelijke planning-werkruimte
**zodat** ik snel capaciteit zie, planningsconflicten herken en efficiënt kan plannen.

**Achtergrond (review 28-07-2026):** de planningskalender is krachtig (capaciteit, geblokkeerde dagen,
per-regel plannen, weekoverzicht) maar toont veel operationele info tegelijk en mist een dashboard/
hiërarchie. **Feitelijke omvang:** `Views/PlanningCalendarWindow.axaml` ≈ 564 regels, 62 bindings,
8 commands; VM netjes opgesplitst (`PlanningCalendarViewModel` + `PlanningUitvoeringViewModel` +
`PlanningCalendarModels`).

> **Risicopunt (belangrijk):** dit scherm heeft **code-behind-koppeling** — `DayTile_PointerPressed`
> (klik op een dagtegel) en `OnDataContextChanged`. Een herontwerp van de kalendertegels moet die
> dag-klik-afhandeling intact houden, anders breekt de dagselectie. Gefaseerd + verify + visuele test
> per fase, en pas oppakken bij stabiele deployment.

### Wat bestaat al (grond voor View-only)
- `DayTile`: `Busy` (bezetting), vulkleur, `IsGeblokkeerd`, `IsToday`, `DayNumber`, `BusyLabel`.
- `DayRow` (uren/minuten per dag, geblokkeerd), `WeekRow` (BonNr, KlantNaam, DuurMin, dag), `WeekSummary`
  (titel/range/totaal), `SelectedDayRow`, `SelectedWeekNr`, `BlokkeerReden`.
- Commands: plannen, per-regel plannen, herplannen, taak verwijderen, vorige/volgende maand, vandaag,
  weekwerklijst, dag blokkeren.

### Toegestane minimale VM-toevoegingen (berekende presentatie, geen businesslogica)
- KPI-aggregaten: utilisatie vandaag/deze week, aantal open werkbonnen, aantal blokdagen, beschikbare uren.
- Week-metrics op `WeekSummary`: geplande uren, resterende capaciteit, utilisatie %, blokdagen, "health".
- Maand-/jaarkiezer-bindings + "spring naar week"; eventueel een "week blokkeren"-command.

### Randvoorwaarden datamodel (NIET leverbaar zoals gevraagd)
- **Prioriteit per taak** — geen prioriteitsveld op werktaak/werkbon. Vervalt op de taakkaart.
- **Productiestatus per taak** — er is `WerkTaak.VoorraadStatus` (voorraad), geen echte productiestatus.
- **"Vertraagde werkbonnen" als KPI** — er is geen deadline-vs-geplande-datum-vergelijking (offerte-deadline
  is een dood veld). Kan niet zinvol berekend worden zonder eerst dat begrip + data toe te voegen. Laat deze
  KPI weg tot dat er is.

### Fasering (elke fase = eigen commit(s), `.\verify.ps1` + visuele test, mergebaar los)
- **Fase A** — dashboard-header met de afleidbare KPI's (utilisatie, open werkbonnen, blokdagen,
  beschikbare uren) + een capaciteitslegende. "Vertraagd" bewust weglaten.
- **Fase B** — kalendertegels verrijken (voortgangsbalk, %, blocked-badge, overload-kleur), **voorzichtig
  met de `DayTile_PointerPressed`-koppeling**.
- **Fase C** — dag-detailpaneel (datum, workload, beschikbare uren, blokstatus + reden, taken, acties) +
  week-samenvattingkaarten met de nieuwe berekende metrics.
- **Fase D** — navigatie (maand-/jaarkiezer, spring-naar-week) + planningsacties netjes groeperen
  (primair vs. destructief gescheiden).
- **Toekomst (niet nu bouwen):** drag-drop, Gantt, resource-/machine-/personeelsplanning — alleen de layout
  zo houden dat het later past.

### Acceptatiecriteria
- Alle 62 bindings + 8 commands blijven werken; geen wijziging aan planningslogica/algoritmes/blokdagen.
- Dashboard-KPI's, verrijkte kalender, dag-detailpaneel, week-kaarten, gegroepeerde acties, betere navigatie.
- MDI-iconen (geen emoji), 8pt-spacing, trimming+tooltips, tokens, toetsenbord + zichtbare focus, responsive.
- Geen functionele regressie (per fase geverifieerd, dag-klik werkt).

⏱ Schatting: **L** (~50–56 dev-uur, gefaseerd). **Post-release, na stabiele deployment.**

**PROMPT (per fase, niet in één keer):**
```
Voer US-49 FASE A uit volgens docs/backlog/UserStories_VervolgFuncties.md. View-first redesign van
Views/PlanningCalendarWindow.axaml: dashboard-header met afleidbare KPI's (utilisatie vandaag/week,
open werkbonnen, blokdagen, beschikbare uren) + capaciteitslegende. Enige toegestane VM-toevoeging:
berekende KPI-properties (read-only, geen businesslogica). Laat "vertraagde werkbonnen" weg (geen
deadline-data). Behoud ALLE bindings/commands en de DayTile_PointerPressed-koppeling. MDI-iconen,
8pt-spacing. Branch feature/us49-planning-fase-a. .\verify.ps1 groen + visuele test. Wacht op akkoord
voor Fase B.
```

---

## US-51 · Werkbonnen-lijst + werkbon-overzicht revamp

**Als** werkvoorbereider/zaakvoerder **wil ik** in de werkbonnenlijst meteen de kerninfo per werkbon zien
én per werkbon een overzichtelijk scherm met álle details **zodat** ik niet hoef te graven en niet per
ongeluk in de offerte beland.

**Achtergrond (review 29-07-2026):** `Views/WerkBonLijstView.axaml` (~344 regels) +
`ViewModels/WerkBonLijstViewModel.cs` (~263 regels). Code-behind is minimaal (laadt bij
`AttachedToVisualTree`).

Huidige situatie:
- **Lijst** toont enkel: bon-nr, achternaam klant, ruwe status-enum-tekst, aanmaakdatum + "Bekijk".
- **Detail** = smal inline paneel rechts: bon-nr, klant (achternaam), status-dropdown, "Opslaan status",
  een knop "📄 Open bestelbon", en de takenlijst met per-taak bestel-/voorraadacties.
- **Verwarrend (feitelijke bug):** de knop **"Open bestelbon" roept `OpenBestelBonAsync` →
  `OpenOfferteAsync` aan en opent dus de OFFERTE**, niet een bestelbon. Label ≠ gedrag.
- **Ontbreekt in de lijst:** offerte-referentie, volledige klantnaam, aantal taken, bestel-voortgang,
  afhaaldatum, totaalprijs, en een gekleurde status-badge (nu kale enum-tekst).

### Wat bestaat al (geen datamodel-uitbreiding nodig)
- `WerkBon`: `TotaalPrijsIncl`, `AfhaalDatum`, `Status`, `AangemaaktOp`, `BijgewerktOp`, `Taken`.
- `WerkTaak`: `Omschrijving`, lijst via `OfferteRegel.TypeLijst`, afmeting via `OfferteRegel`,
  `DuurMinuten`, `GeplandVan/Tot`, `IsBesteld`/`BestelDatum`, `IsOpVoorraad`, `VoorraadStatus`,
  `BenodigdeMeter`.
- `Factuur` (= bestelbon, **uniek per `WerkBonId`**): `FactuurNummer`, `DocumentType`, `Status`
  (incl. *Betaald*), `FactuurDatum`, `VervalDatum`, totalen, voorschot.
- `Offerte → Klant` voor volledige naam + contactgegevens.
- Bestaande commands (behouden): `SelecteerWerkBon`, `SaveStatus`, `MarkeerLijstAlsBesteld`,
  `OpenPlanning`, `Refresh`, `GaTerug`, zoek-/jaarfilter.

### Beslissingen (met gebruiker afgestemd, 29-07-2026)
- Het werkbon-overzicht opent als **apart modaal venster** (zoals `PlanningCalendarWindow`), niet als
  volledig genavigeerd scherm.
- De lijst **houdt een compacte inline preview**; een knop **"Open werkbon"** opent het volledige
  overzichtsvenster.
- De offerte is **één stap verwijderd**: knop **"Open offerte"** staat op het overzichtsvenster, niet
  meer los in de lijst. Zo verdwijnt de misleidende "Open bestelbon"-knop.

> **Risicopunten (belangrijk):**
> - "Opslaan status" met overgang naar **Afgewerkt** heeft neveneffecten via
>   `IWorkflowService.ChangeWerkBonStatusAsync` (maakt bestelbon/factuur aan + navigeert naar Facturen) —
>   deze logica **ongewijzigd** meenemen naar het venster.
> - Per-lijst bestellen (`MarkeerLijstAlsBesteldAsync` + bestelvorm-radio's) moet mee naar het overzicht.
> - Alle bestaande bindings/commands + de `#Root.((vm:...)DataContext).*`-verwijzingen behouden.

### Fasering (elke fase = eigen branch, `.\verify.ps1` + visuele test, mergebaar los)
- **Fase A — Lijst-revamp.** Rijkere rij/kaart met **gekleurde status-badge** (converter, consistent met
  offerte-badges), volledige klantnaam, offerte-nr, aantal taken, afhaaldatum, **bestel-voortgang**
  (bv. "3/5 besteld"), totaalprijs. Statusfilter naast het jaarfilter. Inline preview compacter, met knop
  "Open werkbon".
- **Fase B — Werkbon-overzichtsvenster (nieuw).** `WerkBonDetailWindow` (modal) +
  `WerkBonDetailViewModel`. Kop: bon-nr + status-badge + klant + datums + totaal. Secties: planning-
  samenvatting (geplande uren, periode), taken/lijsten (afmeting, duur, bestel-/voorraadstatus),
  gekoppelde **bestelbon/factuur read-only** (nummer, status, bedragen). Acties: status wijzigen +
  opslaan, per-lijst bestellen. Secundair onderaan: **"Open offerte"**.
- **Fase C — Flow opschonen.** Verwijder de misleidende "Open bestelbon"; offerte enkel nog via het
  overzichtsvenster. Emoji's (🧾👁📌📄🛠) vervangen door MDI-PathIcons in de huisstijl.

### Acceptatiecriteria
- Lijst toont per werkbon minstens: bon-nr, klant (volledige naam), status als **gekleurde badge**,
  aanmaak-/afhaaldatum, aantal taken, bestel-voortgang, totaalprijs.
- "Open werkbon" opent een modaal venster met alle werkbon-info overzichtelijk gegroepeerd.
- Statuswijziging **incl. neveneffect** (Afgewerkt → bestelbon/factuur) werkt vanuit het venster;
  per-lijst bestellen werkt.
- "Open offerte" op het venster opent de gekoppelde offerte (huidige `OpenOfferteAsync`).
- Gekoppelde bestelbon/factuur wordt **read-only** getoond (geen dubbele bewerkweg).
- Geen functionele regressie: alle bestaande commands + zoek-/jaarfilter blijven werken.
- Enterprise-UI: MDI-iconen (geen emoji), tokens, 8pt-spacing, trimming+tooltips, kolommen met MinWidth,
  ScrollViewer, zichtbare focus.

### Technische uitwerking
- Nieuw `Views/WerkBonDetailWindow.axaml(.cs)` + `WerkBonDetailViewModel`; laad de werkbon met Includes
  (`Offerte.Klant`, `Taken.OfferteRegel.TypeLijst`) + de gekoppelde `Factuur` (query op `WerkBonId`).
- Verplaats de status-/bestel-acties uit het inline paneel naar het venster (logica hergebruiken, niet
  herschrijven). Inline preview behoudt enkel lezen + "Open werkbon".
- Status-badge-converter `WerkBonStatus → kleur/label` (hergebruik/naar analogie van de offerte-badge).
- Bestel-voortgang = `Taken.Count(t => t.IsBesteld)` / `Taken.Count` (excl. op-voorraad indien gewenst).
- Vervang emoji door `PathIcon` met MDI-data, net als in de planning-revamp.

⏱ Schatting: **M–L**, gefaseerd. **Post-release, na stabiele deployment.**

**PROMPT (Fase A, niet in één keer):**
```
Voer US-51 FASE A uit volgens docs/backlog/UserStories_VervolgFuncties.md. Revamp de werkbonnenlijst in
Views/WerkBonLijstView.axaml: rijkere rijen met gekleurde WerkBonStatus-badge (nieuwe converter), volledige
klantnaam, offerte-nr, aantal taken, afhaaldatum, bestel-voortgang (x/y besteld) en totaalprijs; voeg een
statusfilter toe naast het jaarfilter; maak de inline preview compacter met een knop "Open werkbon"
(venster volgt in Fase B). Behoud ALLE bestaande commands/bindings en de LoadAsync-on-attach. MDI-iconen
i.p.v. emoji, tokens, 8pt. Branch feature/us51-werkbon-lijst-fase-a. .\verify.ps1 groen + visuele test.
Wacht op akkoord voor Fase B (het overzichtsvenster).
```

---

### Status
| Story | Onderwerp | Prioriteit | Status |
|---|---|---|---|
| US-40 | Audit-leesscherm in de app | Medium | ✅ gereleased |
| US-41 | Volledige EF-migraties voor PostgreSQL | Medium | 🟢 geïmplementeerd + gevalideerd op wegwerp-PG (branch `feature/us41-pg-ef-migrations`): EF-laag → `QuadroApp.Data`, apart `QuadroApp.Migrations.Npgsql` + Npgsql-Baseline, `PostgresSchemaPatcher` (self-healing: verse DB → MigrateAsync, bestaande EnsureCreated-DB → Baseline gemarkeerd), init op `MigrateAsync`, drift-check clean. Rest: baseline-markering op de live `quadrodb` (self-heal bij 1e start, na pg_dump-backup) |
| US-42 | Statussync offerte ↔ werkbon ↔ bestelbon | Hoog | ✅ gereleased |
| US-43 | Retry-on-failure via execution strategy | Medium | 🟡 branch `feature/us43-retry-execution-strategy` — alle 12 transactie-sites in `ExecuteWithRetryAsync`, retry aan; verify op Windows + Postgres-smoketest, dan merge |
| US-44 | Export Center → enterprise wizard (+ export-kolomfixes) | Medium | ✅ gereleased |
| US-45 | Facturen als export-dataset | Medium | ✅ gemerged |
| US-46 | OfferteView redesign — Fase A | Hoog | ✅ afgerond op branch `feature/us46-offerte-fase-a` (actie-hiërarchie, statusbadge, compacte sticky samenvatting, legacy inklapbaar; prijs-dashboard + conditionele acties bleken al aanwezig) |
| US-46b | OfferteView redesign — Fase B/C/D (cosmetische kaart-herbouw) | Laag | ✅ afgerond op branch `feature/us46b-offerte-fase-b` (B: afwerkingen als Expanders; C: regelkaarten + gegroepeerd regeldetail-formulier; D: klant-profielkaart + rijkere klantkeuzelijst; extra: onafhankelijke scroll per kolom, factuur→bestelbon-tekst) — visuele check op Windows |
| US-47 | OfferteViewModel decompositie (god-object) | Hoog | ✅ was al voldaan (prijslogica in OffertePrijsViewModel, RestTeBetalen bestaat) |
| US-48 | Eenmalige status-reconciliatie bestaande offertes | Hoog | ✅ gemerged |
| US-49 | Planning-kalender redesign (gefaseerd, productieplanning) | Medium | ✅ gemerged (Fase A–D + drag-drop, dagtegel-hints, weekdetail per dag) |
| US-50 | Drag-drop planning → nieuwe DataTransfer-API (CS0618 wegwerken) | Laag | ⬜ tech-debt; CS0618 nu onderdrukt met `#pragma` |
| US-51 | Werkbonnen-lijst + werkbon-overzicht revamp (gefaseerd) | Medium | ✅ gemerged (Fase A rijkere lijst+statusfilter, B modaal overzichtsvenster, C flow-opschoning; extra: duidelijker bestel-UI, afwerking+code+varianten, zijbalk verwijderd, Gefactureerd→Besteld, planning→offerte InProductie) |

> Ook gereleased (buiten de US-nummering): offertelijst-laadfouten via toast, en de CI-fix voor
> release-automatisering (`workflow_dispatch` + optionele `RELEASE_PAT`). Sindsdien wordt elke release
> automatisch gebouwd; alleen-docs merges triggeren géén release (`paths-ignore` in `auto-tag.yml`).
