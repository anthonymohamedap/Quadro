# Plan — Standaardkader, winkelverkopen & "Overzicht betalingen" (US-68, US-66, US-69, US-70, US-67)

*Bijgewerkt 27/09/2026 met de antwoorden van Veerle (mail 27/09) en foto's/afdrukken van het huidige systeem.
Vervangt de US-66/US-67/US-68-secties van `US-63-68_VeerleFeedback_v4_Plan.md`.*

## Antwoorden van Veerle (27/09)

| # | Vraag | Antwoord | Gevolg |
|---|---|---|---|
| 1 | Inleg-berekening | Volledige 2e lijst (materiaal, marge, werk) | US-65 klopt, niets te doen |
| 2 | Btw kassa-verkopen | Altijd 21 % | Geen btw-keuze, vast 21 % |
| 3 | Saldo bij winkelverkoop | Altijd meteen betaald; wil de klant een factuur, dan wordt het een factuur (niet in de winkelverkoop, anders dubbel) | Geen saldo/deelbetaling bij winkelverkoop |
| 4 | Afrekenen inlijsting | Meestal bij afhalen, soms vooraf per overschrijving, soms deels cash + deels kaart | Betaling op elk moment registreerbaar op de bestelbon, meerdere betalingen mogelijk |
| 5 | Artikel + inlijsting | Winkelverkoop → kassa. Maar inlijsten in een standaardkader, of een standaardkader bestellen → op de bestelbon (nu als "meerprijs"). Nooit beide tegelijk. | Standaardkader-regel in de offerte blijft |
| 6 | Boekhouder | Geen Excel; afdruk. Twee printknoppen: ontvangsten per dag (detail, met bonnrs; blanco = kassa) en per maand (dagtotalen) | Twee PDF-afdrukken in hun layout |
| 7 | Startdatum | Oké | Overzicht volledig vanaf ingebruikname |
| 8 | Kant-en-klaar in offerte | Niet weghalen, wel hernoemen naar "standaardkader" | US-68 = hernoemen |
| – | Afwerking bij standaardkader | Geen afwerking (bevestigd door Anthony) | Blijft zoals US-58 |

## Huidig systeem (foto's)

**Kassa-verkopen** (tabbladen per maand): datum · info (vrije tekst) · aantal · prijs (incl.) · korting % /
bedrag · btw-code · incl. · betaling · betaalwijze (kontant, cheque, visa, bancontact, proton, storting) ·
terug (wisselgeld) · saldo.

**Overzicht betalingen** (van … tot …): scherm met één rij per dag (datum + dagnaam), kolom per betaalwijze.
Twee afdrukken, kop "Quadro — overzicht betalingen van dd/mm/jj tot dd/mm/jj", blz-nr + afdruktijdstip:
- **Per dag (detail)**: per ontvangst datum · bon (bonnr., leeg = kassaverkoop) · kontant · cheque · visa ·
  b.contact · proton · storting · jaar; per dag een lijn "totaal" met het dagtotaal vet links.
- **Per maand/periode**: per dag datum · dag · kontant · cheque · visa · b.contact · proton · storting;
  onderaan eindtotalen per betaalwijze + algemeen totaal (vet).

## Ontwerp: één tabel `Ontvangst`

```
Ontvangst
  Id, Datum
  Soort              enum { Voorschot, Betaling (bestelbon), Winkelverkoop, Correctie }
  Betaalwijze        enum { Kontant, Cheque, Visa, Bancontact, Proton, Storting }  (bestaat al)
  BedragIncl         decimal(18,2) — ontvangen bedrag
  OfferteId?, FactuurId?            — koppeling (bonnr. op de afdruk)
  // winkelverkoop:
  Omschrijving (300), Aantal, PrijsPerStukIncl, KortingPct  → BedragIncl = aantal × prijs − korting
  // btw altijd 21 % (constante/instelling, niet per regel)
  AangemaaktOp, AangemaaktDoor
```
Wijzigen/verwijderen: `Permissie.Factureren`, automatisch in `AuditLog`.
`Offerte.VoorschotBedrag` = som van de Voorschot-ontvangsten (blijft voor PDF's/rest te betalen).

## Stories (volgorde)

| # | Story | Inhoud | ± |
|---|---|---|---|
| 1 | **US-68** Standaardkader | "Kant-en-klaar kader" → **"Standaardkader"** in alle schermen, PDF's en weeklijst. Bestelbon toont de naam van het standaardkader (i.p.v. "Lijstwerk") en behoudt de prijs bij herberekening. Geen afwerkingen. Geen migratie. | 1–2u |
| 2 | **US-66** Winkelverkopen | Tabel `Ontvangst` + scherm naar het voorbeeld van kassa-verkopen: datum, omschrijving, aantal, prijs incl., korting %, betaalwijze; btw vast 21 %; betaald = totaal; maandfilter. Vervangt de `WinkelVerkoop`-branch (nog niet live → geen data-migratie). | 3–4u |
| 3 | **US-69** Voorschot | "Voorschot ontvangen…" in de offerte: bedrag, datum, betaalwijze (ook overschrijving vooraf); lijstje ontvangen voorschotten. | 4–5u |
| 4 | **US-70** Betaling op bestelbon | "Betaling registreren" op elk moment (vooraf, bij afhalen, achteraf per overschrijving), meerdere betalingen (cash + kaart), rest te betalen zichtbaar; rest = 0 → bestelbon Betaald. Bij afhalen: voorstel "afrekenen: nog € X". | 5–6u |
| 5 | **US-67** Overzicht betalingen | Scherm zoals nu (per dag × betaalwijze + totaal) + **twee PDF-afdrukken** in hun layout (per dag detail met bonnrs, per periode dagtotalen). Geen Excel. | 6–8u |

Totaal ± 19–25u. Elke story eigen branch en PR; migraties telkens SQLite + Npgsql.
