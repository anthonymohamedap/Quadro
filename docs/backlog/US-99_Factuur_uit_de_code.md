# US-99 — Het woord "factuur" uit de code (bestelbon overal)

Stand 31/10/2026. Aanleiding: mail Veerle 28/10 — *"Wat bedoel je met 'facturen'? We moeten die terminologie eens op orde krijgen."* Quadro maakt geen facturen; het document heet overal **bestelbon**. In de app staat "factuur" sinds US-85 nergens meer zichtbaar op het scherm, maar de code, de database en enkele meldingen gebruiken het woord nog volop.

**Als** ontwikkelaar en gebruiker **wil ik** dat het document overal "bestelbon" heet, in de app, de meldingen, de exports én de code, **zodat** er geen verwarring meer is tussen wat de klant krijgt en hoe wij erover praten.

## Omvang (inventaris 31/10)

- 80 bestanden, ± 1 330 keer "factuur/facturen" (zonder migraties).
- Bestanden met "Factuur" in de naam: `Factuur.cs`, `FactuurLijn.cs`, `FactuurSchemaUpgrade.cs`, `FactuurWorkflowService.cs`, `FactuurExportService.cs`, `PdfFactuurExporter.cs`, `IFactuurWorkflowService.cs`, `IFactuurExportService.cs`, `IFactuurExporter.cs`, `CentralExcelExportService.Facturen.cs`, `FactuurOmschrijvingConverter.cs`, `ViewModels/Factuur/*` (3), `FacturenView`, `FactuurPreviewWindow`, `FactuurInfoDialog`, `FactuurWorkflowServiceTests.cs`.
- Meest voorkomende namen: `Factuur` (398), `FactuurId` (157), `Facturen` (142), `FactuurStatus` (112), `FactuurNummer` (105), `FactuurLijn` (80), `FactuurDatum` (67).
- Database: tabellen `Facturen` en `FactuurLijnen`; kolommen `FactuurNummer`, `FactuurDatum`, `FactuurLijnen.FactuurId`, `Ontvangsten.FactuurId`; kolom `DocumentType` met de waarde "Factuur" bij oude bonnen.
- Ook: `Permissie.Factureren`, Excel-export (`ExcelExportDataset.Facturen`, "Facturen / bestelbonnen"), `Scripts/migrate_to_postgres.py`, `Scripts/MigrationTool`, docs (README, AUTH, GDPR, US41-plan).

## Hernoemingen

| Nu | Wordt |
|---|---|
| `Factuur`, `FactuurLijn`, `FactuurStatus` | `Bestelbon`, `BestelbonLijn`, `BestelbonStatus` |
| `FactuurId`, `FactuurNummer`, `FactuurDatum` | `BestelbonId`, `BestelbonNummer`, `BestelbonDatum` ¹ |
| `Facturen`, `FactuurLijnen` (DbSet + tabel) | `Bestelbonnen`, `BestelbonLijnen` |
| `FactuurWorkflowService`, `FactuurExportService`, `PdfFactuurExporter` (+ interfaces) | `BestelbonWorkflowService`, `BestelbonExportService`, `PdfBestelbonExporter` |
| `FacturenViewModel`/`FacturenView`, `FactuurPreview*`, `FactuurInfoDialog*` | `BestelbonnenViewModel`/`BestelbonnenView`, `BestelbonPreview*`, `BestelbonGegevensDialog*` |
| `FactuurOmschrijvingConverter` | `BestelbonOmschrijvingConverter` |
| `Permissie.Factureren` | `Permissie.Bestelbonnen` |
| `ExcelExportDataset.Facturen` | `ExcelExportDataset.Bestelbonnen` |
| `DocumentType` (enkel "Bestelbon" of "Factuur") | weg (altijd bestelbon) |

¹ Niet `BestelDatum`: die naam bestaat al voor de bestellingen bij de leverancier (o.a. op de werkbon), naast de datum van de bestelbon. Twee betekenissen voor één naam zou verwarrend zijn.

## Acceptatiecriteria

1. Geen enkele melding, titel, tooltip, export-kolom of PDF-tekst bevat nog "factuur" (bv. "Factuur niet gevonden", "Alleen draft facturen zijn bewerkbaar", Excel-dataset "Facturen / bestelbonnen").
2. `grep -ri "factu"` op de broncode (zonder `Migrations/` en zonder de migratie van deze story) geeft niets meer.
3. Databasetabellen en -kolommen zijn hernoemd via een EF-migratie (SQLite + Npgsql) **zonder gegevensverlies**: bestaande bestelbonnen, lijnen, betalingen (`Ontvangsten`) en nummers blijven identiek.
4. Oude bonnen met `DocumentType = "Factuur"` worden mee omgezet (of de kolom verdwijnt).
5. De Excel-export blijft werken; kolomtitels zeggen "Bestelbon".
6. `Scripts/migrate_to_postgres.py`, `Scripts/MigrationTool` en de docs gebruiken de nieuwe namen.
7. `verify.ps1` groen; alle bestaande tests blijven slagen (hernoemd waar nodig).

## Aanpak (drie PR's, elk mergebaar en los te testen)

**Fase A — wat de gebruiker ziet (± 1u, laag risico).** Meldingen, Excel-datasetnaam en -kolommen, opmerkingen in het Nederlands. Geen hernoemingen van types. Kan meteen mee in v1.3.26.

> ✅ **Gedaan 31/10** (`feature/us99a-factuur-uit-teksten`): foutmeldingen ("Bestelbon niet gevonden", open/geannuleerde bestelbon), Excel-export (dataset, werkbladen, kolommen "Besteldatum" en "Bestelbon-id", status in gewone woorden, kolom "Type" weg), recht "Bestelbonnen maken en betalingen registreren", bestelbongegevens ("besteldatum", nummer als "voorlopig (offerte 412)"), mapkeuze "bestelbon-PDF". Interne exportsleutels (`factuurNummer`, `factuur-regels`) blijven tot fase B.

**Fase B — code hernoemen (± 3–4u).** Types, bestanden, mappen, members en tests met IDE-refactor (Rider: Rename + "rename file"), één commit per groep. De database blijft hier nog onveranderd: in `AppDbContext` met `ToTable("Facturen")` / `HasColumnName("FactuurId")` enz. vastzetten, zodat er **geen** schemawijziging is (drift-guard `ModelDriftTests` moet groen blijven zonder migratie).

> ✅ **Gedaan 31/10** (`feature/us99b-factuur-naar-bestelbon-code`): alle types, bestanden, de map `ViewModels/Bestelbon`, members, tests, Excel-sleutels (`bestelbonNummer`, `bestelbon-regels`, `bestelbonId`), GDPR-exportsleutel `Bestelbonnen` en `Permissie.Bestelbonnen` hernoemd (script, 87 bestanden). In `AppDbContext` vastgezet: `ToTable("Facturen")`, `ToTable("FactuurLijnen")`, `HasColumnName("FactuurNummer" / "FactuurDatum" / "FactuurId")`. `dotnet ef migrations has-pending-model-changes` geeft voor **SQLite én Npgsql** "no changes". Actuele docs (README, AUTH, GDPR, AUDIT, …) bijgewerkt; `docs/archief` en oude backlog blijven als geschiedenis.
>
> Nog "factu" in de code, bewust: de SQL in `SqliteSchemaPatcher`/`BestelbonSchemaUpgrade` en de mapping hierboven (verdwijnen in fase C), en de legacy-waarde `"Gefactureerd"` in `AppDbContext` (oude offertestatus in de data, moet gelezen kunnen blijven).
>
> Extra voor fase C: nieuwe audit-regels krijgen `EntiteitType = "Bestelbon"`/`"BestelbonLijn"` (typenaam), oude staan op `"Factuur"`/`"FactuurLijn"` → in de migratie mee omzetten (`UPDATE AuditLogs SET EntiteitType = …`), anders toont de filter in het auditlog beide.

**Fase C — database (± 2u, met back-up).** De `ToTable`/`HasColumnName`-mapping uit fase B weghalen en een migratie `HernoemFactuurNaarBestelbon` genereren (`.\Scripts\add-migration.ps1`). Nakijken dat EF **RenameTable/RenameColumn** genereert en geen Drop/Create (anders handmatig omzetten naar rename — anders gaan de gegevens verloren). Test: migratietest zoals `BetaalwijzeTests.Migratie_zet_oude_betaalwijzen_om` met bestelbon + lijnen + betaling vóór en na. Op SQLite maakt EF bij een rename van een kolom met index/FK de tabel opnieuw aan (rebuild); controleren dat data meekomt.

## Risico's

- **Gegevensverlies bij fase C** als EF een drop/create i.p.v. rename genereert → migratie handmatig nalezen, test met echte data-kopie, back-up vóór release.
- **PostgreSQL** (`quadrodb`): zelfde migratie in `QuadroApp.Migrations.Npgsql`; `xmin`-concurrency blijft werken na rename.
- **Merge-conflicten**: fase B raakt veel bestanden → plannen wanneer er geen andere branches open staan.
- `Permissie` en `ExcelExportDataset` worden niet in de database bewaard (nagekeken), dus hernoemen is veilig.

⏱ Schatting: 6–7u in totaal. Fase A kan los vóór de release van v1.3.26; B en C erna.
