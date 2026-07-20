# User Stories — Quadro Inlijstatelier
## Feedbackronde Veerle & Kurt — Versie 2.0 · Vervolg

Dit document beschrijft de 3 user stories voortgekomen uit de tweede feedback-mail van Veerle & Kurt. Elke story bevat acceptatiecriteria en een technische uitwerking op basis van de bestaande codebase.

> **Bron-mail (Veerle & Kurt):**
> 1. De meerprijs moet niet afgedrukt worden.
> 2. De kalender om de inlijstingen in te plannen mag samenvallen met de afhaaldatum voor de klant — dus plandatum en afhaaldatum op de bestelbon zijn hetzelfde.
> 3. De korting moet afgetrokken worden van het bedrag inclusief btw.

---

## US-26 · Bestelbon: meerprijs niet meer afdrukken

**Als:** gebruiker
**Wil ik:** dat een ingegeven meerprijs niet als aparte regel op de bestelbon verschijnt
**Zodat:** de klant geen losse "Meerprijs"-regel ziet

**Bron:** Veerle & Kurt — "De meerprijs moet niet afgedrukt worden."

### Acceptatiecriteria

- Op de bestelbon-PDF en in de bestelbon-preview verschijnt **geen** regel "Meerprijs".
- Een offerte zonder meerprijs verandert niet.
- Het eindtotaal van de bestelbon blijft intern consistent (subtotaal − korting + BTW = totaal).

### Technische uitwerking

**Bestand:** `Service/FactuurWorkflowService.cs`, methode `BuildLijnen`.

De meerprijs wordt momenteel als factuurlijn toegevoegd:

```csharp
if (offerte.MeerPrijsIncl > 0)
{
    var ex = effectiefBtw <= 0 ? offerte.MeerPrijsIncl : offerte.MeerPrijsIncl / (1m + (effectiefBtw / 100m));
    lijnen.Add(CreateLijn("Meerprijs", 1, "st", Math.Round(ex, 2), effectiefBtw, sort));
}
```

- Verwijder dit blok zodat er geen "Meerprijs"-regel meer op de bestelbon komt.
- De `PdfFactuurExporter` rendert lijnen generiek, dus er is geen aparte PDF-wijziging nodig.

**Beslissing om te bevestigen:** door de regel weg te laten valt de meerprijs ook uit het bestelbon-totaal. Als de meerprijs wél in het totaalbedrag moet blijven maar enkel niet zichtbaar mag zijn als losse regel, dan moet de meerprijs in plaats daarvan stil verrekend worden in `HerberekenTotalen` (bv. opgeteld bij het totaal zonder eigen lijn). Standaard-aanname: **meerprijs volledig van de bestelbon (regel én totaal)**.

⏱ Schatting: 30 min · Complexiteit: laag

---

## US-27 · Planning: plandatum = afhaaldatum op de bestelbon

**Als:** gebruiker
**Wil ik:** dat de datum waarop ik een inlijsting in de kalender plan automatisch de afhaaldatum op de bestelbon wordt
**Zodat:** plandatum en afhaaldatum altijd gelijk lopen en ik ze niet dubbel hoef in te geven

**Bron:** Veerle & Kurt — "De kalender om de inlijstingen in te plannen mag samenvallen met de afhaaldatum — plandatum en afhaaldatum bestelbon zijn hetzelfde."

### Acceptatiecriteria

- Wanneer een inlijsting (offerteregel) op datum X gepland wordt in de planningskalender, wordt de **afhaaldatum** van die regel automatisch ook datum X.
- De bestelbon toont per inlijsting dezelfde datum als waarop die gepland staat ("afhalen op" = plandatum).
- Bij meerdere inlijstingen met aparte plandatums (zie US-21) krijgt elke inlijsting zijn eigen, gelijke afhaaldatum.
- Handmatig een afwijkende afhaaldatum zetten blijft mogelijk (de sync gebeurt bij het plannen, niet daarna afdwingend).

### Technische uitwerking

**Bestanden:** `Service/WerkBonWorkflowService.cs` (`PlanRegelMetDagCapaciteitAsync`, `VoegPlanningToeVoorRegelAsync`), `Service/FactuurWorkflowService.cs` (sync naar factuur).

- De geplande datum van een inlijsting zit in `WerkTaak.GeplandVan`. De afhaaldatum per inlijsting zit in `OfferteRegel.AfhaalDatum` (wordt op de bestelbon getagd als `afhaal:` in `BuildLijnen`).
- In `PlanRegelMetDagCapaciteitAsync` (en `VoegPlanningToeVoorRegelAsync`): zet na het plannen `regel.AfhaalDatum = <gekozen plandag>` voor de betrokken `OfferteRegel` en sla op.
  - Bij meerdaagse spreiding: gebruik de **laatste** dag (de dag dat het werk klaar is) als afhaaldatum — dat is de dag die voor de klant relevant is.
- `FactuurWorkflowService.GetOrCreateFactuurAsync` synct al `Factuur.AfhaalDatum`/`GeplandeDatum` vanuit de offerte; controleer dat een Draft-bestelbon na (her)plannen de nieuwe datum overneemt (Draft wordt herbouwd, dus dit volgt automatisch zodra de regel-afhaaldatum is bijgewerkt).

**Aandachtspunt:** controleer of de bestelbon-header ("gepland op", `Factuur.GeplandeDatum`) en de afhaaldatum per regel nu hetzelfde tonen. Eventueel `Offerte.GeplandeDatum` mee laten lopen met de planning.

⏱ Schatting: 2–3 uur · Complexiteit: medium

---

## US-28 · Korting aftrekken van het bedrag inclusief btw

**Als:** gebruiker
**Wil ik:** dat een kortingspercentage wordt afgetrokken van het bedrag **inclusief** btw
**Zodat:** de korting rekent zoals afgesproken met de klant (op het brutobedrag)

**Bron:** Veerle & Kurt — "De korting moet afgetrokken worden van het bedrag inclusief btw."

### Acceptatiecriteria

- Bij `KortingPct > 0` geldt: **eindtotaal incl. BTW = totaal incl. BTW × (1 − KortingPct/100)**.
- De getoonde kortingregel op de bestelbon toont het **incl.-bedrag** van de korting.
- BTW en excl.-bedrag worden consistent herleid uit het verlaagde incl.-totaal (BTW = incl − excl).
- Zonder korting verandert er niets.

### Technische uitwerking

**Bestanden:** `Service/FactuurWorkflowService.cs` (`HerberekenTotalen`), `Service/PdfFactuurExporter.cs` (`DrawTotals`).

Huidige logica (US-23) past de korting toe op de **excl.**-basis:

```csharp
var kortingExcl = Math.Round(brutoExcl * (factuur.KortingPct / 100m), 2);
var nettoFactor = (brutoExcl - kortingExcl) / brutoExcl;
factuur.KortingBedragExcl = kortingExcl;
factuur.TotaalExclBtw = Math.Round(brutoExcl - kortingExcl, 2);
factuur.TotaalBtw = Math.Round(brutoBtw * nettoFactor, 2);
factuur.TotaalInclBtw = Math.Round(factuur.TotaalExclBtw + factuur.TotaalBtw, 2);
```

Gewenst: korting op het **incl.**-totaal:

```csharp
var brutoIncl = Math.Round(brutoExcl + brutoBtw, 2);
var kortingIncl = Math.Round(brutoIncl * (factuur.KortingPct / 100m), 2);
var nettoIncl = brutoIncl - kortingIncl;

factuur.TotaalInclBtw = nettoIncl;
factuur.TotaalExclBtw = Math.Round(nettoIncl / (1m + (btwFactor)), 2);  // btwFactor uit het effectieve tarief
factuur.TotaalBtw = factuur.TotaalInclBtw - factuur.TotaalExclBtw;       // BTW = incl − excl
// Bewaar het korting-incl-bedrag voor de PDF:
factuur.KortingBedragExcl = kortingIncl;   // (veldnaam behouden; bevat nu het incl-kortingbedrag)
```

- `HerberekenTotalen` heeft het effectieve BTW-tarief nodig; lees dit uit de lijnen (`factuur.Lijnen.Max(l => l.BtwPct)`) of geef het mee. Bij BTW-vrijstelling (0%) is incl = excl en is de korting gewoon op het bedrag.
- In `PdfFactuurExporter.DrawTotals`: de kortingregel toont nu `KortingBedragExcl` als negatief bedrag — dat is met deze wijziging het incl.-kortingbedrag. Pas eventueel het label aan naar "Korting X% (incl. btw)".
- Overweeg het veld `KortingBedragExcl` te hernoemen naar `KortingBedrag` voor duidelijkheid (optioneel; vereist mee-aanpassen van model + raw-patch).

**Let op samenhang met de bestelbon-preview:** de samenvatting in `FactuurPreviewWindow` toont de kortingregel via `Factuur.KortingPct` + `Factuur.KortingBedragExcl`; die blijft kloppen zolang het veld het kortingbedrag bevat.

⏱ Schatting: 1–2 uur · Complexiteit: medium

---

## Overzicht

| Story | Omschrijving | Complexiteit | Schatting | Bestand(en) |
|---|---|---|---|---|
| US-26 | Meerprijs niet afdrukken | Laag | 30 min | FactuurWorkflowService.cs |
| US-28 | Korting op incl. btw | Medium | 1–2 u | FactuurWorkflowService.cs + PdfFactuurExporter.cs |
| US-27 | Plandatum = afhaaldatum | Medium | 2–3 u | WerkBonWorkflowService.cs + FactuurWorkflowService.cs |

**Totale schatting:** 4–6 uur netto ontwikkeltijd.

Aanbevolen volgorde: US-26 (snelle win) → US-28 (raakt de totalen) → US-27 (planning-koppeling).
