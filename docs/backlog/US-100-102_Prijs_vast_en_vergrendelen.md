# US-100 → US-102 — Afgesproken prijs blijft staan, afgeronde verkoop wijzigt niet meer

Stand 31/10/2026. Vraag Anthony: een offerte die helemaal afgehandeld en betaald is, mag niet meer veranderen door wijzigingen elders in de database (prijzen of namen van lijsten, afwerkingen, standaardkaders, klantgegevens).

## Analyse (31/10)

Al vastgelegd: de **bestelbon** bewaart per lijn omschrijving en prijzen; een afgedrukte of betaalde bon wordt niet meer opnieuw opgebouwd. Klantnaam en adres staan als kopie op de bon. De prijzen per inlijsting worden bij de offerte opgeslagen.

Niet vastgelegd:
1. **Elke herberekening gebruikt de huidige catalogus.** "Bestelbon bekijken" deed altijd eerst *Berekenen + Opslaan* (`OfferteWorkflowViewModel.OpenFactuurAsync`), ook bij een betaalde offerte. Na een prijswijziging van een lijst/afwerking/standaardkader veranderde zo het totaal van een afgeronde offerte, terwijl de betaalde bestelbon het oude bedrag hield.
2. Omschrijvingen (lijstnummer, afwerkingsnamen, standaardkader, klant) worden in offerte, planning en werkbon live uit de catalogus gelezen.
3. Een afgeronde offerte bleef volledig bewerkbaar.

## US-100 — Prijs vast na bevestiging ✅

**Als** medewerker **wil ik** dat de offerte live meerekent zolang de klant niet bevestigd heeft, maar dat de afgesproken prijs daarna blijft staan, **zodat** een prijswijziging in de catalogus geen lopende of afgeronde verkoop verandert.

1. Vóór "Bevestigen als bestelling" (stap 1, ook met een voorlopige bestelbon) rekent de offerte zoals voorheen automatisch mee.
2. Vanaf de bevestiging (stap 2 en verder) gebeurt er geen automatische herberekening meer: niet na een wijziging, niet vóór "Bestelbon bekijken".
3. De knop "Berekenen" heet dan "Prijzen herberekenen" en vraagt eerst bevestiging ("het totaal kan veranderen").
4. Onderaan staat: "De klant heeft bevestigd: de prijzen staan vast en worden niet meer automatisch herberekend."

Technisch: `OffertePrijsViewModel` krijgt `isPrijsVast`; de stille herberekening doet dan niets, de expliciete vraagt bevestiging. `OfferteWorkflowViewModel.IsPrijsVast` (= `IsPrijsVastNaBevestiging(stap)`) wordt gezet in `ToonStappen`.

> Let op: wijzig je na de bevestiging nog een inlijsting (bv. ander glas), dan verandert de prijs pas na "Prijzen herberekenen" — en die herberekent **alle** inlijstingen met de huidige catalogusprijzen. Een afgesproken prijs per inlijsting blijft altijd voorrang hebben.

## US-101 — Afgeronde verkoop vergrendeld ✅

**Als** zaakvoerder **wil ik** dat een verkoop die afgehaald en volledig betaald is niet meer gewijzigd kan worden, **zodat** wat de klant betaald heeft en wat in de app staat altijd overeenkomt.

1. Afgehaald + bestelbon betaald → de offerte is alleen-lezen: klant, inlijstingen, editor, "Berekenen" en "Opslaan" zijn uitgeschakeld; lezen en scrollen kan wel.
2. Opslaan wordt ook in de code geweigerd (niet enkel de knop).
3. "Bestelbon bekijken" werkt nog (afdrukken), zonder valideren, herberekenen of opslaan.
4. Melding onderaan: "Afgehaald en volledig betaald: deze verkoop is afgerond en kan niet meer gewijzigd worden."
5. Een beheerder (recht *Gebruikers beheren*) kan **ontgrendelen** na bevestiging, enkel zolang de offerte open staat; elke wijziging daarna staat in het auditlogboek. De betaalde bestelbon verandert niet mee.
6. Afgehaald maar nog niet volledig betaald → niet vergrendeld (afrekenen moet nog kunnen).

## US-102 — Momentopname bij afronden (later)

**Als** zaakvoerder **wil ik** dat een afgeronde verkoop er altijd uitziet zoals op het moment van afronden, **zodat** ook het hernoemen of verwijderen van een lijst, afwerking, standaardkader of klant oude verkopen niet verandert.

- Bij "afgehaald en volledig betaald" een JSON-momentopname bewaren van offerte, klant, inlijstingen (met namen, omschrijvingen en prijzen) en werkbon — hetzelfde mechanisme als `OfferteArchiefSnapshot` / `SnapshotOfferteRegel`.
- Afgeronde verkopen tonen en drukken af vanaf die momentopname.
- Hoort bij het verkoopdossier: K13 "Afgerond" (US-93).

⏱ Schatting US-102: 6–8u.
