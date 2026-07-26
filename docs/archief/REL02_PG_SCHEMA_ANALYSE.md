# REL-02 — PostgreSQL-schemastrategie: analyse & voorstel

*Analyse 26 juli 2026 · read-only, geen codewijziging.*

## Bevindingen

1. **Migraties zijn SQLite-specifiek.** De 4 migraties (Baseline, admin, AddAuditLog,
   Us38ConcurrencyStockFactuur) zijn met de SQLite-provider gegenereerd: `Sqlite:Autoincrement`-
   annotaties, SQLite-kolomtypes. `Data/AppDbContextFactory.cs` hardcodeert `UseSqlite`, dus
   `dotnet ef migrations add` levert altijd SQLite-smaak.
2. **PostgreSQL draait op `EnsureCreatedAsync`** (`App.axaml.cs` → `InitializePostgresDatabaseAsync`) —
   geen migratiehistorie. `Scripts/migrate_to_postgres.py` gaat ervan uit dat de app één keer gedraaid
   heeft (schema bestaat via EnsureCreated) en kopieert dan de data rij-voor-rij + reset sequences.
3. **RowVersion-concurrency is provider-specifiek en nu enkel correct op SQLite.** `[Timestamp] byte[]`
   werkt op SQLite via de patcher, maar PostgreSQL onderhoudt een `bytea`-kolom **niet** automatisch.
   Npgsql gebruikt `xmin` (`UseXminAsConcurrencyToken()`). → **US-38 lost-update-detectie werkt
   waarschijnlijk niet op PostgreSQL** met de huidige mapping. Dit is de kritische must-fix.

## Drie richtingen

### Optie A — Pragmatisch (aanbevolen voor DEZE release)
- **Behoud `EnsureCreatedAsync` voor PostgreSQL** (verse install + eenmalige data-import; incrementele
  migraties zijn voor deze release niet nodig).
- **Fix de concurrency-mapping per provider** in `OnModelCreating`: op Npgsql
  `entity.UseXminAsConcurrencyToken()`, op SQLite de bestaande `byte[]`-aanpak. → US-38 werkt écht op
  beide providers.
- **Formaliseer + documenteer** dat PG-schema via EnsureCreated komt en dat toekomstige
  schemawijzigingen een verse regeneratie of expliciete patch vereisen tot Optie C is ingevoerd.
- Inspanning: **M**. Risico: laag. Deblokkeert de release en repareert het echte concurrency-gat.

### Optie B — Alles via EF-migraties, één set
- EF-migraties zijn inherent provider-specifiek (DDL verschilt), dus één set kan niet schoon beide
  providers bedienen zonder conditionals. Niet aangeraden.

### Optie C — Correct langetermijn: EF-migraties óók voor PostgreSQL
- Tweede design-time configuratie/migratie-assembly voor Npgsql, `MigrateAsync` op PG, `xmin`-
  concurrency, `EnsureCreated` uitfaseren. Geen drift meer.
- Inspanning: **L**. Moeilijk volledig te valideren zonder draaiende PostgreSQL. Beste als aparte
  post-release story.

## Voorstel

**Optie A nu** (release-blocker: de xmin-concurrency-fix), **Optie C als post-release backlog-item.**
De must-fix is de RowVersion→xmin-mapping; zonder die faalt US-38 op PostgreSQL en is 2-PC niet veilig.

## Openstaande beslissing
Welke optie? Bij A implementeer ik: per-provider concurrency-config + tests (voor zover zonder
draaiende PG mogelijk) + documentatie + een post-release story voor C.
