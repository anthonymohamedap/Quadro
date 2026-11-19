# Plan — één verkoop = één dossier (US-87 → US-95)

Stand 26/10/2026. Bron: canvas "QuadroApp – huidige flow (as-is) – nov 2026", pagina **5 · Concept: verkoopdossier** (schermen K01–K14, to-be flow, klikbaar mini-prototype), met de pijnpunten P1–P15 van pagina 4.

## Uitgangspunt

Vandaag is één inlijsting verspreid over drie schermen en drie nummers: offerte 412 (Prijsberekening), werkbon #57 (Bestellingen) en bestelbon 2026-19 (Bestelbonnen). De status wordt op drie plaatsen apart bijgehouden en loopt niet gelijk (P10). Het concept:

- **Eén scherm Verkopen** vervangt Prijsberekening, Bestellingen en Bestelbonnen.
- **Een verkoop is een dossier.** Offerte, werkbon, bestelbon en betalingen hangen eronder.
- **Vijf stappen**: 1 Offerte · 2 Bestelling · 3 In productie · 4 Klaar om af te halen · 5 Afgehaald & betaald. Open je een verkoop, dan sta je op de stap waar je gestopt was. Vorige stappen kun je terug bekijken.
- **Stappen overslaan**: Bestelling en/of In productie (bv. standaardkader uit voorraad). Offerte en Afhalen niet.
- **Eén nummer**: verkoop 412 = offertenummer, staat vast vanaf de start. De bestelbon houdt zijn eigen volgnummer 2026-19 (boekhouding).
- **Afdrukken** = PDF-voorbeeld + "Afdrukken" / "Bewaren als PDF" in een vaste map, ongeacht de status.
- **Planning** wordt een pagina in het hoofdvenster (geen popup) en zit ook in stap 2 van het dossier.
- **Winkelverkopen** en **Overzicht betalingen** blijven apart.
- **Betaalwijzen**: Bancontact, Cash, Credit card, Overschrijving.

## Wijziging 27/10/2026: geen offerte-PDF, de bestelbon is het prijsvoorstel

In de praktijk krijgt de klant de **bestelbon**, ook als prijsvoorstel vóór het akkoord. De offerte-PDF (US-57, canvas K04) is daarom verwijderd (branch `feature/bestelbon-als-offerte`). Gevolgen voor dit plan:

- De bestelbon kan al **vanaf stap 1** aangemaakt en afgedrukt worden; de stappenbalk heeft daarvoor een tweede knop naast de volgende stap. De aanname "de bestelbon ontstaat pas bij Klaar om af te halen" (K10, P3) vervalt.
- `MeldKlaarAsync` maakt de bestelbon enkel aan als die nog niet bestaat.
- US-94 drukt enkel werkbon en bestelbon af; K04 vervalt.
- **Beslist (27/10):** een bestelbon vóór het akkoord is *voorlopig* ("V412", "voorlopig (offerte 412)"), zonder volgnummer, en telt niet mee voor de boekhouding. Het volgnummer (2026-19) en de besteldatum komen pas bij "Bevestigen als bestelling", zodat klanten die niet bestellen geen gaten in de nummering laten.

## Architectuurkeuzes

### 1. Geen nieuwe entiteit: de verkoop ís de offerte

`Offerte` heeft al 1:1 een `WerkBon` en 1:1 een `Factuur` (unieke index op `OfferteId`), plus `Ontvangst.OfferteId`. Een aparte tabel `Verkoop` zou alleen een extra join zijn. **Verkoopnummer = `Offerte.OfferteNummer`.**

Gevolg: "verkoop" is een UI-woord. In de code blijven `Offerte`, `WerkBon` en `Factuur` bestaan; enkel de schermen veranderen.

### 2. De stap wordt afgeleid, niet opgeslagen

Nieuwe pure functie `VerkoopStapResolver.Bepaal(offerte, werkBon?, factuur?, betaald)` → `VerkoopStapInfo { HuidigeStap, StatusPerStap[5], IsGeannuleerd, IsAfgehaaldNogTeBetalen }`. Eén plek, volledig unit-testbaar, gebruikt door lijst, dossier, Home en PDF's.

| Stap | Voorwaarde (bestaande statussen) |
|---|---|
| 1 Offerte | offerte Concept / Verzonden, geen werkbon |
| 2 Bestelling | werkbon Gepland |
| 3 In productie | werkbon InUitvoering |
| 4 Klaar om af te halen | werkbon Afgewerkt (bestelbon bestaat) |
| 5 Afhalen en betalen | werkbon Afgehaald en rest > 0 ("afgehaald, nog te betalen") |
| Afgerond | werkbon Afgehaald en bestelbon Betaald |
| Geannuleerd | offerte Geannuleerd |

Een stap die in `OvergeslagenStappen` staat, krijgt status *Overgeslagen* in plaats van *Klaar*.

### 3. Nieuw in het datamodel (één migratie)

Op `Offerte`:

- `OvergeslagenStappen` (`int`, `[Flags] enum VerkoopStapFlags { Bestelling = 1, Productie = 2 }`)
- `OverslaanReden` (`string?`, max 200)
- `GeannuleerdOp` (`DateTime?`), `AnnuleerReden` (`string?`, max 300)

Op `WerkTaak`: `VoltooidOp` (`DateTime?`) en `VoltooidDoor` (`string?`, initialen) voor de voortgang per inlijsting in stap 3 (K08: "ingelijst en nagekeken 26/10 · AB"). Vandaag markeert `MarkeerTaakAlsVoltooidAsync` dat met `Resource = "Voltooid"` en geen knop roept het aan; dat wordt een echt veld.

`Betaalwijze` (opgeslagen als string): enum wordt `Bancontact, Cash, CreditCard, Overschrijving` met data-update in dezelfde migratie: `Kontant→Cash`, `Visa→CreditCard`, `Storting→Overschrijving`. `Cheque` en `Proton` → `Cash` (afgesproken in US-79; eerst nakijken of ze in productie voorkomen). **Dit gebeurt al in de sprint (US-79)**; dit plan voegt hier niets aan toe.

Migratie via `.\Scripts\add-migration.ps1 Verkoopdossier` (SQLite + Npgsql), nooit handmatige SQL.

### 4. Eén orchestrator: `VerkoopDossierService`

Nieuwe service bovenop de bestaande (`IWorkflowService`, `IWerkBonWorkflowService`, `IFactuurWorkflowService`, `IOntvangstService`, `IStockService`). Elke knop in de stappenbalk roept precies één methode aan, in één transactie:

| Methode | Doet | Vervangt |
|---|---|---|
| `StartVerkoopAsync(klantId)` | maakt offerte + kent nummer meteen toe | "Nieuwe offerte" (P7: geen "offertenr. 0" meer) |
| `BevestigAlsBestellingAsync(id)` | offerte → Goedgekeurd, werkbon aanmaken | "Bevestigen → Bestelling" |
| `StartProductieAsync(id)` | werkbon → InUitvoering, offerte → InProductie | inplannen zette offerte al op InProductie (P10) |
| `MeldKlaarAsync(id)` | werkbon → Afgewerkt, bestelbon aanmaken als die nog niet bestaat (zichtbaar gemeld), offerte → Afgewerkt → Besteld | onzichtbare bestelbon (P3) |
| `SlaStappenOverAsync(id, stappen, reden)` | zet de tussenliggende werkbonstatussen in één keer, bewaart flags + reden, maakt bestelbon | nieuw |
| `RekenAfEnHaalAfAsync(id, bedrag, betaalwijze, ontvangen?)` | hergebruikt US-76-logica (betaling + Afgehaald, gesplitst of later) | afrekenblok werkbon |
| `HaalAfZonderBetalenAsync(id)` | werkbon → Afgehaald, rest blijft open | idem |
| `AnnuleerAsync(id, reden)` | hergebruikt `ChangeOfferteStatusAsync(Geannuleerd)` (geeft voorraad vrij, annuleert open leveranciersbestellingen, archiveert een snapshot van de werkbon) + reden bewaren + bestelbon → Geannuleerd + planning-taken weg | "Naar archief" als enige uitweg (P12) |

**Statussynchronisatie**: `OfferteStatusPropagation` blijft, maar de aanroep in `WerkBonWorkflowService` bij *inplannen* (regels 111/226/291–302) verdwijnt; offerte → InProductie gebeurt alleen nog in `StartProductieAsync`. De transitietabellen in `WorkflowService` blijven de bewaker; `SlaStappenOverAsync` doorloopt ze stap voor stap binnen één transactie in plaats van ze te omzeilen.

De handmatige knoppen "↩ Concept" / "↩ Verzonden" (P15) en de werkbon-statuskeuzelijst (P4) verdwijnen.

### 5. Afdrukken: één `IDocumentAfdrukService`

`ToonEnDrukAsync(DocumentSoort soort, int offerteId)` voor Offerte, Werkbon en Bestelbon:

- bouwt de PDF met de bestaande `PdfFactuurExporter` (bestelbon) + een nieuwe `PdfWerkBonExporter` (zonder prijzen, afvinkvakjes, één blok per inlijsting — K07);
- toont een voorbeeldvenster (QuestPDF `GenerateImages()` → `Image`-lijst in Avalonia);
- "Bewaren als PDF" → vaste map `Documenten/QuadroApp/{Offertes|Werkbonnen|Bestelbonnen}/<Soort>-<nr>.pdf`;
- "Afdrukken" → bestand bewaren en openen met de systeemviewer (`IPathOpener`), waar macOS/Windows de printerdialoog geven. Printerkeuze en aantal exemplaren in de app zelf (K04/K07/K11) is fase 2 (zie open vraag 4).

Bestelbon afdrukken werkt **in elke status** (P2, US-82); de knoppen "Markeer als klaar voor export" en "Export PDF" verdwijnen. Draft / KlaarVoorExport / Geexporteerd worden in de UI samen "Open" (P9).

### 6. Planning: Window → UserControl

`PlanningCalendarWindow` wordt `PlanningView` (UserControl) met dezelfde `PlanningCalendarViewModel`. Twee gastheren:

- **K14** zijbalk "Planning" → `NavigateToAsync<PlanningCalendarViewModel>()` (overzicht, geen regels te plannen);
- **K06** stap 2 van het dossier → de view met de werkbonregels van deze verkoop.

De drie `new PlanningCalendarWindow` (`HomeViewModel:123`, `OfferteWorkflowViewModel:170/215`) verdwijnen. `WeekWerkLijstWindow` mag een venster blijven (afdrukbare weeklijst).

## Samenhang met bestaande stories

Nagekeken in `docs/backlog` en in de code (26/10/2026). Wat al gebouwd is, wordt hergebruikt; waar het concept een eerdere beslissing omkeert, staat het hier expliciet.

| Story | Wat er al is | Gevolg voor dit plan |
|---|---|---|
| **US-42** Statussync offerte ↔ werkbon ↔ bestelbon | `OfferteStatusPropagation`: werkbon/bestelbon zetten de offerte vooruit, transactioneel | Blijft. Enige wijziging: inplannen zet de offerte niet meer op InProductie (US-51-aanroep in `WerkBonWorkflowService`), dat doet nu "Start productie" (P10). |
| **US-48** Status-reconciliatie | `OfferteStatusReconciliatieService.BepaalDoelStatus(werkbon, factuur)` leidt de offertestatus al af | `VerkoopStapResolver` bouwt hierop voort, zodat stap en offertestatus nooit verschillen. Extra test: voor elke combinatie zijn resolver en `BepaalDoelStatus` het eens. |
| **US-56** Offertestatus manueel wijzigen | knoppen Concept ↔ Verzonden ↔ Goedgekeurd, ook terug als correctie | Het concept kent geen aparte stap Verzonden. Voorstel: **Offerte afdrukken zet Concept → Verzonden automatisch** (K01 toont "Offerte · afgedrukt"); Goedgekeurd = "Klant akkoord". Terug naar Concept enkel zolang er geen werkbon is (lost P15 op). → open vraag 7. |
| **US-57** Offerte-PDF | `PdfOfferteExporter` + bestandskiezer | **Verwijderd (27/10)**: de klant krijgt de bestelbon als prijsvoorstel. |
| **US-27** Plandatum = afhaaldatum | plannen zet `OfferteRegel.AfhaalDatum` = laatste plandag; handmatig overschrijven mag | Blijft zo. Stap 4 krijgt "Afhalen wijzigen" als die handmatige overschrijving (K10). |
| **US-21** Plannen per regel | aparte datums per inlijsting | Blijft; zit in de planner in stap 2. |
| **US-49 / US-50** Planning-redesign + drag & drop | KPI's, dagtegels, dagdetail; code-behind `DayTile_PointerPressed`, `OnDataContextChanged` en DnD via `DataTransfer` | Bij Window → UserControl (US-92) moet die code-behind letterlijk mee; dat is het grootste risico van fase C. |
| **US-51** Werkbonnenlijst + werkbonvenster | bewuste keuze (29/07) voor een **modaal** `WerkBonDetailWindow` met "Open offerte" | **Het concept draait dit om**: het werkbonvenster verdwijnt in het dossier. Akkoord nodig vóór US-95. |
| **US-76** Afrekenen bij afhaling | betaling + Afgehaald in één transactie, gesplitst of later betalen (werkbonvenster + planning) | Logica wordt hergebruikt in stap 5; enkel de UI verhuist. |
| **US-69 / US-70 / US-73** Voorschot, betalingen op bestelbon, betalingen op PDF | `Ontvangst` met `OfferteId`/`FactuurId`, rest op de PDF | Blijft. "Betalingen in deze verkoop" = ontvangsten met dit `OfferteId` of het `FactuurId` van de bestelbon. |
| **US-58 / US-68 / US-75** Standaardkader | standaardkader in de offerte; US-75 (prijs incl. btw) nog open | US-91/94 tonen omschrijving + afmeting overal (US-80). US-75 los houden. |
| **US-45** Bestelbonnen in het Exportcenter + `FactuurExportService` | export vereist status KlaarVoorExport en zet Geexporteerd; `OntvangstService` zet een bon terug naar KlaarVoorExport/Geexporteerd | De statussen blijven in de database (boekhouding/Exportcenter). Afdrukken heeft geen statusvoorwaarde; zoals US-82 AC 3 afspreekt zet het eerste afdrukken een Draft/KlaarVoorExport-bon intern op Geexporteerd (exportpad bewaard), Betaald blijft Betaald. In de UI heten Draft/KlaarVoorExport/Geexporteerd samen "Open" (US-85). |
| **UI_OPTIMIZATION_PLAN** | tokens, style-classes, geen hex/`FontSize=` in views | Geldt voor elk nieuw scherm (Verkopen, dossier, preview). Kleuren uit de canvas = bestaande tokens (`BrandYellow`, `BrandDark`, …). |
| **BL-010 / BL-014** (Backlog.docx) | `OfferteViewModel` is een god-object (nu 1 798 regels) met handmatige property-forwarders | US-91 splitst niet verder op dan nodig om in de stap te passen; geen extra forwarders bijmaken. |
| **US-79–86** (sprint nov 2026) | `Sprint_2026-11_US-79-86.md` op branch `docs/sprint-november-2026` (nog niet in main); release v1.3.26 ten laatste 11/11, demo bij Veerle 12/11 | Zie hieronder: de sprint gaat eerst en wordt zo gebouwd dat het dossier erop voortbouwt. |

## Stories (volgorde)

| # | Story | Fase | ± |
|---|---|---|---|
| 1 | **US-87** Fundament: stapbepaling, datamodel, `VerkoopDossierService` | A | 5–7u |
| 2 | ~~**US-88** Betaalwijzen~~ — vervalt: wordt US-79 in de sprint | — | 0u |
| 3 | **US-89** Scherm Verkopen (K01) + Nieuwe verkoop (K02) | B | 4–6u |
| 4 | **US-90** Dossier-schil: kop, documentchips, stappenbalk, vorige stap bekijken | B | 4–5u |
| 5 | **US-91** Stap 1 Offerte in het dossier + voorschot (K03, K05) | B | 4–6u |
| 6 | **US-92** Planning als pagina en in stap 2 (K06, K14) | C | 4–6u |
| 7 | **US-93** Stappen 3–5, overslaan, afgerond, annuleren (K08–K10, K12, K13, K09) | C | 6–8u |
| 8 | **US-94** Afdrukken werkbon / bestelbon (K07, K11) | C | 4–6u |
| 9 | **US-95** Navigatie opruimen, Home, oude schermen weg | D | 2–3u |

Totaal ± 34–48u (zonder US-88, dat in de sprint als US-79 gebeurt). Elke story = eigen feature-branch + PR; na fase B is er al een bruikbaar dossier naast de oude schermen.

### Verhouding tot de sprint van november (US-79 → US-86)

De sprint (± 13–17,5u, release ten laatste 11/11) lost dezelfde pijnpunten op **binnen de huidige schermen**; dit plan vervangt die schermen daarna door het dossier. Volgorde: **eerst de sprint, dan dit plan als volgende versie.** Om geen werk weg te gooien, worden de sprintstories zo gebouwd dat het dossier ze hergebruikt:

| Sprint | Bouwen als | Later in dit plan |
|---|---|---|
| US-79 Betaalwijzen | zoals beschreven (Cheque/Proton → Cash) | US-88 vervalt |
| US-80 Standaardkaders (omschrijving, afwerkingen) | zoals beschreven | US-91/94 tonen het gewoon; niets opnieuw |
| US-81 Offertenummer 0 | kies de optie **"krijgt meteen een nummer"** (niet "eerst automatisch opslaan") + eenmalige correctie + waarschuwing afhaaldatum | = US-87 AC 3; `StartVerkoopAsync` gebruikt dezelfde nummering |
| US-82 Bestelbon afdrukken | als kleine `IDocumentAfdrukService` (bestelbon eerst), vaste map `Documenten/QuadroApp/Bestelbonnen` | US-94 voegt offerte, werkbon en het voorbeeldvenster toe |
| US-83 Stappenbalk in de offerte | stapbepaling meteen als **`VerkoopStapResolver`** (incl. tests), de balk als herbruikbare UserControl `StappenBalk` | US-87 krijgt de resolver kant-en-klaar; US-90 hergebruikt de balk; de knop "volgende stap" wordt de voetbalk van het dossier |
| US-84 Offertelijst per fase | tabs + tellers op basis van de resolver, query in een aparte service (`VerkoopOverzichtService`) | US-89 (K01) gebruikt dezelfde service, met 8 tabs i.p.v. 5 |
| US-85 Gewone woorden | één `StatusLabels`-helper/converter voor offerte, werkbon en bestelbon | overal hergebruikt; US-95 doet de rest van de namen |
| US-86 Beginscherm | queries "vandaag af te halen" / "nog te betalen" in `VerkoopOverzichtService` | dezelfde kaarten bovenaan K01 |

Na de sprint krimpt US-87 tot datamodel + `VerkoopDossierService`, en US-89 tot de nieuwe lijst-UI.


---

### US-87 — Fundament: stapbepaling, datamodel, dossierservice

**Als** ontwikkelaar **wil ik** één plek die bepaalt in welke stap een verkoop zit en die elke stapovergang uitvoert, **zodat** offerte, werkbon en bestelbon nooit meer uit de pas lopen.

Acceptatiecriteria:

1. `VerkoopStapResolver` geeft voor elke combinatie uit de tabel hierboven de juiste stap; overgeslagen stappen tonen als *Overgeslagen*.
2. Migratie `Verkoopdossier` voegt `OvergeslagenStappen`, `OverslaanReden`, `GeannuleerdOp`, `AnnuleerReden` toe (SQLite + Npgsql, snapshot zonder openstaande wijzigingen).
3. `StartVerkoopAsync` kent het offertenummer toe bij aanmaken; een nieuwe verkoop heeft nooit nummer 0.
4. Inplannen verandert de offertestatus niet meer; `StartProductieAsync` doet dat.
5. `MeldKlaarAsync` maakt de bestelbon aan en geeft zijn nummer terug (voor de melding "Bestelbon 2026-19 is aangemaakt").
6. `SlaStappenOverAsync(Bestelling | Productie)` brengt een verkoop van stap 1 naar stap 4 in één transactie, met bestelbon.
7. `AnnuleerAsync` zet offerte en bestelbon op Geannuleerd, verwijdert planning en geeft voorraad vrij; betalingen blijven staan (zie open vraag 3).

Technisch: `Service/Verkoop/VerkoopStap.cs`, `VerkoopStapResolver.cs`, `VerkoopDossierService.cs` + `IVerkoopDossierService`; registratie in `App.axaml.cs`. Tests: `VerkoopStapResolverTests` (table-driven, alle rijen), `VerkoopDossierServiceTests` (elke methode, overslaan, annuleren met reservatie, idempotentie bij dubbel klikken).

---

### ~~US-88 — Betaalwijzen~~ (vervalt: zie US-79 in de sprint; onderstaande tekst enkel ter referentie)

**Als** medewerker **wil ik** enkel Bancontact, Cash, Credit card en Overschrijving zien, **zodat** de keuze klopt met hoe klanten echt betalen.

1. Overal dezelfde vier, in die volgorde: voorschot, afrekenen, winkelverkoop, betalingen op de bestelbon.
2. Wisselgeldveld "Ontvangen (cash)" verschijnt bij Cash.
3. Overzicht betalingen: vier kolommen.
4. Bestaande ontvangsten worden omgezet (Kontant→Cash, Visa→Credit card, Storting→Overschrijving, Cheque/Proton→Cash).

Technisch: `Betaalwijze.cs` + weergavenaam (`BetaalwijzeLabels`), dezelfde migratie als US-87 of een eigen, `OverzichtBetalingenViewModel`, `PdfBetalingsOverzichtExporter`, `WinkelVerkopenViewModel`.

---

### US-89 — Scherm Verkopen + Nieuwe verkoop

**Als** medewerker **wil ik** alle verkopen op één plek zien, per fase, **zodat** ik meteen zie wat vandaag af te halen of nog te betalen is.

1. Kaarten bovenaan: "Vandaag af te halen" (afhaaldatum = vandaag, stap 4/5) en "Afgehaald, nog te betalen" (met bedrag).
2. Tabs met aantallen: Lopend · Offerte · Bestelling · In productie · Klaar · Afhalen / te betalen · Afgerond · Geannuleerd.
3. Kolommen: Verkoop, Klant, Gestart, Fase (vijf bolletjes + tekst, overgeslagen = gestreept), Afhalen, Totaal, Nog te betalen.
4. Eén klik opent het dossier (geen dubbelklik meer, P5).
5. Zoeken op verkoopnummer, klantnaam of bestelbonnummer.
6. "+ Nieuwe verkoop" opent een dialoog (K02): klant zoeken of "+ Nieuwe klant" → "Verkoop starten" opent meteen stap 1. Verwijzing naar Winkelverkopen voor toonbankverkopen.

Technisch: `ViewModels/Verkopen/VerkopenViewModel.cs` + `Views/VerkopenView.axaml`; hergebruik `KlantSelectieViewModel` en `IKlantDialogService`. Eén query met `Include(WerkBon)`, `Factuur` en de som van `Ontvangst` per offerte; stap via de resolver (in geheugen). Vervangt `OffertesLijstViewModel`, `WerkBonLijstViewModel` en `FacturenViewModel` als ingang (verwijderen pas in US-95).

---

### US-90 — Dossier-schil

**Als** medewerker **wil ik** bij het openen van een verkoop op de stap staan waar ik gestopt was, **zodat** ik niet hoef te zoeken wat de volgende stap is.

1. Kop: "◀ Verkopen", "Verkoop 412 · Test Quadro", klantgegevens, afhaaldatum; rechts totaal en "nog te betalen"; menu ⋯ met Annuleren (en later Dupliceren).
2. Chips "In deze verkoop": Offerte, Werkbon, Bestelbon 2026-19, Voorschot € 50,00 — gestreept zolang het document niet bestaat.
3. Stappenbalk met vijf stappen: klaar (groen ✓ + datum), huidig (geel, "hier ben je"), overgeslagen (gestreept), nog te doen.
4. Klik op een vorige stap of "◀ Vorige stap" toont die stap **alleen-lezen** met label "Terug gekeken"; de voetknop springt terug naar de huidige stap. Er wordt geen status teruggezet.
5. Voetbalk: "Stap n van 5", contextknoppen per stap, hoofdknop rechts (geel).
6. Overal Nederlandse statusnamen, nooit enumnamen (P9).

Technisch: `VerkoopDossierViewModel` (houdt `OfferteId`, `StapInfo`, `BekekenStap`) met per stap een kind-ViewModel (`StapOfferteViewModel`, `StapBestellingViewModel`, …) en `DataTemplate`s in `App.axaml`. Navigeren via `NavigateToAsync<VerkoopDossierViewModel, int>(offerteId)` (`IParameterReceiver`).

---

### US-91 — Stap 1 Offerte + voorschot

**Als** medewerker **wil ik** de offerte opmaken in het dossier, **zodat** prijs, voorschot en afdrukken samen staan.

1. Links de inlijstingen als kaarten (titel, prijs, maat, lijst of standaardkader, afwerkingen) + "+ Inlijsting"; onderaan subtotaal, btw, korting/meerprijs, totaal, voorschot, nog te betalen.
2. Rechts de geselecteerde inlijsting: titel, aantal, schakelaar *Lijst op maat / Standaardkader*, afwerkingen, prijs (afgesproken prijs, korting %, prijs deze inlijsting). Wijzigingen worden meteen bewaard.
3. Standaardkader-keuzelijst toont naam + omschrijving + prijs en is doorzoekbaar ("c2 zwart"); de kaart Afwerkingen blijft zichtbaar (US-80).
4. "Voorschot ontvangen" (K05): bedrag met snelknoppen 25 % / 50 % / Volledig, betaalwijze, datum.
5. Voetknoppen: "Bestelbon aanmaken / afdrukken" (prijsvoorstel voor de klant) en "Klant akkoord: bevestigen als bestelling ▶".

Technisch: de bestaande `OfferteViewModel` (1 798 regels) + `OfferteRegelViewModel`/`OffertePrijsViewModel` worden hergebruikt; `OfferteView.axaml` wordt opgesplitst in `OfferteRegelsPaneel` en `OfferteRegelEditor` (UserControls) zodat ze in de stap passen. Workflowknoppen uit `OfferteWorkflowViewModel` verhuizen naar de dossier-voetbalk; "Bestelbon aanmaken" in de offerte verdwijnt (P3). Automatisch bewaren: debounce (± 800 ms) op `SaveAsync`, met de bestaande concurrency-afhandeling (`ConcurrencyUx`).

---

### US-92 — Planning als pagina en in stap 2

**Als** medewerker **wil ik** de planner in het hoofdvenster, **zodat** hij niet als los venster achter de app verdwijnt.

1. Zijbalk "Planning" opent de kalender als pagina (K14).
2. Stap 2 Bestelling toont dezelfde kalender + "Werkbon regels" van deze verkoop, met slepen en "Plan op geselecteerde dag" (K06).
3. Een standaardkader-regel toont het kader (C2 – Alu zwart 50×70), niet "Geen lijst gekozen" (P6).
4. Inplannen verandert de stap niet; "Start productie ▶" wel.
5. Voetknoppen: Vorige stap · Werkbon afdrukken · Stap overslaan · Start productie ▶.

Technisch: `PlanningCalendarWindow.axaml(.cs)` → `PlanningView.axaml` (UserControl); code-behind voor drag & drop meeverhuizen. `PlanningCalendarViewModel` krijgt een optionele context `WerkBonId` in plaats van via de offerte geopend te worden. Na deze story: geen `new PlanningCalendarWindow` meer.

---

### US-93 — Stappen 3–5, overslaan, afgerond, annuleren

**Als** medewerker **wil ik** de verkoop met één knop per stap afwerken, **zodat** ik geen statuskeuzelijst meer nodig heb.

1. **Stap 3 In productie** (K08): per inlijsting de voortgang (lijst besteld / binnen / ingelijst / nagekeken), lijsten bij de leverancier met "Lijst bestellen", planning, notitie voor het atelier. Hoofdknop "Klaar om af te halen ▶" met hint "maakt de bestelbon aan".
2. **Stap 4 Klaar** (K10): melding "Bestelbon 2026-19 is aangemaakt…", inhoud van de bon, klant, "Afhalen wijzigen", "Bestelbon afdrukken". Hoofdknop "Klant komt afhalen ▶".
3. **Stap 5 Afhalen en betalen** (K12): totaal / voorschot / al betaald / nog te betalen, bedrag nu (deel betalen mag), betaalwijze, wisselgeld bij Cash, betalingen in deze verkoop, "Meegeven" (afvinklijst). Knoppen "Afgehaald, later betalen" en "Betalen en afhalen ▶".
4. **Afgerond** (K13): verloop van de verkoop (tijdlijn), documenten met "Afdrukken", betalingen; "Nieuwe verkoop voor deze klant".
5. **Stap overslaan** (K09): dialoog met de over te slane stappen, reden (keuzelijst: *Standaardkader uit voorraad*, *Klant neemt meteen mee*, *Andere*), voorbeeld van de stappenbalk na overslaan. Alleen beschikbaar vanuit stap 2 en 3.
6. **Annuleren** via ⋯: bevestiging met reden; daarna alleen-lezen met label "Geannuleerd".

Technisch: kind-ViewModels per stap; afrekenlogica uit `WerkBonDetailViewModel` (US-76) verhuist naar `StapAfhalenViewModel` en `VerkoopDossierService.RekenAfEnHaalAfAsync`. Tijdlijn K13: afgeleid uit bestaande datums (offerte, werkbon `AangemaaktOp`/`BijgewerktOp`, `WerkTaak`, `LeverancierBestelLijn`, `Ontvangst`) — geen nieuwe tabel; de audit-log is optioneel aanvullend.

---

### US-94 — Afdrukken werkbon / bestelbon

**Als** medewerker **wil ik** elk document met één knop afdrukken of als PDF bewaren, **zodat** ik niet hoef te zoeken waar het bestand terechtkwam.

1. Elk "… afdrukken" opent een voorbeeldvenster met de echte PDF en de knoppen Afdrukken, Bewaren als PDF, Sluiten; het pad (`Documenten › QuadroApp › Bestelbonnen › Bestelbon-2026-19.pdf`) staat eronder.
2. ~~Offerte-PDF~~ — vervallen (27/10): de bestelbon is het prijsvoorstel.
3. Werkbon-PDF (nieuw): zonder prijzen, per inlijsting maat, lijst/kader, glas, inleg, geplande datum en afvinkvakjes; optie "Prijzen tonen".
4. Bestelbon afdrukken kan in elke status; een betaalde bon toont "Betaald op …".
5. Standaardkader staat met naam + omschrijving + afmeting op elk document (US-80).

Technisch: `IDocumentAfdrukService` + `DocumentPreviewWindow` (QuestPDF `GenerateImages`), `PdfWerkBonExporter` (nieuw, opbouw zoals `PdfWeekLijstExporter`), vaste map via `Environment.SpecialFolder.MyDocuments`. `FactuurWorkflowService.MarkeerKlaarVoorExportAsync` en de exportstatus-check verdwijnen uit de UI-flow (de service-methodes blijven voor Excel-export).

---

### US-95 — Navigatie opruimen, Home

**Als** medewerker **wil ik** een korte zijbalk met de woorden die ik zelf gebruik, **zodat** ik de werkbon niet meer kwijt ben (P1, P13).

1. Zijbalk: **Verkopen · Winkelverkopen · Planning · Overzicht betalingen**, daaronder Klanten · Lijsten · Standaardkaders · Afwerkingen · Leveranciers · Instellingen.
2. Prijsberekening, Bestellingen en Bestelbonnen verdwijnen uit de zijbalk en van Home.
3. Home toont "Vandaag af te halen" en "Nog te betalen" (zelfde query als K01) naast de voorraadmeldingen; meldingen met Nederlandse tekst in plaats van "BelowMinimum".
4. Leveranciersbestellingen heten overal "Lijst bestellen" / "Bestelling bij leverancier" (P1).
5. `OffertesLijstView`, `WerkBonLijstView`, `WerkBonDetailWindow`, `FacturenView` worden verwijderd zodra niets er nog naar verwijst; het archief blijft bereikbaar via Instellingen.

## Volgorde en risico's

- **Fase A (US-87, 88)** heeft geen zichtbare UI-wijziging behalve betaalwijzen → veilig om eerst te releasen; de stapbepaling is volledig getest vóór er een scherm op steunt.
- **Fase B (US-89–91)**: Verkopen en het dossier staan *naast* de oude schermen. Veerle kan testen zonder dat iets verdwijnt.
- **Fase C (US-92–94)**: planner en afdrukken. Grootste risico: `PlanningCalendarWindow` → UserControl (drag & drop in code-behind) en het opsplitsen van `OfferteView.axaml` (1 057 regels). Eerst verhuizen zonder gedrag te veranderen, dan pas in het dossier hangen.
- **Fase D (US-95)**: pas na akkoord van Veerle de oude schermen weg.
- Bestaande data: elke bestaande offerte krijgt automatisch een stap via de resolver; geen datamigratie nodig behalve betaalwijzen. Offertes die vandaag op InProductie staan met werkbon Gepland (P10) tonen stap 2 — klopt met de werkelijkheid.

## Open vragen (voor Veerle / Anthony)

1. **Vorige stap**: alleen terug *bekijken* (zoals het prototype), of moet een stap ook echt teruggezet kunnen worden (bv. bestelling terug naar offerte omdat de klant nog iets wijzigt)? Dit plan gaat uit van alleen bekijken; wijzigen van de offerte na bevestigen → aparte story.
2. ~~Cheque en Proton~~ — beslist in US-79: worden Cash.
3. **Annuleren met voorschot**: wordt het voorschot terugbetaald (negatieve ontvangst *Correctie*) of blijft het staan?
4. **Printer en exemplaren** in de app (K04/K11 tonen een printerkeuze en "Exemplaren 2"): nodig, of volstaat de printdialoog van macOS? Rechtstreeks afdrukken vanuit Avalonia is niet cross-platform; via de systeemviewer wel.
5. **Bestelbon bij overslaan**: overslaan van Bestelling + Productie maakt meteen de bestelbon aan (K09). Ook als alleen Bestelling wordt overgeslagen? Plan: nee, die ontstaat bij "Klaar".
6. **Afhaaldatum vóór besteldatum** (P7): waarschuwing in stap 1 of stap 2?
7. **Verzonden** (US-56): mag het afdrukken van de bestelbon in stap 1 de offerte automatisch op Verzonden zetten, zodat de handmatige statusknoppen weg kunnen?
8. **Werkbonvenster** (US-51): akkoord dat het modale werkbonvenster, gekozen op 29/07, verdwijnt ten voordele van het dossier?
9. **Annuleren na Klaar**: de transitietabel laat Geannuleerd vandaag alleen toe tot InProductie. Moet een verkoop met een bestelbon (Afgewerkt/Besteld) ook geannuleerd kunnen worden?
10. ~~Nummering bestelbon~~ — beslist: volgnummer pas na bevestiging (zie bovenaan).
