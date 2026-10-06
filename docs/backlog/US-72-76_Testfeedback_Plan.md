# Plan — testfeedback ontvangsten & winkelverkopen (US-72 → US-76)

Stand 06/10/2026, na de eerste test van US-66/67/68/69/70 (alle tests groen, Npgsql zonder openstaande modelwijzigingen).

## Uitgangspunt: standaardkader blijft in de offerte

Veerle's eerste mail ("kant-en-klare kaders uitboeken: datum, hoeveelheid, prijs incl. btw") is achterhaald door haar antwoorden van 27/09 (zie `US-66-70_Ontvangsten_Plan.md`):

- **Vraag 8**: kant-en-klaar in de offerte niet weghalen, wel hernoemen naar "standaardkader" (gedaan in US-68).
- **Vraag 5**: een los verkocht standaardkader gaat via de kassa (Winkelverkopen). Inlijsten in een standaardkader, of er een bestellen, komt op de bestelbon. Nooit allebei.

Wat ze in die eerste mail vroeg, is het scherm **Winkelverkopen** (US-66) samen met het **Overzicht betalingen** (US-67).

**Wat de foto van de oude kassa leert:**

- De kolom "artikel" is overal leeg; ze typen de omschrijving zelf. Een artikel-keuzelijst is dus geen must.
- De btw staat op code 2 = altijd 21 % (bevestigd).
- "Saldo" is niet nodig (een winkelverkoop is altijd meteen betaald); "terug" (wisselgeld) wel → US-74.

## Al opgelost tijdens de test

- Afronding van het winkelverkoop-totaal: commercieel afronden, dus 89,865 wordt 89,87 (commit `a9646d1`).
- **Standaardkader ging verloren bij het opslaan van een offerte**: daardoor viel de prijs naar € 0 en braken de planning en de bestelbon. Het kader wordt nu mee opgeslagen, ook bij regel dupliceren en archief/terugzetten (commit `d38cc32`). Al sinds US-58 → offertes met een standaardkader die al opgeslagen zijn, moeten opnieuw nagekeken worden.

## Stories (volgorde)

| # | Story | ± |
|---|---|---|
| 1 | **US-72** Testfixes winkelverkopen & taal | 1–1,5u |
| 2 | **US-73** Betalingen op de bestelbon-PDF | 1–2u |
| 3 | **US-74** Wisselgeld in Winkelverkopen | 0,5u |
| 4 | **US-75** (optioneel) Standaardkaders: prijs incl. btw | 1u |
| 5 | **US-76** Afrekenen bij afhaling vanuit de werkbon | 2–3u |

Totaal ± 5,5–8u.

---

### US-72 — Testfixes winkelverkopen & taal

**Als** medewerker **wil ik** dat Winkelverkopen en het overzicht betalingen er net en in het Nederlands uitzien, **zodat** ik ze vlot kan gebruiken.

Acceptatiecriteria:

1. Winkelverkopen: zoekvak en datums op één rij, knoppen (Deze maand, Vorige maand, Alles, Nieuw, Ververs) op een eigen rij en volledig leesbaar. Het zoekvak heeft een normale hoogte.
2. Datums overal in het Nederlands: datumkiezer (dag/maand/jaar, "oktober"), "Detail van dinsdag 06/10/2026". De app zet bij het opstarten de taal op nl-BE.
3. Aantal toont "1" i.p.v. "1,0" (decimalen alleen als ze er zijn, bv. "1,5").
4. Een betaling verwijderen op een bestelbon die op Betaald stond, zet de bestelbon terug op de vorige status als er weer een rest openstaat.
5. Controle: de twee PDF's van het overzicht betalingen opnieuw afdrukken na het invoeren van meerdere betalingen. Ze moeten hetzelfde totaal tonen als het scherm (bij de test toonden ze € 222 tegenover € 750,68, waarschijnlijk te vroeg afgedrukt).

Technisch: `WinkelVerkopenView.axaml` (filtergrid → 2 rijen), cultuur instellen in `Program.cs`/`App` (`CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrent*`), `StringFormat` voor aantal, `OntvangstService.VerwijderAsync` + `FactuurWorkflowService` voor de status.

---

### US-73 — Betalingen op de bestelbon-PDF

**Als** medewerker **wil ik** dat de bestelbon toont wat er al betaald is, **zodat** de klant bij het afhalen het juiste bedrag ziet.

Acceptatiecriteria:

1. Onder "Totaal (incl. BTW)" komen het voorschot én elke geregistreerde betaling: datum, betaalwijze, bedrag (bv. "Betaald 06/10 – storting  − € 37,05").
2. "Te betalen bij afhalen" = de rest (totaal − voorschot − betalingen).
3. Is de rest 0, dan staat er **"Betaald"** i.p.v. "Te betalen bij afhalen € 0,00".
4. Oude bestelbonnen zonder betalingsregels blijven er hetzelfde uitzien.

Technisch: `PdfFactuurExporter` krijgt de betalingen mee (via `IOntvangstService.GetBetalingenAsync`/`GetBetaalStandAsync`); test op de render van de rest.

---

### US-74 — Wisselgeld in Winkelverkopen

**Als** medewerker aan de kassa **wil ik** bij een cash-betaling het ontvangen bedrag invullen en zien hoeveel ik moet teruggeven, **zodat** ik niet zelf moet rekenen.

Acceptatiecriteria:

1. In het detail van een winkelverkoop staat het veld "Ontvangen", alleen zichtbaar bij betaalwijze Kontant.
2. "Terug" = ontvangen − totaal, live berekend, nooit negatief. Is het ontvangen bedrag te laag, dan staat "nog te ontvangen € X".
3. Ontvangen en terug worden **niet** opgeslagen; het geboekte bedrag blijft het totaal.

Technisch: alleen ViewModel en View (proxy-properties in `WinkelVerkopenViewModel`), geen migratie.

---

### US-75 (optioneel) — Standaardkaders: prijs incl. btw

**Als** medewerker **wil ik** de prijs van een standaardkader inclusief btw ingeven en zien, **zodat** het overeenkomt met de winkelprijs.

Acceptatiecriteria:

1. Het scherm Standaardkaders toont en bewerkt "Prijs per stuk (incl. 21 % btw)".
2. In de database blijft het bedrag exclusief btw bewaard (de offerte rekent exclusief); omrekenen = incl ÷ 1,21.
3. Bestaande prijzen blijven ongewijzigd; incl. → excl. → incl. geeft hetzelfde bedrag terug (afronding nakijken, eventueel meer decimalen voor de excl-prijs → migratie SQLite + Npgsql).

Eerst bij Veerle navragen of dit nog nodig is.

---

### US-76 — Afrekenen bij afhaling vanuit de werkbon

**Als** medewerker **wil ik** bij het afhalen van een inlijsting meteen kunnen afrekenen, **zodat** de betaling en de status in één keer goed staan.

Acceptatiecriteria:

1. Zet ik een werkbon op "Afgehaald" en staat er op de bestelbon nog een rest open, dan verschijnt: "Afrekenen: nog € X te betalen" met bedrag (standaard de rest), datum (vandaag) en betaalwijze.
2. Betalen → betaling geregistreerd op de bestelbon (komt in het overzicht betalingen) en de werkbon op Afgehaald; is de rest 0 → bestelbon op Betaald. Alles in één keer: lukt één stap niet, dan verandert niets.
3. "Later betalen" → werkbon wel op Afgehaald, bestelbon blijft open.
4. Een gesplitste betaling (deels cash, deels kaart) kan: na de eerste betaling vraagt hij de resterende rest.
5. Geen bestelbon of rest 0 → geen vraag, gewoon Afgehaald.

Technisch: `WerkBonWorkflowService`/`WorkflowService.ChangeWerkBonStatusAsync` + `IOntvangstService.RegistreerBetalingAsync` in één transactie; dialoog in het werkbon- en planningsscherm.
