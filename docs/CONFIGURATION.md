# QuadroApp — Configuratie & Secrets (US-33)

## Omgevingen

| Omgeving | Database | Config |
|---|---|---|
| Lokaal (standaard) | SQLite in `%LOCALAPPDATA%\QuadroApp\quadro.db` (macOS: `~/Library/Application Support/QuadroApp/`) | Geen config nodig — werkt out-of-the-box |
| Gedeeld (PC 1 + PC 2) | PostgreSQL op PC 1 | `appsettings.json` naast de exe + secret per PC |

## Connection string resolutie (volgorde)

1. Omgevingsvariabele `QUADRO_CONNECTION_STRING` (volledige string)
2. `appsettings.json` → `ConnectionStrings:Default`
3. Fallback: lokale SQLite

## Wachtwoord resolutie (alleen PostgreSQL)

Het wachtwoord staat **nooit** als platte tekst in `appsettings.json`. Gebruik de placeholder:

```json
{ "ConnectionStrings": { "Default": "Host=192.168.1.X;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__" } }
```

De placeholder wordt bij het opstarten vervangen, in deze volgorde:

1. Omgevingsvariabele `QUADRO_DB_PASSWORD`
2. Secret-bestand `db.secret` in de app-datamap
   - **Windows:** DPAPI-versleuteld (alleen leesbaar door het Windows-account dat het aanmaakte)
   - **macOS:** bestand met 600-permissies (alleen eigenaar)

## Secret instellen (eenmalig per PC, per gebruikersaccount)

**Windows:**
```powershell
.\Scripts\set-db-secret.ps1
```

**macOS:**
```bash
bash Scripts/set-db-secret.sh
```

## Logging (US-31)

Logs staan in `%LOCALAPPDATA%\QuadroApp\logs\quadro-JJJJMMDD.log` (macOS: `~/Library/Application Support/QuadroApp/logs/`), 14 dagen retentie. Niveau instellen in `appsettings.json`:

```json
{ "Logging": { "MinimumLevel": "Debug" } }
```

Geldige niveaus: `Verbose`, `Debug`, `Information` (standaard), `Warning`, `Error`. Onafgevangen crashes staan zowel in het log als in `crash.log`.

## Tijd & tijdzones (REL-01)

Voor een correcte werking op een gedeelde (mogelijk UTC-)PostgreSQL-server geldt:

- **Gebeurtenis-tijdstippen** (wanneer iets gebeurde) worden in **UTC** opgeslagen: audit-log,
  laatste login, account-aanmaakdatum, lijst-`LaatsteUpdate`, GDPR-archivering. De UI toont ze in
  lokale tijd via `UtcToLocalConverter`.
- **Kalenderdatums** (een dag in de agenda) blijven **lokaal**: offerte-/factuur-/afhaal-/plandatum,
  bestelbon-datums, de GDPR-retentie-cutoff (die vergelijkt met de lokale offertedatum).

Vuistregel bij nieuwe code: een *moment in de tijd* → `DateTime.UtcNow`; een *dag op de kalender*
die een mens invoert/leest → `DateTime.Today` (lokaal).

## Probleemoplossing

- App valt terug op SQLite terwijl je Postgres verwacht → controleer of `appsettings.json` naast de exe staat en geldige JSON is.
- "password authentication failed" → secret ontbreekt of is fout; check `crash.log` (melding "DB-wachtwoord niet gevonden") en draai het set-db-secret script opnieuw.
- Secret werkt niet na Windows-accountwissel → DPAPI is per account; draai het script onder het account dat de app start.
