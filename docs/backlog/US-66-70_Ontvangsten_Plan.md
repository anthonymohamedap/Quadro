# Plan — Ontvangsten & "Overzicht betalingen" (US-66 herwerkt, US-69, US-70, US-67)

*Opgesteld 26/09/2026. Vervangt de US-66/US-67-secties van `US-63-68_VeerleFeedback_v4_Plan.md`.*

## Aanleiding

Veerle (mail na testdag 09/09):

> In ons huidige systeem hebben we een kop "overzicht betalingen". Hier geven we de datum in:
> van … tot …, en krijgen dan een overzicht van de bedragen van de inlijstingen die zijn
> afgehaald, voorschotten die betaald zijn, en artikelen die in de winkel aangekocht zijn
> (kant-en-klare kaders, wenskaarten, ophangsystemen, kunst), en ook hoe ze betaald zijn
> (kontant, Bancontact, Visa, …). Dit geheel van "dagontvangsten" wordt naar de boekhouder
> doorgestuurd.

Het overzicht heeft dus **drie bronnen**, en vandaag kent de app er maar één:

| Bron | Waar in de app | Betaaldatum | Betaalwijze |
|---|---|---|---|
| Voorschot | `Offerte.VoorschotBedrag` + `IsVoorschotBetaald` (bool) | ❌ | ❌ |
| Afhaling (restbedrag) | `WerkBon.Status = Afgehaald`, `Factuur.Status = Betaald` | ❌ | ❌ |
| Winkelaankoop | `WinkelVerkoop` (US-66-branch, nog niet in main) | ✅ | ✅ |

## Ontwerpbeslissing: één centrale tabel `Ontvangst`

Elke ontvangen som geld wordt **één regel** in een centrale tabel, ongeacht de bron. Het
"overzicht betalingen" wordt dan één eenvoudige query op die tabel.

```
Ontvangst
  Id
  Datum              (datum van de betaling, standaard vandaag)
  Soort              enum OntvangstSoort { Voorschot, Afhaling, Winkelverkoop, Correctie }
  Betaalwijze        enum Betaalwijze { Kontant, Bancontact, Visa, Cheque, Proton, Storting }  (bestaat al)
  BedragIncl         decimal(18,2)  — het ontvangen bedrag incl. btw
  BtwPct             decimal(5,2)   — standaard 21
  OfferteId?         — verplicht bij Voorschot/Afhaling
  FactuurId?         — verplicht bij Afhaling (de bestelbon)
  // enkel bij Winkelverkoop:
  Omschrijving       (max 300)
  ArtikelSoort?      enum { KantKlaarKader, Wenskaart, Ophangsysteem, Kunst, Overig }
  Aantal             decimal(18,2), standaard 1
  PrijsPerStukIncl   decimal(18,2)  → BedragIncl = Aantal × PrijsPerStukIncl
  // traceerbaarheid:
  AangemaaktOp (UTC), AangemaaktDoor (gebruikersnaam)
```

Regels (validator + DB-constraints waar mogelijk):
- **Winkelverkoop**: geen Offerte/Factuur; Omschrijving verplicht; BedragIncl = Aantal × PrijsPerStukIncl.
- **Voorschot**: OfferteId verplicht.
- **Afhaling**: FactuurId (en OfferteId) verplicht.
- **Correctie**: mag negatief zijn (terugbetaling, vergissing); verplicht omschrijving.
- Wijzigen/verwijderen alleen met `Permissie.Factureren`; alles zit automatisch in de `AuditLog`.

**Waarom één tabel en niet drie aparte velden?**
- Het overzicht voor de boekhouder is één query, per dag × betaalwijze, zonder drie bronnen te moeten samenvoegen.
- Meerdere voorschotten of een gesplitste betaling (deels cash, deels Bancontact) zijn gewoon meerdere regels.
- De offerte/bestelbon blijft de bron van *wat* er verkocht is; `Ontvangst` is de bron van *wat er binnenkwam*.

`Offerte.VoorschotBedrag` blijft bestaan (PDF's, bestaande schermen gebruiken het), maar wordt
voortaan **afgeleid**: = som van de Voorschot-ontvangsten van die offerte.

---

## US-66 (herwerkt) — Ontvangst-tabel + winkelverkopen

- Entiteit `WinkelVerkoop` (enkel op de branch, nog niet in main) **vervangen** door `Ontvangst`
  + `OntvangstSoort` + `ArtikelSoort`. Migratie `AddOntvangsten` (SQLite + Npgsql) vervangt
  `AddWinkelVerkoop` — kan zonder data-migratie, want de tabel staat nog nergens live.
- Winkelverkopen-scherm blijft functioneel hetzelfde, maar leest/schrijft `Ontvangst` met
  `Soort = Winkelverkoop`. Extra: keuzelijst **Soort artikel**.
- Tests: round-trip (Betaalwijze/Soort als string), validatie winkelverkoop, BedragIncl-berekening.
- Schatting: ~3–4u (scherm bestaat al).

## US-69 — Voorschot registreren met betaalwijze

- In de offerte: het veld "Voorschot betaald" wordt een knop **"Voorschot ontvangen…"** →
  dialoog: bedrag, datum (vandaag), betaalwijze → maakt een `Ontvangst(Soort=Voorschot)`.
- Onder de knop een lijstje van de reeds ontvangen voorschotten (datum · bedrag · betaalwijze),
  met verwijderen (rechten + bevestiging).
- `Offerte.VoorschotBedrag` = som → bestelbon, PDF en "rest te betalen" blijven werken.
- **Bestaande offertes** met een voorschot van vóór deze versie: geen Ontvangst (betaalwijze
  onbekend). Ze blijven correct voor "rest te betalen", maar verschijnen niet in het overzicht.
  → Veerle informeren: het overzicht is volledig vanaf de datum van ingebruikname.
- Schatting: ~4–5u.

## US-70 — Afrekenen bij afhaling

- Eén actie **"Afrekenen"** op de bestelbon (Facturen-scherm) én op de werkbon:
  dialoog met bedrag (standaard = rest te betalen = totaal − voorschotten), datum, betaalwijze,
  en optie "gesplitste betaling" (2 regels).
- Bevestigen doet in **één transactie**: `Ontvangst(Soort=Afhaling)` aanmaken, bestelbon →
  `Betaald`, werkbon → `Afgehaald` (bestaande statuspropagatie US-42 zet de offerte mee).
- De bestaande knop "Markeer betaald" gaat via dezelfde dialoog (geen betaling meer zonder betaalwijze).
- Schatting: ~5–6u.

## US-67 — Overzicht betalingen (dagontvangsten)

- Nieuw scherm **"Overzicht betalingen"**: periode van … tot …
  - **Samenvatting**: per dag × betaalwijze + totalen per betaalwijze en algemeen totaal.
  - **Detail**: elke ontvangst (datum · soort · klant/omschrijving · bestelbonnr · betaalwijze · bedrag).
  - Subtotalen per soort (voorschotten / afhalingen / winkel), en voor winkel per soort artikel.
- **Export voor de boekhouder**: Excel (bestaand `CentralExcelExportService`-patroon) + PDF.
- Melding bovenaan: "Volledig vanaf <datum ingebruikname>; oudere voorschotten zonder betaalwijze
  zitten er niet in."
- Schatting: ~6–8u.

## US-68 — Kant-en-klaar kader uit de offerte-flow (ongewijzigd)

Na US-66: nieuwe kant-en-klare verkopen lopen via Winkelverkopen (`Ontvangst`); bestaande
offertes met zo'n regel blijven leesbaar. ~2–3u.

---

## Volgorde

**US-66 → US-69 → US-70 → US-67**, US-68 los na US-66. Elke story eigen branch, eigen PR.
US-67 als laatste: pas dan zijn alle drie de bronnen gevuld. Totaal ± 20–26u.

## Open vragen voor Veerle (vóór US-69/US-70)

1. **Soort artikel** bij winkelverkoop: klopt de lijst kaders / wenskaarten / ophangsystemen /
   kunst / overig? Moet de boekhouder die opsplitsing zien?
2. **Wanneer wordt er afgerekend?** Altijd bij het afhalen, of soms vooraf/achteraf (overschrijving)?
   Komen gesplitste betalingen (deels cash, deels kaart) voor?
3. **Artikel bij een inlijsting**: koopt een klant bij het ophalen ook een ophangsysteem, komt dat
   dan op de bestelbon of apart als winkelverkoop?
4. **Boekhouder**: welk formaat (Excel/PDF)? Moet de btw apart staan (excl./btw/incl.) of volstaan
   bedragen incl. btw per betaalwijze?
5. **Startdatum**: vanaf wanneer moet het overzicht volledig zijn (oude voorschotten zonder betaalwijze)?
