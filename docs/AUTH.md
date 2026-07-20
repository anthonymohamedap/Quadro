# QuadroApp — Gebruikers, rollen & vergrendeling (US-32)

## Hoe het werkt

Bij het opstarten toont QuadroApp een aanmeldscherm. Na 15 minuten zonder muis- of toetsenbordactiviteit vergrendelt de app automatisch en moet er opnieuw aangemeld worden.

## Eerste keer

Bij de allereerste start (of een lege database) wordt automatisch één beheerder aangemaakt:

- Gebruikersnaam: `admin`
- Wachtwoord: `quadro`

**Wijzig dit wachtwoord meteen na de eerste login.** De app toont hiervoor een waarschuwing zolang dat niet gebeurd is.

## Rollen

| Actie | Admin | Medewerker |
|---|---|---|
| Offertes, werkbonnen, planning | ✅ | ✅ |
| Factureren / bestelbonnen | ✅ | ✅ |
| Prijzen wijzigen (afwerkingen) | ✅ | ❌ |
| Leverancier verwijderen | ✅ | ❌ |
| Lijst archiveren/verwijderen | ✅ | ❌ |
| Gebruikers beheren | ✅ | ❌ |

## Techniek

- Wachtwoorden: PBKDF2-SHA256, 210.000 iteraties, unieke salt per wachtwoord (`PasswordHasher`).
- Login-fouten zijn bewust generiek ("gebruikersnaam of wachtwoord onjuist") zodat accountnamen niet te raden zijn.
- `AuthService` (singleton) houdt de ingelogde gebruiker bij; `HeeftPermissie`/`VereisPermissie` voor autorisatie.
- Auto-lock: timer in `MainWindowViewModel`, activiteit gereset via MainWindow events.
- Gebruikersbeheer-UI (accounts aanmaken/deactiveren) volgt in een aparte story; tot dan kunnen accounts via de database beheerd worden.
