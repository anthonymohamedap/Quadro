# QuadroApp — UI-Optimalisatieplan (enterprise frontend)

> **Gebruik:** open een nieuwe chat en zeg: *"Voer fase X uit volgens docs/backlog/UI_OPTIMIZATION_PLAN.md"*.
> Eén fase per sessie. Elke fase eindigt met `.\verify.ps1` groen + visuele check via de checklist onderaan.
> Werkwijze: feature-branch per fase (`feature/ui-fase-0`, `feature/ui-fase-1`, ...), één commit per scherm.

## Doel

Alle 27 views professioneel en robuust maken: geen afgesneden tekst, geen overlappende knoppen/componenten, consistent op elke schermresolutie (klein Windows-scherm t.e.m. macOS Retina), één visuele taal.

## Vastgestelde problemen (codebase-scan 20 juli 2026)

1. **Vaste afmetingen overal** — 20+ views bevatten hardcoded `Width`/`Height` op panelen en vensters (o.a. ImportPreviewView 1200×760, BulkLijstenWindow 1100×760). Op kleinere schermen valt content buiten beeld; MainWindow (1200×820) heeft geen `MinWidth`/`MinHeight`.
2. **Geen text-overflow-bescherming** — 6 views hebben géén enkele `TextTrimming`/`TextWrapping` (KlantDetailView, LijstDialog, LijstenWindow, BulkLijstenWindow, beide ImportPreview-windows); in de rest is het ad hoc. Lange klantnamen/omschrijvingen lopen uit hun container.
3. **Kleuren hardcoded per view** — 200+ hex-kleuren verspreid over de views (34× `#6B7280`, 31× `#E5E7EB`, 28× `#F5C242`, ...) i.p.v. theme-resources. Inconsistenties en niet centraal aanpasbaar.
4. **Typografie hardcoded** — 250+ losse `FontSize=`-attributen (ArchiefView alleen al 45). Geen schaal, geen hiërarchie.
5. **Min-maten vrijwel afwezig** — slechts 10 bestanden gebruiken `MinWidth`/`MinHeight`; krimpende vensters laten componenten over elkaar schuiven.
6. **Scroll ontbreekt op volle schermen** — o.a. FacturenView heeft geen ScrollViewer; content clippt bij kleine vensters.

## Enterprise-regels (gelden voor élke wijziging)

- **Tekst:** elke `TextBlock` in een begrensde container krijgt `TextTrimming="CharacterEllipsis"` (+ `ToolTip.Tip` met de volledige tekst) óf bewust `TextWrapping="Wrap"`. Nooit kale tekst in een vaste breedte.
- **Layout:** Grid met star-sizing (`*`) + `MinWidth` in plaats van vaste breedtes; vaste pixelbreedtes alleen voor iconen/badges. Geen negatieve margins om overlap te "fixen".
- **Vensters:** alle Windows `CanResize="True"` met zinvolle `MinWidth`/`MinHeight`; dialogen `SizeToContent="Height"` waar passend; MainWindow krijgt `MinWidth="1024" MinHeight="700"`.
- **DataGrids/tabellen:** kolommen met `*`-breedtes + `MinWidth`; nooit alle kolommen vast.
- **Kleuren:** uitsluitend `{DynamicResource ...}` uit `Styles/QuadroTheme.axaml`. Geen nieuwe hex-codes in views.
- **Typografie:** uitsluitend via style-classes (`h1`, `h2`, `body`, `caption`, `label`) uit het theme. Geen losse `FontSize=` in views.
- **Spacing:** schaal 4/8/12/16/24/32; consistente `Margin`/`Spacing` per patroon (formulierrij, kaart, sectie).
- **Scroll:** elk hoofdscherm wikkelt zijn content in een `ScrollViewer` (verticaal) tenzij de layout aantoonbaar altijd past.
- **Per scherm:** aparte commit `ui(<scherm>): ...`; na elke fase `verify.ps1` + visuele checklist.
- **Geen functionele wijzigingen** — alleen layout/stijl. Bindings en commands blijven onaangeroerd.

## Fases

### Fase 0 — Fundament (design tokens) · ~½ dag
`Styles/QuadroTheme.axaml` uitbreiden tot single source of truth:
- Kleurtokens: `BrandYellow` (#F5C242), `BrandDark` (#444A50), `TextPrimary`, `TextSecondary` (#6B7280), `BorderSubtle` (#E5E7EB), `SurfaceMuted` (#F5F5F5/#F9FAFB), `InfoBg` (#F0F6FF/#BFDBFE), semantisch: `Danger`, `Success`, `Warning`.
- Typografie-classes: `h1` (22 SemiBold), `h2` (17 SemiBold), `body` (13), `caption` (11, TextSecondary), `label` (12 Medium).
- Basisstijlen: `TextBlock` default `TextTrimming` binnen `DataGridCell`; herbruikbare `Card`-, `FormRow`- en `PageSection`-stijlen.
- MainWindow: `MinWidth`/`MinHeight`.
**Klaar wanneer:** app start identiek qua uiterlijk (tokens = huidige waarden), verify groen.

### Fase 1 — Kernschermen (dagelijks gebruik) · ~1–1,5 dag
`OfferteView` (grootste scherm, 1540 regels), `WerkBonLijstView`, `FacturenView` (+ ScrollViewer!), `PlanningCalendarWindow`, `HomeView`.
Per scherm: vaste breedtes → star-sizing, trimming/tooltips, kleuren & fonts → tokens, min-maten, overlap-scenario's testen door venster te verkleinen.

### Fase 2 — Beheerschermen · ~1 dag
`KlantenView`, `KlantDetailView`, `LijstenView`, `LeveranciersView`, `AfwerkingenView`, `ArchiefView`, `OffertesLijstView`, `ExportCenterView`.

### Fase 3 — Dialogen & import-vensters · ~1 dag
`KlantDialog`, `LijstDialog`, `FactuurInfoDialog`, `InstellingenWindow`, `BulkLijstenWindow`, `LijstenWindow`, `ImportPreviewWindow`, `KlantImportPreviewWindow`, `AfwerkingImportPreviewWindow`, `FactuurPreviewWindow`, `WeekWerkLijstWindow`, `PlanningTijdDialog`.
Focus: resizable + min-maten, `SizeToContent` waar passend, knoppenrijen die niet overlappen bij lange labels (Wrap-panel of rechts uitlijnen met spacing).

### Fase 4 — Consistentie-pass & QA · ~½ dag
- Grep-controles: geen hex-kleuren meer in `Views/` (behalve theme), geen losse `FontSize=`, geen `Width="[0-9]` op tekstcontainers.
- Volledige visuele QA op 3 profielen: 1366×768 (klein Windows-laptop), 1920×1080, macOS Retina (Veerle).
- Restpunten in dit document afvinken/aanvullen.

## Visuele checklist (per scherm, na elke fase)

1. Venster verkleinen tot minimum → niets overlapt, niets valt weg zonder scrollbar.
2. Lange teksten testen (klant "Vanoverberghe-Vandenbroucke", omschrijving 100+ tekens) → ellipsis + tooltip, geen uitloop.
3. Alle knoppen zichtbaar en klikbaar op 1366×768.
4. Vergrendel/login-overlay blijft correct over het scherm liggen.
5. Vergelijk kleur/typografie met een reeds afgewerkt scherm — identieke taal.

## Status

| Fase | Status |
|---|---|
| 0 — Fundament | ✅ 20-07-2026 (branch `feature/ui-fase-0`, verify groen) |
| 1 — Kernschermen | ✅ 20-07-2026 (branch `feature/ui-fase-1`, verify groen) |
| 2 — Beheerschermen | 🟡 20/21-07-2026 (branch `feature/ui-fase-2`, alle 8 schermen gecommit; verify.ps1 + visuele QA + merge nog uit te voeren op Windows) |
| 3 — Dialogen & import | 🟡 21-07-2026 (branch `feature/ui-fase-3`, alle 12 vensters aangepast; verify.ps1 + visuele QA + merge nog uit te voeren op Windows). Nieuw in theme: `SuccessBorder`. De in deze fase toegevoegde `Planning*`-tokens (donker planningsthema) zijn in de planning-redesign weer verwijderd. |
| Planning-redesign | 🟡 21-07-2026 (branch `feature/ui-planning-redesign`, afgetakt van `feature/ui-fase-3`). Donker sub-thema volledig verwijderd; volledig planning-pad naar huisstijl: `Planning*`-tokens weg, `GhostBtn` licht + nieuwe `DangerBtn` + `Capacity*`-tokens in theme; PlanningCalendarWindow licht met QuadroHeader-taal, DayTile-styling van C# (IBrush) naar AXAML-classes, proportionele capaciteitsbalk (was 120px vast → overflow-bug), toasts bovenaan; PlanningTijdDialog licht (AccentBtn/GhostBtn); WeekWerkLijstWindow AccentBtn-header, Black/White → tokens, star-kolommen + MinWidth. Aparte fix-commit: kalendergrid dynamisch 35/42 cellen (6-weken-maanden verloren laatste dagen). Nog uit te voeren op Windows: `.\verify.ps1` + visuele checklist (min-venster, lange klantnamen, 1366×768) + merge. |
| 4 — Consistentie & QA | 🟡 21-07-2026 (branch `feature/ui-fase-4`, afgetakt van `feature/ui-planning-redesign`). Grep-pass volledig groen: 0 hex-kleuren, 0 losse `FontSize=`, 0 vaste `Width` op TextBlocks in `Views/`. Gevonden & gefixt: **ImportPreviewView** was in fase 1–3 gemist (wordt nog gebruikt via DialogService) → volledige pass (resizable + min-maten 900×560, QuadroHeader-control, tokens/classes, star-toolbar, trimming/tooltips, scroll rechterkolom); **QuadroHeader**-control naar tokens + lokale wordmark-classes (subtitel `#CCC` → `TextMutedOnDark`, Terug-knop → AccentBtn); **ArchiefView**-schaduwen/overlays naar nieuwe theme-tokens. Nieuw in theme: `ShadowBar`, `ShadowCard`, `ShadowPanel`, `ShadowTopEdge` (BoxShadows) + `OverlayDim`, `OverlayLight`. Nog uit te voeren op Windows: `.\verify.ps1` + volledige visuele QA op 1366×768 / 1920×1080 / Retina + merge (samen met open fases 2, 3 en planning-redesign). |

*Vink af (✅ + datum) na merge van elke fase-branch, zodat een volgende chat de stand kent.*
