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

`Betaalwijze` (opgeslagen als string): enum wordt `Bancontact, Cash, CreditCard, Overschrijving` met data-update in dezelfde migratie: `Kontant→Cash`, `Visa→CreditCard`, `Storting→Overschrijving`. `Cheque` en `Proton` blijven als `[Obsolete]`-waarden bestaan voor historische ontvangsten, maar staan niet meer in keuzelijsten (zie open vraag 2).

Migratie via `.\Scripts\add-migration.ps1 Verkoopdossier` (SQLite + Npgsql), nooit handmatige SQL.

### 4. Eén orchestrator: `VerkoopDossierService`

Nieuwe service bovenop de bestaande (`IWorkflowService`, `IWerkBonWorkflowService`, `IFactuurWorkflowService`, `IOntvangstService`, `IStockService`). Elke knop in de stappenbalk roept precies één methode aan, in één transactie:

| Methode | Doet | Vervangt |
|---|---|---|
| `StartVerkoopAsync(klantId)` | maakt offerte + kent nummer meteen toe | "Nieuwe offerte" (P7: geen "offertenr. 0" meer) |
| `BevestigAlsBestellingAsync(id)` | offerte → Goedgekeurd, werkbon aanmaken | "Bevestigen → Bestelling" |
| `StartProductieAsync(id)` | werkbon → InUitvoering, offerte → InProductie | inplannen zette offerte al op InProductie (P10) |
| `MeldKlaarAsync(id)` | werkbon → Afgewerkt, bestelbon aanmaken (zichtbaar gemeld), offerte → Afgewerkt → Besteld | onzichtbare bestelbon (P3) |
| `SlaStappenOverAsync(id, stappen, reden)` | zet de tussenliggende werkbonstatussen in één keer, bewaart flags + reden, maakt bestelbon | nieuw |
| `RekenAfEnHaalAfAsync(id, bedrag, betaalwijze, ontvangen?)` | hergebruikt US-76-logica (betaling + Afgehaald, gesplitst of later) | afrekenblok werkbon |
| `HaalAfZonderBetalenAsync(id)` | werkbon → Afgehaald, rest blijft open | idem |
| `AnnuleerAsync(id, reden)` | offerte + bestelbon → Geannuleerd, planning-taken weg, voorraadreservaties vrij (`ReleaseReservationsForWerkBonAsync`) | "Naar archief" als enige uitweg (P12) |

**Statussynchronisatie**: `OfferteStatusPropagation` blijft, maar de aanroep in `WerkBonWorkflowService` bij *inplannen* (regels 111/226/291–302) verdwijnt; offerte → InProductie gebeurt alleen nog in `StartProductieAsync`. De transitietabellen in `WorkflowService` blijven de bewaker; `SlaStappenOverAsync` doorloopt ze stap voor stap binnen één transactie in plaats van ze te omzeilen.

De handmatige knoppen "↩ Concept" / "↩ Verzonden" (P15) en de werkbon-statuskeuzelijst (P4) verdwijnen.

### 5. Afdrukken: één `IDocumentAfdrukService`

`ToonEnDrukAsync(DocumentSoort soort, int offerteId)` voor Offerte, Werkbon en Bestelbon:

- bouwt de PDF met de bestaande exporters (`PdfOfferteExporter`, `PdfFactuurExporter`) + een nieuwe `PdfWerkBonExporter` (zonder prijzen, afvinkvakjes, één blok per inlijsting — K07);
- toont een voorbeeldvenster (QuestPDF `GenerateImages()` → `Image`-lijst in Avalonia);
- "Bewaren als PDF" → vaste map `Documenten/QuadroApp/{Offertes|Werkbonnen|Bestelbonnen}/<Soort>-<nr>.pdf`;
- "Afdrukken" → bestand bewaren en openen met de systeemviewer (`IPathOpener`), waar macOS/Windows de printerdialoog geven. Printerkeuze en aantal exemplaren in de app zelf (K04/K07/K11) is fase 2 (zie open vraag 4).

Bestelbon afdrukken werkt **in elke status** (P2, US-82); de knoppen "Markeer als klaar voor export" en "Export PDF" verdwijnen. Draft / KlaarVoorExport / Geexporteerd worden in de UI samen "Open" (P9).

### 6. Planning: Window → UserControl

`PlanningCalendarWindow` wordt `PlanningView` (UserControl) met dezelfde `PlanningCalendarViewModel`. Twee gastheren:

- **K14** zijbalk "Planning" → `NavigateToAsync<PlanningCalendarViewModel>()` (overzicht, geen regels te plannen);
- **K06** stap 2 van het dossier → de view met de werkbonregels van deze verkoop.

De drie `new PlanningCalendarWindow` (`HomeViewModel:123`, `OfferteWorkflowViewModel:170/215`) verdwijnen. `WeekWerkLijstWindow` mag een venster blijven (afdrukbare weeklijst).

## Stories (volgorde)

| # | Story | Fase | ± |
|---|---|---|---|
| 1 | **US-87** Fundament: stapbepaling, datamodel, `VerkoopDossierService` | A | 5–7u |
| 2 | **US-88** Betaalwijzen Bancontact / Cash / Credit card / Overschrijving (= US-79) | A | 1,5–2u |
| 3 | **US-89** Scherm Verkopen (K01) + Nieuwe verkoop (K02) | B | 4–6u |
| 4 | **US-90** Dossier-schil: kop, documentchips, stappenbalk, vorige stap bekijken | B | 4–5u |
| 5 | **US-91** Stap 1 Offerte in het dossier + voorschot (K03, K05) | B | 4–6u |
| 6 | **US-92** Planning als pagina en in stap 2 (K06, K14) | C | 4–6u |
| 7 | **US-93** Stappen 3–5, overslaan, afgerond, annuleren (K08–K10, K12, K13, K09) | C | 6–8u |
| 8 | **US-94** Afdrukken offerte / werkbon / bestelbon (K04, K07, K11) | C | 5–7u |
| 9 | **US-95** Navigatie opruimen, Home, oude schermen weg | D | 2–3u |

Totaal ± 36–50u. Elke story = eigen feature-branch + PR; na fase B is er al een bruikbaar dossier naast de oude schermen.

Koppeling met de lopende stories US-79–86: US-79 → US-88; US-80 (standaardkaderinfo) wordt in US-91/94 meegenomen; US-81 → US-87/91; US-82 → US-94; US-83 → US-87/93; US-84 → US-89; US-85 (namen) → US-89/90/95; US-86 → US-95.

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

### US-88 — Betaalwijzen (= US-79)

**Als** medewerker **wil ik** enkel Bancontact, Cash, Credit card en Overschrijving zien, **zodat** de keuze klopt met hoe klanten echt betalen.

1. Overal dezelfde vier, in die volgorde: voorschot, afrekenen, winkelverkoop, betalingen op de bestelbon.
2. Wisselgeldveld "Ontvangen (cash)" verschijnt bij Cash.
3. Overzicht betalingen: vier kolommen. Oude Cheque/Proton-ontvangsten tellen nog mee in een kolom "Andere" die alleen verschijnt als er zo'n ontvangst in de periode is.
4. Bestaande ontvangsten worden omgezet (Kontant→Cash, Visa→Credit card, Storting→Overschrijving).

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
5. Voetknoppen: "Offerte afdrukken" en "Klant akkoord: bevestigen als bestelling ▶".

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

### US-94 — Afdrukken offerte / werkbon / bestelbon

**Als** medewerker **wil ik** elk document met één knop afdrukken of als PDF bewaren, **zodat** ik niet hoef te zoeken waar het bestand terechtkwam.

1. Elk "… afdrukken" opent een voorbeeldvenster met de echte PDF en de knoppen Afdrukken, Bewaren als PDF, Sluiten; het pad (`Documenten › QuadroApp › Bestelbonnen › Bestelbon-2026-19.pdf`) staat eronder.
2. Offerte-PDF toont "Offertenr. 412" (nooit 0), geldig-tot-datum en optioneel voorschot en rest.
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
2. **Cheque en Proton**: zijn er nog historische ontvangsten mee die in het overzicht moeten blijven? Anders kunnen ze volledig weg.
3. **Annuleren met voorschot**: wordt het voorschot terugbetaald (negatieve ontvangst *Correctie*) of blijft het staan?
4. **Printer en exemplaren** in de app (K04/K11 tonen een printerkeuze en "Exemplaren 2"): nodig, of volstaat de printdialoog van macOS? Rechtstreeks afdrukken vanuit Avalonia is niet cross-platform; via de systeemviewer wel.
5. **Bestelbon bij overslaan**: overslaan van Bestelling + Productie maakt meteen de bestelbon aan (K09). Ook als alleen Bestelling wordt overgeslagen? Plan: nee, die ontstaat bij "Klaar".
6. **Afhaaldatum vóór besteldatum** (P7): waarschuwing in stap 1 of stap 2?
