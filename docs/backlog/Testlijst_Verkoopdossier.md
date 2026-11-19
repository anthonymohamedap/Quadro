# Testlijst verkoopdossier (US-87 → US-95, US-102)

Voor Anthony en Veerle, op de branch `dev` na het mergen van #123 → #124 → #125 → #126 (in die volgorde).
Vink af wat klopt; noteer wat niet klopt met het verkoopnummer erbij.

Grondregel: **niets uit de oude schermen mag verdwijnen.** Sinds de laatste stap (US-95, 08/11) zijn het
oude offertescherm, Werkbonnen (+ werkbonvenster), Bestelbonnen en het oude planningsvenster weg; elke
functie zit in het dossier (zie 10).

## 0. Opstarten

- [ ] De app start; de database wordt bijgewerkt (nieuwe kolommen, geen gegevens weg).
- [ ] Een oude offerte met nummer 0 heeft nu een echt nummer (ook "Werkbon 0" in de planning is weg).

## 1. Verkopen (menu "Verkopen")

- [ ] Vijf tabbladen met aantallen: Open offertes · Lopende werkbonnen · Klaar om af te halen · Afgerond · Geannuleerd.
- [ ] Zoeken op nummer, klant **en bestelbonnummer**; "Zoek in alle tabbladen"; jaarfilter.
- [ ] Kolom Bestelbon (nummer of "voorlopig"); onder de fase de werkbon ("Werkbon 423 · Gepland · 2 taken · 1/2 besteld").
- [ ] **Openen** of dubbelklik → het **dossier**.
- [ ] Lopende werkbonnen: Planning openen (pagina) en Weeklijst afdrukken.

## 2. Dossier — kop en stappenbalk

- [ ] Kop: verkoopnummer · klant, adres/telefoon, afhaaldatum, fase; rechts totaal en nog te betalen.
- [ ] Chips Offerte / Werkbon / Bestelbon / Voorschot; doorgestreept zolang ze niet bestaan.
- [ ] Klik op een vorige stap of "◀ Vorige stap" = enkel bekijken ("Terug gekeken"); "Naar huidige stap ▶".
- [ ] Menu ⋯: Opslaan, Berekenen, → Verzonden / ↩ Concept, Werkbon afdrukken met prijzen, Lijsten bestellen…, Bestelbon en betalingen…, Verkoop annuleren….

## 3. Stap 1 Offerte

- [ ] Links "Inlijsting details" (alle velden zoals het offertescherm), rechts klant, Inlijstingen, prijsaanpassingen + voorschot, totalen.
- [ ] Iets wijzigen en een andere stap kiezen → automatisch bewaard ("bewaard ✓").
- [ ] "Bestelbon aanmaken/afdrukken" werkt zoals vroeger.
- [ ] "Bevestigen → Werkbon ▶" → je komt in stap 2 (geen planningvenster meer).
- [ ] Na "Bevestigen → Werkbon" staat de chip **Bestelbon 2026-…** er meteen (definitief bij het akkoord); bij "Klaar" blijft het hetzelfde nummer.
- [ ] "+ Nieuwe verkoop" opent stap 1 van het dossier; na bewaren krijgt de verkoop zijn nummer.
- [ ] Na het akkoord: stap 1 terugkijken is alleen-lezen; "✎ Offerte aanpassen…" maakt hem bewerkbaar.

## 4. Stap 2 Werkbon (planning)

- [ ] Cijfers bovenaan; 2 weken / Maand; per dag uren, % en kleur (rustig/vol/geblokkeerd); ook zaterdag/zondag.
- [ ] Inlijstingen rechts: selecteren + "Plan op …", "Plan per regel", slepen naar een dag.
- [ ] Gepland: herplannen en ✕; een lange inlijsting toont elk deel apart.
- [ ] Dagdetails (ook andere verkopen, Herplannen/Verwijderen), weekdetail, dag/week blokkeren.
- [ ] Afhaaldatum = laatste plandag; de stap blijft Werkbon na inplannen.
- [ ] "Start productie ▶" zonder volledige planning of afhaaldatum → melding wat ontbreekt.
- [ ] De weeklijst van die week afdrukken → volledig geplande verkopen gaan naar In productie.
- [ ] "⤼ Stap overslaan" → reden kiezen → meteen Klaar om af te halen (stappen doorgestreept).
- [ ] "🖨 Werkbon afdrukken" → PDF zonder prijzen (Documenten › QuadroApp › Werkbonnen).

## 5. Stap 3 In productie

- [ ] Werktaken zoals in het werkbonvenster (titel, code, maat, inleg, duur, van/tot, lijst/kader, afwerking, opmerking).
- [ ] "Bestelling plaatsen" met bestelwijze werkt; status wordt "Besteld op …".
- [ ] "✎ Offerte aanpassen…" opent stap 1 bewerkbaar.
- [ ] "Klaar om af te halen ▶" → bevestigen → bestelbon met nummer, stap 4.

## 6. Stap 4 Klaar om af te halen

- [ ] Bestelbon: datums, opmerking, initialen, lijnen, Opslaan (zolang open).
- [ ] Betalingen: registreren, ✕, Rest betaald.
- [ ] "🖨 Bestelbon afdrukken" werkt in elke status.
- [ ] "Klant komt afhalen ▶" → afrekenen: bedrag, betaalwijze, datum, gesplitst; Annuleren / Later betalen / Betalen en afhalen.

## 7. Stap 5 en afgerond

- [ ] Later betalen → stap 5 met "Afrekenen ▶" en de rest bovenaan.
- [ ] Alles betaald → "Afgehaald en volledig betaald…" + Ontgrendelen (enkel beheerder).
- [ ] Kaart "Momentopname bij afronden": verandert niet als je daarna een lijst of klant hernoemt.

## 8. Annuleren

- [ ] Menu ⋯ → Verkoop annuleren…: reden verplicht; voorschot terugbetalen (betaalwijze) of houden.
- [ ] Na afhalen of betalen kan annuleren niet meer.
- [ ] Terugbetaling staat in Overzicht betalingen.

## 9. Planning (menu)

- [ ] Planning opent als pagina met dezelfde opmaak als stap 2 (zonder inlijstingen), met "Terug".
- [ ] Vanuit Verkopen (tab Lopende werkbonnen → Planning openen): met de inlijstingen van die werkbon; "Terug" naar Verkopen.

## 10. Oude schermen zijn weg — alles terug te vinden?

- [ ] Menu: geen Werkbonnen en Bestelbonnen meer; beginscherm met Verkopen en Leveranciers.
- [ ] Werkbonnenlijst → Verkopen, tab Lopende werkbonnen (werkbon, status, taken, bestelvoortgang, Planning, Weeklijst).
- [ ] Werkbonvenster → stap 3 (werktaken, Bestelling plaatsen), stap 4/5 (afrekenen); in stap 2 "Bestellen…" / menu ⋯ "Lijsten bestellen…".
- [ ] Bestelbonnen → stap 4/5, en in elke stap menu ⋯ "Bestelbon en betalingen…" (gegevens, lijnen, Opslaan, Afdrukken, betalingen, Rest betaald); zoeken op bestelbonnummer in Verkopen.
- [ ] Oud offertescherm → stap 1.
