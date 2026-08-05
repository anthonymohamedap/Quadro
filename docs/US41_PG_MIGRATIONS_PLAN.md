# US-41 — PostgreSQL op echte EF-migraties (Optie C): implementatieplan

*Shovel-ready plan, opgesteld 2026-08-04. Geen codewijziging in dit document — dit is de
uitvoerbare gids. Voer de stappen uit op een Windows-dev-machine met `dotnet ef` én een
**wegwerp/dev-PostgreSQL** (niet meteen de live `quadrodb`).*

---

## 0. Doel & uitgangspunt

PostgreSQL initialiseert nu via `EnsureCreatedAsync` (geen migratiehistorie). We willen PG op
**`MigrateAsync`** met een provider-eigen migratieset, met behoud van:

- de **xmin**-optimistic-concurrency (US-38), nu geconfigureerd in `AppDbContext.OnModelCreating`
  onder `if (Database.IsNpgsql())`;
- de bestaande **SQLite**-migraties + `SqliteSchemaPatcher` (ongewijzigd);
- de eenmalige **data-import** (`Scripts/migrate_to_postgres.py`).

Versies: .NET 10, EF Core **9.0.9**, Npgsql.EntityFrameworkCore.PostgreSQL **9.0.4**.

---

## 1. Waarom eerst een projectopsplitsing (de circulaire referentie)

EF-migraties zijn **provider-specifiek** (DDL verschilt), dus één set kan niet schoon beide
providers bedienen. De aanbevolen oplossing is een **aparte migratie-assembly per provider**. Maar:

- Het migratieproject heeft het `AppDbContext`-type nodig → verwijst naar de context-assembly.
- De app moet bij opstart de migratie-assembly **laden** om `MigrateAsync` te draaien → moet ernaar
  verwijzen.

Als `AppDbContext` in het app-project (`QuadroApp`) zit, ontstaat een **cyclus**
(`QuadroApp → Migrations.Npgsql → QuadroApp`). Oplossing: verhuis de EF-laag naar een eigen library.

### Doel-projectstructuur

```
QuadroApp.Data                     ← AppDbContext + Model/DB + bestaande SQLite-migraties
        ▲                    ▲
        │                    │
QuadroApp.Migrations.Npgsql  │      ← alleen de PostgreSQL-migraties
        ▲                    │
        └──────── QuadroApp (app) ──┘   ← verwijst naar Data én naar Migrations.Npgsql
```

Geen cyclus: `QuadroApp.Data` verwijst naar niets van bovenstaande; beide andere verwijzen
naar `Data`; de app verwijst naar beide.

---

## 2. Stap A — EF-laag extraheren naar `QuadroApp.Data`

**Doel:** een nieuw class-library-project met de context, het model, de data-config en de
bestaande SQLite-migraties.

1. Maak `QuadroApp.Data/QuadroApp.Data.csproj` (net10.0 class library). Package-refs:
   `Microsoft.EntityFrameworkCore` 9.0.9, `Microsoft.EntityFrameworkCore.Sqlite` 9.0.9,
   `Microsoft.EntityFrameworkCore.Design` 9.0.9, `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4
   (Npgsql hier nodig omdat `OnModelCreating` `Database.IsNpgsql()` gebruikt).
2. **Verplaats** naar dit project (namespaces blijven `QuadroApp.Data` / `QuadroApp.Model.DB`):
   - `Data/` (o.a. `AppDbContext.cs`, `AppDbContextFactory.cs`, `SqliteSchemaPatcher.cs`,
     `DbExecutionExtensions.cs`);
   - `Model/` (alle entiteiten);
   - `Migrations/` (Baseline, admin, AddAuditLog, Us38ConcurrencyStockFactuur + `*.Designer.cs`
     + `AppDbContextModelSnapshot.cs`).
3. In `QuadroApp.csproj`: verwijder deze bestanden uit het app-project (ze zitten nu in Data) en
   voeg `<ProjectReference Include="..\QuadroApp.Data\QuadroApp.Data.csproj" />` toe.
4. Voeg beide projecten toe aan `Quadro.sln`
   (`dotnet sln add QuadroApp.Data/QuadroApp.Data.csproj`).
5. **Bouwen + volledige testsuite groen** (`.\verify.ps1`). Dit is puur structureel; het gedrag
   mag niet wijzigen. Los eventuele `using`/namespace-issues op (namespaces zelf veranderen niet).

> Tip: de SQLite-migraties blijven in `QuadroApp.Data` en houden dus hun `MigrationsAssembly` =
> `QuadroApp.Data`. Pas de runtime-`UseSqlite` daarop aan (zie stap D) als EF de migraties niet
> meer vindt.

---

## 3. Stap B — Project `QuadroApp.Migrations.Npgsql`

1. Maak `QuadroApp.Migrations.Npgsql/QuadroApp.Migrations.Npgsql.csproj` (net10.0 class library).
   Package-refs: EF Core 9.0.9, `Microsoft.EntityFrameworkCore.Design` 9.0.9,
   `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4.
   `<ProjectReference Include="..\QuadroApp.Data\QuadroApp.Data.csproj" />`.
2. `dotnet sln add QuadroApp.Migrations.Npgsql/QuadroApp.Migrations.Npgsql.csproj`.
3. Voorlopig leeg (de Baseline-migratie komt in stap E).

---

## 4. Stap C — Provider-bewuste design-time factory

`dotnet ef` gebruikt de `IDesignTimeDbContextFactory<AppDbContext>`. Vervang de bestaande
`AppDbContextFactory` (in `QuadroApp.Data`) door een provider-schakelaar via omgevingsvariabele,
zodat één factory beide providers bedient én de juiste `MigrationsAssembly` zet:

```csharp
public AppDbContext CreateDbContext(string[] args)
{
    var provider = Environment.GetEnvironmentVariable("EF_PROVIDER"); // "Npgsql" of leeg=Sqlite
    var b = new DbContextOptionsBuilder<AppDbContext>();

    if (string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
    {
        var cs = Environment.GetEnvironmentVariable("QUADRO_PG_DESIGN_CS")
                 ?? "Host=localhost;Port=5432;Database=quadro_migdev;Username=postgres;Password=postgres";
        b.UseNpgsql(cs, o => o.MigrationsAssembly("QuadroApp.Migrations.Npgsql"));
    }
    else
    {
        b.UseSqlite("Data Source=quadro.db",
                    o => o.MigrationsAssembly("QuadroApp.Data")); // sqlite-migraties blijven hier
    }
    return new AppDbContext(b.Options);
}
```

> Belangrijk: **exact één** `IDesignTimeDbContextFactory<AppDbContext>` in de hele solution.
> Verwijder een eventuele tweede.

---

## 5. Stap D — Runtime-wiring (`App.axaml.cs`)

In `ConfigureServices`, bij de PostgreSQL-tak, zet de MigrationsAssembly zodat de app de
PG-migraties vindt/laadt (de app verwijst naar `QuadroApp.Migrations.Npgsql`):

```csharp
options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.MigrationsAssembly("QuadroApp.Migrations.Npgsql");
    npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null); // US-43, ongewijzigd
});
```

SQLite-tak: als de migraties naar `QuadroApp.Data` verhuisd zijn, voeg
`o => o.MigrationsAssembly("QuadroApp.Data")` toe aan `UseSqlite` (indien EF ze niet meer vindt).

**Nog NIET** `InitializePostgresDatabaseAsync` omzetten — dat gebeurt pas in stap G, ná de Baseline.

---

## 6. Stap E — Genereer de Npgsql-Baseline (op de dev-PG)

Op Windows, met een **verse wegwerp-PG** draaiend:

```powershell
$env:EF_PROVIDER = "Npgsql"
$env:QUADRO_PG_DESIGN_CS = "Host=localhost;Port=5432;Database=quadro_migdev;Username=postgres;Password=postgres"

dotnet ef migrations add Baseline `
  --project QuadroApp.Migrations.Npgsql `
  --startup-project QuadroApp `
  --context AppDbContext
```

Dit genereert de PG-Baseline (Npgsql-DDL) in `QuadroApp.Migrations.Npgsql/`. **Controleer:**
- de kolomtypes zijn Npgsql-native (geen `Sqlite:Autoincrement`);
- de **xmin**-shadow-property verschijnt **niet** als echte kolom in de DDL (xmin is een
  systeemkolom; EF mag er geen `CREATE`-kolom voor genereren). Zie stap I.

Test toepassen op de verse dev-PG:

```powershell
dotnet ef database update --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp
```

Vergelijk het resultaat met wat `EnsureCreatedAsync` opleverde (zelfde tabellen/kolommen/indexen).

---

## 7. Stap F — Baseline-markering van een BESTAANDE PG-DB

Een DB die al via `EnsureCreated` is gemaakt, heeft de tabellen maar **geen**
`__EFMigrationsHistory`. `MigrateAsync` zou dan de Baseline opnieuw willen draaien → fout
("relation already exists"). Markeer de Baseline als *al toegepast* zonder de DDL te draaien:

**Methode 1 — idempotent script, handmatig gefilterd:**
```powershell
dotnet ef migrations script --idempotent `
  --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp -o baseline.sql
```
Op een bestaande DB alleen de **INSERT in `__EFMigrationsHistory`** uitvoeren (niet de CREATE's).

**Methode 2 — directe insert (eenvoudiger, expliciet):**
```sql
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) NOT NULL,
    "ProductVersion" varchar(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);
INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
VALUES ('<TIMESTAMP>_Baseline', '9.0.9')
ON CONFLICT DO NOTHING;
```
(`<TIMESTAMP>_Baseline` = de exacte MigrationId uit stap E.)

Daarna doet `MigrateAsync` niets meer voor de Baseline en past hij enkel **toekomstige** migraties toe.

> **Oefen dit eerst op de wegwerp-PG** (maak er één met EnsureCreated-schema, markeer, draai
> MigrateAsync, bevestig "geen wijziging"). Pas als dat klopt: op een **backup/kopie** van de live
> `quadrodb`, en pas dan op de echte.

---

## 8. Stap G — `InitializePostgresDatabaseAsync` → `MigrateAsync`

Vervang in `App.axaml.cs`:

```csharp
private static async Task InitializePostgresDatabaseAsync(AppDbContext db)
{
    // US-41: PG draait nu op echte EF-migraties. Bestaande (EnsureCreated-)DB moet éérst
    // gebaseline-markeerd zijn (zie US41-plan stap F), anders faalt MigrateAsync op bestaande tabellen.
    await db.Database.MigrateAsync();
    _logger.LogInformation("[DB] PostgreSQL-migraties toegepast.");
}
```

En werk de XML-docs bij (de "bekende beperking: geen migratiehistorie" is dan opgelost).

---

## 9. Stap H — CI-guard: geen pending model changes

Voorkom drift: een test/CI-stap die faalt als het model afwijkt van de migraties, op **beide**
providers.

- SQLite (in de testsuite):
  ```csharp
  [Fact]
  public void Sqlite_heeft_geen_pending_model_changes()
  {
      using var db = /* SQLite-context via DbFactoryBuilder */;
      Assert.False(db.Database.HasPendingModelChanges());
  }
  ```
- Npgsql: idem met een Npgsql-context (vereist een PG in CI), of via
  `dotnet ef migrations has-pending-model-changes --project QuadroApp.Migrations.Npgsql
  --startup-project QuadroApp` als CI-stap (met `EF_PROVIDER=Npgsql`).

---

## 10. Stap I — xmin-verificatie

De xmin-concurrency zit als **shadow-property** in `OnModelCreating` (`Database.IsNpgsql()` →
`Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsConcurrencyToken()`). Controleer
na stap E dat de gegenereerde Baseline **géén** `xmin`-kolom probeert te CREATE'en (het is een
systeemkolom). Zo niet, markeer de property met `.Metadata.SetColumnName("xmin")` +
`ValueGeneratedOnAddOrUpdate()` en negeer 'm in migraties via
`modelBuilder.Entity(...).Property("xmin").Metadata.SetIsRequired(false)`, of sluit 'm uit met een
`migrationBuilder`-review. Bevestig met een lost-update-test op de dev-PG (twee contexts, één
`DbUpdateConcurrencyException`).

---

## 11. Validatie-checklist (alles op de dev-PG)

- [ ] Solution bouwt; `.\verify.ps1` groen na de projectopsplitsing (stap A).
- [ ] `dotnet ef migrations add Baseline` levert Npgsql-DDL zonder `Sqlite:Autoincrement`.
- [ ] Verse PG via `database update` = zelfde schema als voorheen via EnsureCreated.
- [ ] Bestaande (EnsureCreated-)PG: na baseline-markering doet `MigrateAsync` niets (idempotent).
- [ ] xmin blijft werken: lost-update-test geeft `DbUpdateConcurrencyException`.
- [ ] `HasPendingModelChanges()` = false op beide providers.
- [ ] SQLite-pad (`SqliteSchemaPatcher` + migraties) ongewijzigd en groen.
- [ ] Pas ná groen op dev-PG: herhaal baseline-markering op een **kopie** van live `quadrodb`.

---

## 12. Rollback / veiligheid

- Alles gebeurt op branch `feature/us41-pg-ef-migrations`; niets raakt live tot merge.
- De **live `quadrodb`** wordt pas aangeraakt na volledige validatie op wegwerp-PG + een kopie.
- Maak vóór de baseline-markering op de echte DB een **backup** (pg_dump).
- Faalt iets: de app kan tijdelijk terug naar `EnsureCreatedAsync` (stap G omkeren) zonder dataverlies.

---

## 13. Volgorde in increments (elk apart getest)

1. **Stap A** — datalaag-extractie → `verify.ps1` groen. *(grootste structurele stap)*
2. **Stap B–D** — Npgsql-project + factory + runtime-wiring (retry blijft), init nog op EnsureCreated.
3. **Stap E** — Baseline genereren + valideren op verse dev-PG.
4. **Stap F–G** — baseline-markering-procedure + omzetten naar `MigrateAsync`.
5. **Stap H–I** — CI-guard + xmin-verificatie.

Elke increment: commit, `verify.ps1`, en (waar PG nodig is) een smoketest op de dev-PG.
