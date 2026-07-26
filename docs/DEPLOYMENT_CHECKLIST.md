# QuadroApp — Deployment checklist (2-PC / PostgreSQL)

> Afvinkbare checklist voor het uitrollen van QuadroApp op **PC 1 + PC 2** met een gedeelde
> PostgreSQL-database. Volg de secties op volgorde. Referenties: `docs/CONFIGURATION.md`,
> `docs/BACKUP_RESTORE.md`, `docs/AUTH.md`, `docs/backlog/RELEASE_2PC_PLAN.md`.
>
> **Regel:** een stap pas afvinken als je ze écht hebt uitgevoerd én gecontroleerd.

---

## A. Vóór de release (code — moet gemerged op `main`)

- [ ] REL-01 (DateTime → UtcNow) gemerged en CI groen.
- [ ] REL-02 (PostgreSQL xmin-concurrency) gemerged en CI groen.
- [ ] REL-03 (concurrency-conflict-UX) gemerged en CI groen.
- [ ] US-26 (meerprijs niet afdrukken) gemerged (klantwens; US-27/US-28 waren al gedaan).
- [ ] `.\verify.ps1` lokaal groen op `main`.
- [ ] Versie gebumpt in `QuadroApp.csproj` (`<Version>`), bv. 1.0.5 → **1.1.0**.

## B. Release bouwen

- [ ] `main` getagd → auto-tag workflow draait → release-workflow bouwt win-x64 + osx-arm64
      (Velopack, macOS-notarisatie).
- [ ] Release-artefacten verschijnen op GitHub Releases (Windows + macOS).
- [ ] **Eerst op jouw eigen PC:** installeer/upgrade via Velopack met je **echte** SQLite-database.
      Controleer dat de upgrade-flow draait (healer → Baseline → alle migraties) en dat klanten,
      offertes en facturen intact zijn. (Vangnet: de dagelijkse backup uit US-34.)

## C. PostgreSQL-server opzetten (PC 1)

- [ ] PostgreSQL geïnstalleerd op PC 1.
- [ ] Database `quadrodb` + gebruiker `quadro` aangemaakt (met wachtwoord).
- [ ] Firewall PC 1: poort **5432** open voor het lokale netwerk.
- [ ] Bepaal PC 1's LAN-IP (bv. `ipconfig` → 192.168.1.X) voor de PC 2-config.

## D. Data opzetten (GEEN migratie nodig)

> Bevestigd 26 juli 2026: er is géén bestaande productiedata om te migreren. De enige data zijn de
> afwerkingsgroepen (referentiedata, worden automatisch aangemaakt) en de lijsten + afwerkingsopties
> (komen via Excel-import). Klanten/offertes/facturen worden live aangemaakt.
> **`Scripts/migrate_to_postgres.py` is dus NIET nodig voor deze uitrol.**

- [ ] Start QuadroApp één keer met de PostgreSQL-connection string zodat het schema wordt aangemaakt
      (`EnsureCreatedAsync`) en de 5 afwerkingsgroepen (G/P/D/O/R) automatisch worden geseed
      (`DbSeeder.SeedReferenceData`). *(Zie sectie E voor de connection string.)*
- [ ] **Lijsten** importeren via de Excel-lijsten-import (unified import-preview → commit).
- [ ] **Afwerkingsopties** importeren via de afwerking-Excel-import.
- [ ] Controleer in de app dat lijsten en afwerkingen zichtbaar en correct zijn.

## E. Configuratie & secrets (PC 1 én PC 2)

- [ ] `appsettings.json` naast de exe op **PC 1** met de PostgreSQL-string en `Password=__SECRET__`:
      `Host=localhost;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__`
- [ ] `appsettings.json` naast de exe op **PC 2** met PC 1's IP:
      `Host=192.168.1.X;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__`
- [ ] Op **elke** PC eenmalig `.\Scripts\set-db-secret.ps1` gedraaid (DPAPI-secret, per Windows-account).
- [ ] Gecontroleerd dat `appsettings.json` **niet** in git staat (het is gitignored; enkel
      `appsettings.example.json` is getrackt).
- [ ] Bevestigd dat `QUADRO_SEED_DEMO` **niet** gezet is (geen demo-klanten in productie).

## F. Backups

- [ ] `Backup:Directory` in `appsettings.json` verwijst naar een **tweede schijf / NAS / OneDrive**
      (niet dezelfde schijf als de database).
- [ ] Dagelijkse `pg_dump` ingepland op PC 1 via Windows Taakplanner met `Scripts/backup-postgres.ps1`
      (wachtwoord via `QUADRO_DB_PASSWORD` of `pgpass.conf`).
- [ ] **Proefherstel uitgevoerd** volgens `docs/BACKUP_RESTORE.md` (PostgreSQL-sectie) op een moment
      dat het niet dringend is — zodat je zeker weet dat restore werkt.

## G. Accounts & beveiliging (na eerste start)

- [ ] Ingelogd als `admin` / `quadro` → verplichte dialoog verscheen → **admin-wachtwoord gewijzigd**.
- [ ] Account voor **Veerle** aangemaakt via Instellingen → Gebruikersbeheer (rol naar wens).
- [ ] Veerle's eerste login getest (haar wachtwoord-wijzig dialoog verschijnt).
- [ ] Rechten gecontroleerd: een Medewerker ziet géén Gebruikersbeheer-knop en kan geen prijzen/
      leveranciers/lijsten wijzigen.

## H. Smoke-test (op beide PC's)

- [ ] App start, login werkt, home laadt.
- [ ] Offertes: lijst laadt, offerte openen werkt.
- [ ] Planning: kalender opent, een WerkTaak plannen werkt.
- [ ] Werkbon: aanmaken vanuit goedgekeurde offerte werkt.
- [ ] Bestelbon/factuur: aanmaken + PDF-preview werkt (geen "Meerprijs"-regel, korting op incl. btw).
- [ ] Import: unified import-preview opent en commit werkt.
- [ ] **Multi-user:** PC 1 en PC 2 tegelijk — twee offertes/facturen maken; controleer dat er geen
      dubbele factuurnummers ontstaan en dat een gelijktijdige wijziging de nette
      "iemand anders heeft dit gewijzigd"-melding geeft (REL-03).

## I. macOS-specifiek (Veerle)

- [ ] macOS-build geïnstalleerd (osx-arm64, genotariseerd).
- [ ] Login-overlay, backup-map (`~/Library/Application Support/QuadroApp/`) en dagelijkse workflow
      werken.
- [ ] Bestelbon-PDF drukt correct af.

---

## Terugrol-plan (als er iets misgaat)

- SQLite blijft ongewijzigd bruikbaar: zet de connection string terug naar
  `Data Source=quadro.db` (of verwijder `appsettings.json`) om lokaal single-PC te draaien.
- De laatste backup (SQLite in `%LOCALAPPDATA%\QuadroApp\Backups`, PostgreSQL via `pg_dump`) is het
  vangnet; herstelprocedure staat in `docs/BACKUP_RESTORE.md`.
