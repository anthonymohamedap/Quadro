# QuadroApp — afspraken voor Claude

Desktop-app (Avalonia 11, .NET 10, EF Core 9) voor inlijstatelier Quadro (Veerle en Kurt, Aarschot). Zie README.md voor stack en mappen. Praat Nederlands met de gebruiker (Anthony); hij leest vaak op zijn gsm.

## Na elke implementatie: schermen tonen (verplicht)

Na **elke** wijziging die iets op het scherm verandert (view, ViewModel, teksten, knoppen, PDF), laat je Anthony **visueel** zien wat er veranderd is, nog vóór je de PR voorstelt:

1. Render de betrokken schermen met `Tools/SchermAfbeeldingen` (buiten beeld, met testgegevens):
   ```
   dotnet run --project Tools/SchermAfbeeldingen -- <uitvoermap> [scenario ...]
   ```
   Uitvoermap: de scratchpad van de sessie (nooit in de repo committen). Zonder scenario's worden ze allemaal gerenderd.
2. Bestaat er nog geen scenario voor het scherm, **voeg er een toe** in `Tools/SchermAfbeeldingen/Program.cs` (`Scenarios`), met testgegevens in `TestGegevens` die de situatie tonen (bv. concept / bevestigd / afgerond). Commit dat scenario mee.
3. Bekijk de afbeeldingen zelf eerst. Klopt er iets niet (knop die aan staat, verkeerd bedrag, overlap), los het op en render opnieuw. Meld wat je zo gevonden hebt.
4. Stuur de afbeeldingen naar Anthony (SendUserFile, `display: "render"`), met per afbeelding in één zin wat hij moet zien. Liefst **voor en na** als het een bestaand scherm is (render eerst op `main`, dan op de branch).
5. PDF's (bestelbon, weeklijst, betalingsoverzicht): maak de PDF met de exporter en zet de eerste pagina om naar PNG (`pdftoppm -png -r 70 -f 1 -l 1`).

Bekende valkuilen bij het renderen:
- De app wacht op de UI-thread: `Program.cs` verwerkt de dispatcher zelf (`Dispatcher.UIThread.RunJobs()` in een lus). Niet weghalen, anders blijft het hangen.
- `OfferteViewModel.LoadCatalogAsync` heeft de vijf afwerkingsgroepen (G, P, D, O, R) nodig in de database.
- Bedragen tonen met nl-BE (komma); de tool zet de cultuur zelf.
- In de cloudomgeving is er geen map Documenten (`MyDocuments` is leeg): exporters die daarheen schrijven falen daar, zet `HOME` op een map in de scratchpad.

## Werkwijze

- Eén story per branch: `feature/usNN-korte-naam` (docs: `docs/…`, fixes los van een story: `fix/…`). Nooit rechtstreeks op `main`.
- Commits in het Nederlands, conventional: `feat(scope): … (US-NN)`, `fix(scope): …`, `docs(backlog): …`.
- Stories en plannen staan in `docs/backlog/` (bv. `Sprint_2026-11_US-79-86.md`, `US-87-95_Verkoopdossier_Plan.md`). Nieuwe story = nieuw bestand of sectie daar.
- Vóór push: `dotnet build Quadro.sln` en `dotnet test WorkflowService.Tests/WorkflowService.Tests.csproj` groen, en `dotnet list Quadro.sln package --vulnerable --include-transitive` zonder kwetsbare pakketten (CI faalt anders).
- Databasewijziging: altijd een EF-migratie voor **SQLite én Npgsql** via `.\Scripts\add-migration.ps1 <Naam>` (of de twee `dotnet ef migrations add`-commando's uit dat script). Bij een pure datamigratie herschikt EF soms de snapshot zonder echte wijziging: die herordening terugdraaien. Datamigratie altijd met een migratietest (zie `BetaalwijzeTests`).

## Woorden (afgesproken met Veerle)

- **Offerte**: de prijsberekening, met offertenummer (412).
- **Bestelbon**: wat de klant krijgt; vóór het akkoord *voorlopig* (geen volgnummer), daarna 2026-19. Nooit "factuur" op het scherm (US-99).
- **Werkbon**: de bon voor het atelier; menu en scherm heten "Werkbonnen".
- **Bestelling**: enkel de stap "klant gaat akkoord" ("Bevestigen als bestelling").
- Betaalwijzen: Bancontact, Cash, Credit card, Overschrijving.
- Statussen op het scherm altijd via `StatusLabels` / `StatusLabelConverter`, nooit de enumnaam.
