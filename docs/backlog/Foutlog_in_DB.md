# Crashmeldingen in de database

**Status:** klaar. De PR wacht op akkoord van Anthony.

## Doel

Crashes van de app worden ook in de database bewaard, zodat de beheerder ze op afstand kan opvolgen.
Veerle hoeft niets meer door te sturen: de fout staat in de tabel `FoutLogs` en is zichtbaar in de app.

## Wat gebeurt er bij een fout

1. **Eerst crash.log.** De fout wordt meteen (synchroon) in `crash.log` geschreven, zoals voorheen. Er komt nu
   een regel `Hash: …` bij, direct na de kopregel van het blok. Dit bestand blijft altijd het vangnet, ook als de
   database niet bereikbaar is.
2. **Dan de database.** Op de achtergrond wordt het record in `FoutLogs` opgeslagen, na filtering (zie hieronder).
   De schrijfactie heeft een time-out van 5 seconden. Lukt dat niet, dan gaat de app gewoon door.
3. **Stormbescherming.** Dezelfde fout (zelfde hash) gaat hooguit één keer per minuut naar de database, en er loopt
   nooit meer dan één databaseschrijfactie tegelijk. Overgeslagen fouten staan wel in crash.log en komen bij de
   volgende import alsnog in de database.

## Import van crash.log

Na de eerste login in een app-run gebeurt dit op de achtergrond, dus het opstarten en inloggen vertraagt niet:

- De app leest de laatste 2 MB van crash.log.
- Enkel blokken van de laatste 180 dagen worden meegenomen.
- Blokken die al in de tabel staan (zelfde hash) worden overgeslagen. Dubbels binnen het bestand komen maar één keer erin.
- De bron wordt `Geïmporteerd`. Stond de oorspronkelijke bron in crash.log, dan wordt die tussen haakjes erbij gezet,
  bv. `Geïmporteerd (UI-thread)`.
- Elke pc importeert zijn eigen crash.log. crash.log wordt nooit gewijzigd of gewist.

## Opruimen

Direct na de import worden fouten verwijderd die **bekeken** zijn en **ouder dan 180 dagen**.
Niet-bekeken fouten blijven altijd staan.

## Filter op gevoelige gegevens

Voordat een fout in de database komt, worden deze gegevens vervangen (crash.log blijft ruw):

| In de fout | Wordt |
|---|---|
| wachtwoorden (`Password=…`, `wachtwoord: …`) | `***` |
| gebruikersnamen (`username=…`, `user id=…`) | `***` |
| e-mailadressen | `[e-mail]` |
| IBAN | `[iban]` |
| telefoonnummers | `[tel]` |
| bedragen (`€ 12,50`, `12,50 EUR`, `12.50`) | `[bedrag]` |

Stackregels blijven leesbaar. Te lange velden worden ingekort (zie de tabel hieronder).

## Scherm: Instellingen → Foutmeldingen

- Enkel zichtbaar voor de beheerder (recht `AuditInzien`, enkel Admin). Een medewerker ziet de knop niet en de
  service weigert de gegevens ook.
- Opent in een eigen venster. Toont maximaal 500 fouten, nieuwste eerst.
- Filter **enkel niet-bekeken**.
- Klik op een fout voor de details: tijdstip, bron, computer, gebruiker, app-versie, scherm, laatste klikken en de stacktrace.
- **Kopiëren** zet de details op het klembord.
- **Markeer als bekeken**. Dit is eenmalig; er is geen knop om het terug te zetten.

## Tabel FoutLogs

| Veld | Inhoud | Max. lengte |
|---|---|---|
| `Id` | sleutel | |
| `Tijdstip` | moment van de fout, in UTC (op de seconde) | |
| `Bron` | `UnobservedTask`, `Achtergrondthread`, `UI-thread`, `Opstarten` of `Geïmporteerd (…)` | 60 |
| `Melding` | korte foutmelding | 1000 |
| `ExceptionType` | type van de fout | 300 |
| `Stacktrace` | volledige `ex.ToString()` | 16000 |
| `Scherm` | scherm dat open stond | 200 |
| `LaatsteKlikken` | laatste klikken, één per regel | 4000 |
| `AppVersie` | versie van de app | 50 |
| `Computernaam` | naam van de pc | 100 |
| `Gebruiker` | login van de ingelogde gebruiker, als bekend | 50 |
| `Hash` | SHA-256 (uniek), op tijdstip + type + eerste 3 stackregels + computer | 64 |
| `IsBekeken` | ja/nee | |

Index: uniek op `Hash` (geen dubbels), en op `Tijdstip`.

## Migratie

- SQLite: `20261109142343_FoutLogs`
- Npgsql: `20261109142350_FoutLogs`

Beide migraties hebben dezelfde ID op `main` en `dev`.

## Testen

- `FoutLogFilterTests`: filter op gevoelige gegevens en inkorten.
- `FoutRegistratieTests`: crash.log eerst, de databasekant faalt nooit.
- `CrashLogImporterTests`: inlezen van crash.log (oud en nieuw formaat), geen dubbels, crash.log blijft ongewijzigd.
- `FoutLogServiceTests`: rechten, volgorde, filter, opruimen en kopiëren.
- `FoutLogMigratieTests`: de migratie van de tabel.
