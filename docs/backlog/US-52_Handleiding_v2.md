# US-52 · Handleiding v2 — visueel, knop-voor-knop, foolproof

> **Bedoeld voor een aparte agent.** Deze story is zelfstandig te volgen. Doel: de bestaande
> gebruikershandleiding uitbreiden tot een écht duidelijke, visuele handleiding met schermafbeeldingen,
> waarin elke knop en functie benoemd wordt én de macOS install/update-stappen (toestemmingen +
> wachtwoord) staan, zodat een niet-technische gebruiker niets kan stukmaken.
>
> **Bestaat al:** `docs/QuadroApp_Handleiding.docx` (v1, tekst, 18 hoofdstukken). Gebruik dit als
> basis en breid het uit — niet vanaf nul herbeginnen.

---

## Story

**Als** niet-technische gebruiker (Veerle, Kurt) **wil ik** een handleiding met duidelijke
schermafbeeldingen waarin elke knop en actie stap-voor-stap wordt getoond, inclusief wat er bij
het installeren/updaten op de Mac gebeurt, **zodat** ik de app zelfstandig kan gebruiken en niets
per ongeluk kan stukmaken.

## Doelgroep & toon

- Volledig niet-technisch. Geen jargon; elk begrip één keer uitleggen.
- Elke handeling als genummerde stappen ("klik op …", "typ …", "kies …").
- Geruststellend: benoem expliciet wat veilig is en wat je beter niet doet.

---

## Wat moet erbij komen (t.o.v. v1)

### 1. Schermafbeeldingen bij elk scherm en elke belangrijke knop
- Van **elk** scherm minstens één screenshot; bij complexe schermen meerdere.
- **Elke knop/functie** die in de tekst genoemd wordt, ook tonen op een afbeelding.
- Gebruik **annotaties** (genummerde bolletjes of pijlen) die verwijzen naar de stappen in de tekst.
- Consistent formaat: zelfde vensterbreedte, zelfde zoom, bij voorkeur de echte app in het Nederlands.
- Anonimiseer klantgegevens op screenshots (gebruik testdata, geen echte klantnamen/adressen).

> **Screenshots aanleveren:** de agent kan zelf geen screenshots van de desktop-app maken. Vraag
> Anthony om per scherm een screenshot te bezorgen (of lever een genummerde **shotlist** op met exact
> welke schermen/knoppen nodig zijn, zodat Anthony ze in één keer kan maken). Plaats de beelden in
> `docs/handleiding/img/` en verwijs ernaar in het document.

### 2. Knop-voor-knop dekking per scherm
Werk **elk** scherm uit met: wat je ziet, elke knop/functie, en een stap-voor-stap voor de
belangrijkste taken. Te dekken schermen (volledige lijst):

- Inloggen · Wachtwoord wijzigen · Automatische vergrendeling
- Beginscherm (Home): meldingen (voorraad, lage voorraad, tekorten, te late bestellingen) + alle tegels
- Klanten + Klantdetail
- Offerte maken (regels/inlijstingen, afwerkingen: passe-partout, glas, rug, opkleven, dieptekern + varianten, prijs, korting, opslaan, lijstbeheer)
- Offertes-overzicht + statusflow (Concept → Verzonden → Goedgekeurd → In productie → Afgewerkt → Besteld → Betaald / Geannuleerd)
- Planning-kalender (regel naar dag slepen, dag/week blokkeren, taak herplannen/verwijderen, weekdetail)
- Werkbonnen-lijst + statusfilter + werkbon-detailvenster (status wijzigen, lijst als besteld, open offerte); werkbonstatus (Gepland → In uitvoering → Afgewerkt → Afgehaald)
- Bestelbonnen + PDF-preview
- Lijsten/voorraad (nieuw, bewerken, verwijderen, Excel-import, bulk-prijs-update)
- Leveranciers + bestellingen (bestelling maken, lijn ontvangen, annuleren, paginering)
- Afwerkingen (groepen + varianten, Excel-import)
- Archief
- Export Center (velden/entiteiten kiezen, map kiezen, exporteren, map/laatste export openen)
- Instellingen (uurloon, standaard prijs/meter, winstfactor, afvalpercentage)
- Gebruikersbeheer (admin: gebruiker maken, actief/inactief) + auditlog

### 3. NIEUW hoofdstuk: "Installeren en updaten op de Mac" (foolproof)
Dit is het belangrijkste toevoegingsdoel. Beschrijf met screenshots exact wat de gebruiker ziet en
moet doen, zodat ze niets kapotmaken:

- **Eerste installatie**: de app openen, en de **Gatekeeper-melding** ("app van een onbekende
  ontwikkelaar" / "wil je het openen") — leg uit dat dit normaal is en hoe je veilig doorgaat.
- **macOS vraagt toestemmingen**: welke pop-ups verschijnen en dat je op **Toestaan** klikt.
- **macOS vraagt je Mac-wachtwoord**: leg uit dat dit het inlogwachtwoord van de Mac is (niet het
  QuadroApp-wachtwoord), waarom het gevraagd wordt en dat het veilig is.
- **Automatische update**: wat de melding **"🔄 Update gedownload — Herstart nu"** betekent, dat je
  gerust op **Herstart nu** mag klikken, en dat je werk bewaard blijft.
- **Duidelijk "niet doen"-kader**: bv. de app niet uit `~/Library/Application Support/QuadroApp/`
  verwijderen, bestanden daar niet wissen, en bij twijfel Anthony bellen i.p.v. zelf iets weg te gooien.

> Feitelijke basis voor dit hoofdstuk staat in `docs/CLOUD_ROLLOUT_RUNBOOK.md` (macOS-paden,
> update-flow via `VelopackUpdateChecker`, toast "Herstart nu").

### 4. Uitbreiden "Problemen oplossen"
- Screenshots van de echte foutmeldingen (kan niet inloggen, geen internet/DB, "iemand anders heeft
  dit gewijzigd", lege lijsten) + wat te doen. Telkens één regel: "en als het dan nog niet lukt, bel Anthony".

---

## Acceptatiecriteria

- [ ] Elk scherm uit de lijst hierboven heeft minstens één screenshot en een knop-voor-knop-uitleg.
- [ ] Elke knop/functie die in de tekst staat, is ook op een afbeelding aangeduid (annotatie).
- [ ] Er is een volledig hoofdstuk "Installeren en updaten op de Mac" met screenshots van de
      Gatekeeper-melding, de toestemmings-pop-ups en de wachtwoordvraag, plus de update-herstart.
- [ ] Er is een duidelijk "wat je beter niet doet"-kader zodat de gebruiker niets kan stukmaken.
- [ ] "Problemen oplossen" toont de echte foutmeldingen met beeld.
- [ ] Taal is niet-technisch, stap-voor-stap, en consistent (NL).
- [ ] Levering als **`.docx`** (net, met inhoudsopgave) in `docs/`, plus de beeldbestanden in
      `docs/handleiding/img/`. Optioneel ook een PDF-export.
- [ ] Klantgegevens op screenshots zijn testdata (geen echte personen).

## Aanpak voor de agent

1. Lees `docs/QuadroApp_Handleiding.docx` (v1) als vertrekpunt.
2. Lever eerst een **shotlist** op (genummerd, per scherm/knop) en vraag Anthony de screenshots te maken.
3. Bouw de v2-`.docx` met de docx-skill: neem de v1-tekst over, verfijn per scherm, en voeg de
   afbeeldingen + annotatie-verwijzingen toe.
4. Schrijf het nieuwe Mac-installatie/update-hoofdstuk op basis van `CLOUD_ROLLOUT_RUNBOOK.md`.
5. Rendeer naar PDF en controleer elke pagina visueel voor levering.

**Bronnen:** `docs/QuadroApp_Handleiding.docx`, `docs/CLOUD_ROLLOUT_RUNBOOK.md`,
`docs/backlog/REL04c_CloudPostgres_Plan.md`, en de schermenlijst hierboven.
