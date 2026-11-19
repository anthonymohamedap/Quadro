# US-106 — Afvinklijst per inlijsting in productie (later)

Stand 02/11/2026. Idee uit de eerste schets van het verkoopdossier (stap 3 In productie). **Niet in te plannen
zonder akkoord van Anthony**: het is een nieuwe functie, geen herschikking van wat er al is (grondregel van het
verkoopdossier, zie `US-87-95_Verkoopdossier_Plan.md`).

**Als** medewerker in het atelier **wil ik** per inlijsting afvinken wat klaar is en wie het deed,
**zodat** iedereen ziet hoe ver een werkbon staat en wie je moet aanspreken bij een vraag.

## Hoe het eruit zou zien

Per inlijsting in stap 3 (In productie) van het dossier:

- ☑ Lijst besteld · Nielsen · 22/10 · VK
- ☑ Lijst binnen · 27/10 · VK
- ☑ Ingelijst · 30/10 · KB
- ☐ Nagekeken

Ook op de werkbon-PDF (US-94) als afvinkvakjes met een vakje voor initialen.

## Acceptatiecriteria (voorstel)

1. Per inlijsting (werktaak) vier punten: lijst besteld, lijst binnen, ingelijst, nagekeken.
2. "Lijst besteld" en "Lijst binnen" volgen vanzelf uit de leveranciersbestelling (besteld / ontvangen); bij een
   standaardkader of lijst op voorraad: "uit voorraad".
3. "Ingelijst" en "Nagekeken" vink je zelf af; de app bewaart datum en initialen van de ingelogde gebruiker.
4. Afvinken kan ongedaan gemaakt worden (met audit-log).
5. Zijn alle inlijstingen nagekeken, dan stelt de app voor om de verkoop op "Klaar om af te halen" te zetten
   (geen automatische overgang zonder klik).

## Technisch

- Datamodel: op `WerkTaak` velden zoals `IngelijstOp`/`IngelijstDoor`, `NagekekenOp`/`NagekekenDoor` (of een aparte
  tabel met stappen). EF-migratie voor SQLite én Npgsql.
- Vandaag bestaat `MarkeerTaakAlsVoltooidAsync` (zet `Resource = "Voltooid"`), maar geen knop roept het aan:
  bij deze story nakijken of dat hergebruikt of vervangen wordt.
- Initialen uit de ingelogde gebruiker (`IAuthService`).

## Open vragen

1. Zijn deze vier punten de juiste? (Ook "gezaagd", "glas gesneden"…?)
2. Moet "nagekeken" door een andere persoon gebeuren dan "ingelijst"?
3. Ook tonen in de planning / weeklijst?

⏱ Schatting: 4–6u.
