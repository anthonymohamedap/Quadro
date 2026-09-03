# User Stories — Quadro Inlijstatelier
## Feedbackronde Veerle — Versie 3.0 (testdag, 18/08/2026)

Dit document beschrijft de user stories voortgekomen uit Veerle's feedback na een testdag met de
cloud-testversie. Elke story bevat acceptatiecriteria en een technische uitwerking op basis van de
bestaande codebase. Nog niet ingepland — enkel beschreven zodat ze klaarstaan om opgepakt te worden.

> **Bron-mail (Veerle), 18/08/2026:**
> 1. Het veld "Inleg (B x H)cm" — er ontbreekt een veld om het nummer van de inleg in te geven
>    (kadernummer of afstandshouder-nummer, uit dezelfde lijst als de kadernummers).
> 2. Bij een 2e inlijsting voor dezelfde klant zie je de afzonderlijke prijs pas na opslaan — voor
>    die tijd toont het systeem enkel de totaalprijs van beide inlijstingen samen.
> 3. Op de overzichtslijst van de werkbonnen voor het atelier wordt niet herhaald wat er moet
>    ingelijst worden (zie bijlage `Print werklijst atelier.pdf`).
> 4. Alle testoffertes staan op "Concept" — hoe gaan ze naar "Verstuurd" of "Goedgekeurd door de
>    klant"? En is er een manier om een offerte voor de klant af te drukken (buiten de
>    bestelbon-preview)?

---

## US-53 · Offerteregel: nummer van inleg toevoegen

**Als:** gebruiker (Veerle, Kurt)
**Wil ik:** bij "Inleg (B x H)cm" ook het nummer van de inleg (kadernummer of
afstandshouder-nummer) kunnen ingeven
**Zodat:** de atelier-medewerker meteen weet welk exact artikel als inleg gebruikt moet worden,
zonder dit apart te moeten opzoeken of noteren

**Bron:** Veerle — "We merken dat hier een veld ontbreekt om het nummer van de inleg in te geven.
Meestal is dit gewoon een nummer van een kader uit het systeem, of het nummer van een
afstandshouder die ook in dezelfde lijst staat als de kadernummers."

### Acceptatiecriteria

- Bij het invullen van "Inleg (B x H)cm" op een offerteregel kan ook het artikel (kader- of
  afstandshoudernummer) van die inleg gekozen/ingegeven worden.
- Dit nummer is zichtbaar op de offerteregel in de offerte-detail, op de bestelbon en op de
  weeklijst-print voor het atelier (naast de bestaande "inleg 1 / inleg 2" B×H-weergave).
- Bestaande offertes zonder ingevuld inleg-nummer blijven werken (veld is optioneel).

### Technische uitwerking

**Bestand:** `QuadroApp.Data/Model/DB/OfferteRegel.cs` (regels 37-38, naast `InlegBreedteCm` /
`InlegHoogteCm`).

**Beslissing om te bevestigen:** twee opties voor het veld zelf —
1. **Vrij tekstveld** `InlegNummer` (string) — snel te bouwen, geen validatie tegen de catalogus.
2. **Echte catalogus-referentie** `InlegTypeLijstId` (FK naar `TypeLijst`, dezelfde tabel als de
   kaderkeuze) met een combobox zoals bij de bestaande lijstkeuze (`TypeLijstId`) — consistenter
   (autocomplete op artikelnummer, voorraadkoppeling mogelijk) maar meer werk: extra FK, migratie,
   en een tweede TypeLijst-picker in `OfferteView.axaml` naast de bestaande.

   Gezien Veerle expliciet zegt "die ook in dezelfde lijst staat als de kadernummers" is optie 2
   waarschijnlijk de betere match, maar dit moet bevestigd worden vóór het bouwen.

**Te raken bestanden (optie 2):**
- `QuadroApp.Data/Model/DB/OfferteRegel.cs` — nieuw `int? InlegTypeLijstId` + navigatie.
- `QuadroApp.Data/Data/AppDbContext.cs` — FK-configuratie (naar analogie van `TypeLijstId`).
- Nieuwe EF-migratie (SQLite + Postgres, zie `QuadroApp.Data/Migrations` /
  `QuadroApp.Migrations.Npgsql/Migrations` voor het patroon).
- `Views/OfferteView.axaml` — extra combobox naast de bestaande inleg B×H-velden
  (rond regel 334 e.v., waar de regel-bewerk-velden staan).
- `ViewModels/Offerte/OfferteRegelViewModel.cs` / `OfferteRegelbeheerViewModel` — property +
  catalogus-binding.
- `Service/PdfWeekLijstExporter.cs` — `WeekWerkItem`/`Inleg1`-opbouw uitbreiden met het nummer.

⏱ Schatting: 2-3 uur (optie 2) / 45 min (optie 1) · Complexiteit: gemiddeld (optie 2) / laag (optie 1)

---

## US-54 · Live regelprijs blijft soms hangen zonder foutmelding

**Als:** gebruiker
**Wil ik:** dat de afzonderlijke prijs per offerteregel altijd live bijwerkt tijdens het invullen
**Zodat:** ik meteen zie wat een 2e (of volgende) inlijsting kost, en niet moet opslaan om dat te
weten

**Bron:** Veerle — "Bij de prijsberekening van een 2e inlijsting... zie je de afzonderlijke prijs
pas als je alles opslaat. Voor je dit doet, geeft het systeem alleen de totaalprijs van beide
inlijstingen."

### Analyse

Dit is vermoedelijk **geen ontbrekende functie maar een bug**. Live herberekening per regel bestaat
al: `OffertePrijsViewModel.TriggerRecalc()` (aangeroepen bij elke wijziging aan een regel/afwerking)
start een gedebouncte berekening (350ms) die per-regel prijzen (`TotaalExcl`, `SubtotaalExBtw`,
`BtwBedrag`, `TotaalInclBtw`) live terugschrijft naar de regels-lijst — zichtbaar in
`Views/OfferteView.axaml` (regel ~186, `Text="{Binding TotaalInclBtw, ...}"` in de regel-lijst).

Het probleem zit in `OffertePrijsViewModel.BerekenAsync` (regels 75-80): de gedebouncte
(stille) berekening roept `_runFullValidation(showFeedback: false)` aan. Als de offerte op dat
moment (nog) niet volledig geldig is — wat tijdens het invullen van een 2e regel heel normaal is,
bv. omdat die regel nog niet compleet is — geeft `RunValidationOrToastAsync`
(`ViewModels/Offerte/OfferteViewModel.cs`, regels 1087-1101) `false` terug **zonder enige melding**
(de `Toast.Error`/`Toast.Warning`-aanroepen zijn er, maar staan achter `if (showFeedback)`). Het
gevolg: de hele herberekening — dus ook voor de reeds complete 1e regel — slaat stil over, en alle
regelprijzen blijven "hangen" op hun oude (of lege) waarde tot de offerte alsnog volledig valideert
(wat vaak pas gebeurt bij het opslaan, dat wél een succesvolle validatie afdwingt).

### Acceptatiecriteria

- Terwijl een gebruiker een 2e (of volgende) offerteregel invult, blijft de prijs van de reeds
  volledig ingevulde 1e regel zichtbaar en correct — deze wordt niet "bevroren" door een nog
  onvolledige 2e regel.
- Zodra een regel zelf voldoende ingevuld is om een prijs te berekenen, verschijnt die prijs meteen
  (binnen de bestaande 350ms-debounce), ook al is de offerte als geheel nog niet 100% valide.
- Als de herberekening om een andere reden faalt, krijgt de gebruiker een duidelijke (niet-opdringerige)
  indicatie in plaats van stilzwijgend verouderde prijzen.

### Technische uitwerking

**Bestanden:** `ViewModels/Offerte/OffertePrijsViewModel.cs` (`BerekenAsync`, regels 75-104),
`ViewModels/Offerte/OfferteViewModel.cs` (`RunValidationOrToastAsync`, regels 1087-1101).

Voorstel: herbereken **per regel afzonderlijk** in plaats van de validatie voor de volledige offerte
als alles-of-niets-gate te gebruiken — regels die zelf compleet zijn (klant + type lijst + geldige
afmetingen) worden herberekend en getoond, ook als een andere regel nog onvolledig is. Alternatief
(kleinere ingreep): laat de stille validatiefout een subtiele status tonen (bv. een klein "☐ nog niet
volledig"-label op de betrokken regel) in plaats van niets, zodat het duidelijk is dát er niet
herberekend werd en waarom.

⏱ Schatting: 2-4 uur (afhankelijk van gekozen aanpak) · Complexiteit: gemiddeld

---

## US-55 · Weeklijst-print voor atelier: inlijstinginfo ontbreekt/herhaalt niet

**Als:** atelier-medewerker
**Wil ik:** op de geprinte weeklijst voor elke te maken inlijsting de volledige info zien (wat moet
ingelijst worden)
**Zodat:** ik niet terug in de app moet kijken om te weten wat ik precies moet maken

**Bron:** Veerle — "Op de overzichtslijst van de werkbonnen voor het atelier, wordt niet herhaald
wat er moet ingelijst worden. (zie bijlage)" — bijlage `Print werklijst atelier.pdf` ontvangen en
geanalyseerd.

### Analyse (bevestigd aan de hand van de bijlage)

De bijlage toont de weeklijst-PDF (Week 35/2026): voor **elk** item staat de omschrijving generiek
als **"Inlijsten"**, en de velden **"artikel 1 :"** en **"artikel 2 :"** staan voor elk item leeg.
Dit zijn drie afzonderlijke, bevestigde bugs in `Service/PdfWeekLijstExporter.cs` en de
planning-flow die de werktaken aanmaakt:

1. **Omschrijving is altijd de statische tekst "Inlijsten"**, nooit de eigenlijke omschrijving van
   het stuk (wat de klant liet inlijsten). Root cause:
   `ViewModels/Planning/PlanningUitvoeringViewModel.cs`, regels 111, 170 en 272 — alle drie de
   plekken waar een regel ingepland wordt (`_workflow.PlanRegelMetDagCapaciteitAsync(...)`) geven
   de **hardcoded string `"Inlijsten"`** mee als taakomschrijving, in plaats van de omschrijving van
   de offerteregel zelf (`OfferteRegel.Titel`, al beschikbaar in `regel`/`r` op elk van die drie
   plekken). Daardoor is `WerkTaak.Omschrijving` — en dus wat de weeklijst print — voor elke taak
   identiek en nietszeggend.
2. **"artikel 1 :" en "artikel 2 :" zijn dode labels zonder waarde** —
   `Service/PdfWeekLijstExporter.cs`, regels 181-182:
   ```csharp
   right.Item().Text("artikel 1 :").FontSize(9);
   right.Item().Text("artikel 2 :").FontSize(9);
   ```
   Dit is letterlijke, statische tekst — er wordt nergens een waarde geïnterpoleerd. Onduidelijk
   wat hier ooit getoond moest worden (mogelijk een tweede/derde lijst-artikelnummer bij
   samengestelde kaders?) — te bevestigen met Veerle/Anthony vóór dit ingevuld wordt.
3. **`Inleg2` is altijd een lege string** — `ViewModels/Planning/WeekWerkLijstViewModel.cs`, regel
   260: `Inleg2 = ""`. Zelfs als een offerteregel een tweede inleg-maat zou hebben, wordt die nooit
   doorgegeven (er is momenteel ook geen tweede inleg-veld op `OfferteRegel` — hangt samen met
   US-53).

### Acceptatiecriteria

- De weeklijst-PDF toont per werk-item de **eigenlijke omschrijving** van de offerteregel (bv. wat
  de klant liet inlijsten), niet de generieke tekst "Inlijsten".
- "artikel 1"/"artikel 2" tonen ofwel een echte waarde, of worden verwijderd als ze geen betekenis
  (meer) hebben — te bevestigen.
- Bestaande, al ingeplande taken tonen na de fix hun (voortaan bewaarde) omschrijving; taken die vóór
  de fix als "Inlijsten" zijn opgeslagen, blijven zo (geen retroactieve herschrijving nodig).

### Technische uitwerking

**Bestanden:**
- `ViewModels/Planning/PlanningUitvoeringViewModel.cs` (regels 111, 170, 272) — vervang de
  hardcoded `"Inlijsten"` door bv. `r.Titel is { Length: > 0 } t ? t : "Inlijsten"` (fallback
  behouden voor regels zonder titel).
- `Service/PdfWeekLijstExporter.cs` (regels 181-182) — "artikel 1/2"-regels invullen met een echte
  waarde of verwijderen, na bevestiging wat ze moeten tonen.
- `ViewModels/Planning/WeekWerkLijstViewModel.cs` (regel 260) — `Inleg2` blijft `""` tot er een
  tweede inleg-veld bestaat (zie US-53); geen actie nodig tenzij US-53 wordt opgepakt.

⏱ Schatting: 1 uur (omschrijving-fix) + 30 min (artikel 1/2, na bevestiging) · Complexiteit: laag

---

## US-56 · Offerte-status manueel kunnen wijzigen

**Als:** gebruiker
**Wil ik:** de status van een offerte manueel kunnen zetten op "Verzonden" of "Goedgekeurd"
**Zodat:** ik kan bijhouden waar een offerte staat in het traject, ook vóór er een werkbon/factuur
aan hangt

**Bron:** Veerle — "Alle testoffertes die ik maakte, staan op 'concept'. Hoe gaan ze naar
'verstuurd' of 'goedgekeurd door de klant'?"

### Analyse

Dit is een echt gat, geen bedieningsfout van Veerle: `Offerte.Status` wordt in de hele UI enkel
**getoond** (`Views/OfferteView.axaml`, regels 45-47 en 695-697 —
`Text="{Binding Offerte.Status}"`), nergens is er een control om de status te **wijzigen**. De
automatische propagatie (`Service/OfferteStatusPropagation.cs`) zet een offerte enkel vooruit naar
InProductie/Afgewerkt/Besteld/Betaald, en enkel als de status al op z'n minst "Goedgekeurd" staat —
de stap Concept → Verzonden → Goedgekeurd moet dus sowieso manueel gebeuren, maar daar is nu geen
UI voor.

### Acceptatiecriteria

- In de offerte-detail kan de gebruiker de status manueel zetten op "Verzonden" of "Goedgekeurd"
  (en terug, zolang de offerte nog niet in productie is).
- De bestaande automatische propagatie (werkbon/bestelbon → offerte-status) blijft ongewijzigd
  werken.
- Een offerte die al verder staat dan "Goedgekeurd" (InProductie e.v.) kan niet per ongeluk terug
  naar Concept gezet worden vanuit dit scherm (of enkel met expliciete bevestiging).

### Technische uitwerking

**Bestanden:** `Views/OfferteView.axaml` (rond regels 45-47, waar de status nu enkel als tekst
staat), `ViewModels/Offerte/OfferteViewModel.cs` of een nieuwe kleine sub-ViewModel/command voor de
statuswijziging zelf, `Model/DB/OfferteStatus.cs` (bestaande enum, geen wijziging nodig).

Voorstel: een dropdown/knoppen-groepje naast de bestaande status-badge, enkel met de statussen die
vanuit de huidige status manueel toegelaten zijn (Concept→Verzonden, Verzonden→Goedgekeurd,
Goedgekeurd→Concept/Verzonden terug als correctie). Wijziging gaat via `SaveCoreAsync` zoals elke
andere veldwijziging.

⏱ Schatting: 1-2 uur · Complexiteit: laag-gemiddeld

---

## US-57 · Offerte kunnen afdrukken/exporteren voor de klant

**Als:** gebruiker
**Wil ik:** een offerte als nette PDF kunnen afdrukken/opslaan om naar de klant te sturen
**Zodat:** ik de klant een officieel voorstel kan geven vóór er sprake is van een bestelbon

**Bron:** Veerle — "Nu bekijk ik de preview van een bestelbon en print die af. Is er nog een andere
manier om een offerte voor de klant te printen?"

### Analyse

Er bestaat vandaag **geen** PDF-export voor de offerte zelf — enkel `Service/PdfFactuurExporter.cs`
(facturen/bestelbonnen) en `Service/PdfWeekLijstExporter.cs` (interne weeklijst). Veerle's
workaround (bestelbon-preview afdrukken) toont productie-/facturatiegegevens, niet een
klantvriendelijk offertevoorstel — dat is dus niet geschikt als vervanger op termijn.

### Acceptatiecriteria

- Vanuit de offerte-detail kan een PDF gegenereerd worden met: klantgegevens, alle offerteregels
  (afmetingen, gekozen lijst/afwerkingen, prijs per regel), subtotaal, korting, BTW, totaal —
  zonder interne/productiegegevens (geen werkbon-status, geen leveranciersinfo).
- De PDF is herkenbaar als "Offerte" (i.t.t. "Bestelbon"/"Factuur"), met offertenummer (zie
  doorlopende nummering, reeds gebouwd) en datum.
- Werkt zowel voor een nieuwe (nog niet opgeslagen) als een bestaande offerte.

### Technische uitwerking

**Nieuw bestand:** `Service/PdfOfferteExporter.cs`, naar analogie van `PdfFactuurExporter.cs`
(zelfde QuestPDF-aanpak, logo/header hergebruiken).
**Te raken bestanden:** `Views/OfferteView.axaml` / `OfferteViewModel.cs` — nieuwe knop "Offerte
afdrukken" naast de bestaande workflow-knoppen (`FactuurCommand`, `OpenPlanningCommand`), en een
`FilePickerService`-aanroep zoals al gebruikt bij de factuur-export.

⏱ Schatting: 3-4 uur · Complexiteit: gemiddeld

---

## Samenvatting

| # | Titel | Type | Schatting |
|---|-------|------|-----------|
| US-53 | Inleg-nummer op offerteregel | Ontbrekende functie | 45 min – 3 uur |
| US-54 | Live regelprijs blijft hangen zonder melding | Bug | 2-4 uur |
| US-55 | Weeklijst-print toont overal "Inlijsten" i.p.v. echte omschrijving | Bug (bevestigd) | ~1,5 uur |
| US-56 | Offerte-status manueel wijzigen | Ontbrekende functie | 1-2 uur |
| US-57 | Offerte afdrukken/exporteren voor klant | Ontbrekende functie | 3-4 uur |
