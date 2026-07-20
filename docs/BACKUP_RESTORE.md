# QuadroApp — Backup & Herstel (US-34)

## Hoe backups werken

**SQLite (huidige modus):** de app maakt bij het opstarten automatisch één backup per dag via de veilige SQLite online-backup API. Bestandsnaam: `quadro-backup-JJJJMMDD.db`.

- Standaardlocatie: `%LOCALAPPDATA%\QuadroApp\Backups` (macOS: `~/Library/Application Support/QuadroApp/Backups`)
- Retentie: 30 dagen (ouder wordt automatisch verwijderd)
- Configureerbaar in `appsettings.json`:

```json
{ "Backup": { "Directory": "D:\\QuadroBackups", "RetentionDays": 30 } }
```

> **Belangrijk:** zet `Backup:Directory` op een andere schijf, NAS of gesynchroniseerde map (OneDrive/Dropbox) zodat backups een schijfcrash overleven. De standaardmap staat op dezelfde schijf als de database.

**PostgreSQL (na de migratie):** plan `Scripts\backup-postgres.ps1` dagelijks in via Windows Taakplanner op PC 1. Wachtwoord via omgevingsvariabele `QUADRO_DB_PASSWORD` of `pgpass.conf`.

## Herstelprocedure — SQLite

1. Sluit QuadroApp op alle PC's.
2. Ga naar `%LOCALAPPDATA%\QuadroApp\` en hernoem het huidige `quadro.db` naar `quadro.db.kapot` (niet verwijderen).
3. Kopieer de gewenste backup uit de backupmap naar `%LOCALAPPDATA%\QuadroApp\` en hernoem naar `quadro.db`.
4. Start QuadroApp — controleer klanten, offertes en facturen.
5. Klopt alles? Dan mag `quadro.db.kapot` weg.

## Herstelprocedure — PostgreSQL

1. Sluit QuadroApp op alle PC's.
2. Op PC 1:
   ```powershell
   pg_restore -h localhost -U quadro -d quadrodb --clean --if-exists "D:\QuadroBackups\quadrodb-backup-JJJJMMDD.dump"
   ```
3. Start QuadroApp en controleer de data.

## Test de herstelprocedure

Doe minstens één keer een proefherstel (stap 1–4 hierboven) op een moment dat het niet dringend is, zodat je zeker weet dat de procedure werkt en je ze onder druk niet voor het eerst uitvoert.
