# QuadroApp — Uitrol-runbook (cloud PostgreSQL)

> Klik-voor-klik draaiboek om QuadroApp op de cloud-database (Scaleway, REL-04c) te zetten en
> uit te rollen naar de klant-pc's. Volg de secties op volgorde. Referenties:
> `docs/backlog/REL04c_CloudPostgres_Plan.md`, `docs/GDPR.md`, `docs/BACKUP_RESTORE.md`, `docs/AUTH.md`.
>
> **Stand:** de cloud-verbinding is getest en werkt (TLS + login OK, schema automatisch gebouwd).
> Dit runbook dekt het afwerken en uitrollen.
>
> **Verbindingsgegevens (host/poort — niet geheim; wachtwoord NOOIT hier of in git):**
> `Host=212.47.241.9;Port=20900;Database=quadrodb;Username=quadro;Password=__SECRET__;SSL Mode=Require`

---

## Deel 1 — Server-side afwerken (eenmalig, in de Scaleway-console)

- [ ] **Rechten:** gebruiker `quadro` heeft **volledige rechten** op database `quadrodb` (gedaan tijdens de test).
- [ ] **Backups + PITR** staan aan; retentie ingesteld op **7–30 dagen**.
- [ ] **Encryptie-at-rest**: status bekeken en beslist (aan laten of aanvaard restrisico). Noteer de keuze in `docs/GDPR.md`.
- [ ] **Allowed IPs**: `0.0.0.0/0` (of verfijnd). Beveiliging leunt op het **sterke `quadro`-wachtwoord + TLS** — controleer dat het wachtwoord lang en willekeurig is.
- [ ] **DPA van Scaleway getekend** en Scaleway in het **verwerkingsregister** gezet (GDPR).

## Deel 2 — Data vullen (eenmalig, in de app op één pc)

- [ ] App gestart op de cloud-DB → schema + seed (afwerkingsgroepen G/P/D/O/R) automatisch aangemaakt.
- [ ] **Lijsten** geïmporteerd via de Excel-lijsten-import (import-preview → commit).
- [ ] **Afwerkingsopties** geïmporteerd via de afwerking-Excel-import.
- [ ] In de app gecontroleerd dat lijsten + afwerkingen zichtbaar en correct zijn.

## Deel 3 — Accounts & beveiliging (eenmalig, na eerste login)

- [ ] Ingelogd als `admin` / `quadro` → verplichte dialoog → **admin-wachtwoord gewijzigd**.
- [ ] Account voor **Veerle** (en eventueel **Kurt**) aangemaakt via Instellingen → Gebruikersbeheer, rol naar wens.
- [ ] Eerste login van Veerle getest (haar wachtwoord-wijzig-dialoog verschijnt).
- [ ] Bevestigd dat `QUADRO_SEED_DEMO` **niet** gezet is (geen demo-klanten in productie).

## Deel 4 — Uitrol naar elke klant-pc (herhaal per pc)

> Belangrijk: het schema staat al in de cloud. Een klant-pc hoeft **niets** aan de database te doen —
> alleen verbinden. Er is geen PC1/PC2, geen LAN-IP, geen firewallpoort meer.

> **Waar komt `appsettings.json`?** De app leest hem uit twee plekken (data-map wint):
> - **Windows:** naast de exe, óf in `%LOCALAPPDATA%\QuadroApp\`.
> - **macOS:** in `~/Library/Application Support/QuadroApp/` — **NIET** binnen de `.app`-bundle
>   (dat zou de Apple-handtekening breken). Zelfde map als het secret en de backups.

Per pc:

1. **QuadroApp installeren** (Velopack-installer, of de bestaande installatie updaten).
   Op macOS: de genotariseerde `osx-arm64`-build; bij eerste start Gatekeeper laten doorlaten.
2. **`appsettings.json`** neerzetten met exact deze regel als `Default` (host/poort zijn voor elke pc identiek):
   ```
   "Default": "Host=212.47.241.9;Port=20900;Database=quadrodb;Username=quadro;Password=__SECRET__;SSL Mode=Require"
   ```
   - **Windows:** naast de exe (of in `%LOCALAPPDATA%\QuadroApp\`).
   - **macOS:** in `~/Library/Application Support/QuadroApp/appsettings.json`.
3. **Secret zetten** (het cloud-DB-wachtwoord, eenmalig per account):
   - **Windows:** `.\Scripts\set-db-secret.ps1`
   - **macOS:** `bash Scripts/set-db-secret.sh` → schrijft naar `~/Library/Application Support/QuadroApp/db.secret`
   - Alternatief (beide): omgevingsvariabele `QUADRO_DB_PASSWORD`.
4. **App starten** → login-scherm verschijnt → inloggen met het eigen account.
5. **Smoke-test op deze pc:** home laadt, een offerte openen, planning openen, een bestelbon-PDF maken.

## Deel 5 — Multi-user & backup verifiëren

- [ ] **PC 1 en PC 2 tegelijk**: op beide een offerte/bestelbon maken; controleer dat er geen dubbele
      factuurnummers ontstaan en dat een gelijktijdige wijziging de nette
      "iemand anders heeft dit gewijzigd"-melding geeft (REL-03).
- [ ] **Proefherstel** van een Scaleway-backup uitgevoerd op een rustig moment (procedure bijwerken in
      `docs/BACKUP_RESTORE.md`, PostgreSQL-sectie), zodat je zeker weet dat restore werkt.

---

## Terugrol-plan (als er iets misgaat)

In `appsettings.json` staat onder de actieve cloud-regel een **rollback**-blok. Zet de cloud-regel in
commentaar en haal één van deze uit commentaar:

- **Lokale SQLite** (single-PC, noodwerk): `"Default": "Data Source=quadro.db"`
- **LAN-PostgreSQL** (oude 2-PC-opzet): `"Default": "Host=localhost;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__;SSL Mode=Disable"`

Let op: noodwerk op lokale SQLite maakt data aan die niet vanzelf terug samenvloeit met de cloud.

---

## Snelle probleemoplossing

| Symptoom | Waarschijnlijke oorzaak | Oplossing |
|---|---|---|
| Time-out / kan niet verbinden | Allowed IPs blokkeert deze pc | In Scaleway `0.0.0.0/0` (of het juiste publieke IP) toevoegen |
| `28P01 password authentication failed` | Verkeerd/niet-gezet secret | `.\Scripts\set-db-secret.ps1` opnieuw met het cloud-wachtwoord |
| `42501 permission denied for database` | `quadro` mist rechten op `quadrodb` | In de console `quadro` volledige rechten op `quadrodb` geven |
| SSL-fout | `SSL Mode` ontbreekt | Zorg dat `;SSL Mode=Require` in de connection string staat |
| Poort werkt niet | 5432 gebruikt i.p.v. de Scaleway-poort | Poort **20900** (uit de console) gebruiken |
