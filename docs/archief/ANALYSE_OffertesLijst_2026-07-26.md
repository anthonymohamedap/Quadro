# Analyse — OffertesLijst-scherm (dode/niet-gekoppelde componenten)

*Read-only analyse 26 juli 2026 · scope: `ViewModels/Offerte/OffertesLijstViewModel.cs`,
`Views/OffertesLijstView.axaml(.cs)`.*

## Conclusie vooraf
Het scherm is **beter gekoppeld dan het aanvoelt**: alle 6 commando's worden gebruikt en de bindings
kloppen. Er is géén grote dode-code-massa. Wel één **echte bug** (stille laadfouten) en twee kleine
opruimpunten.

## Wat is wél correct gekoppeld
- **Commando's (6/6 in gebruik):** `LoadCommand`, `NewCommand`, `DeleteCommand`, `OpenArchiefCommand`,
  `GaTerugCommand` zitten in de AXAML; `OpenCommand` wordt getriggerd via dubbelklik in de code-behind
  (`OffertesLijstView.axaml.cs` → `OffertesList_DoubleTapped`). Dus niet dood.
- **Collecties:** `FilteredOffertes` (grid) en `Offertes` (interne bron voor filtering) werken samen —
  `Offertes` wordt gevuld in `LoadAsync` en gefilterd naar `FilteredOffertes`.
- **Filters:** `Zoekterm`, `GeselecteerdJaar`, `BeschikbareJaren`, `SelectedOfferte` zijn allemaal
  gebonden.
- **Delete-flow:** archiveert via `IOfferteArchiefService` en geeft nette toast-feedback (succes/fout).

## Bevindingen

### 1. [bug] Laadfouten zijn onzichtbaar voor de gebruiker
`OffertesLijstViewModel.LoadAsync` (regel ~102) zet bij een fout `Foutmelding = "Fout bij laden: ..."`,
maar **`Foutmelding` is nergens gebonden in de view en wordt niet ge-toast**. Gevolg: als het laden van
de offertelijst mislukt, ziet de gebruiker niets (leeg scherm zonder uitleg). `DeleteAsync` doet het wél
goed (toast). → **Fix:** in `LoadAsync` de fout via `_toast.Error(...)` tonen (consistent met de rest),
of `Foutmelding` in de AXAML binden.

### 2. [smaak] `IsBusy` heeft geen visuele indicator
`IsBusy` werkt correct als herentree-guard (`if (IsBusy) return`) maar is **niet gebonden** aan een
laad-indicator in de view. Functioneel oké, maar er is geen spinner/greyed-out tijdens laden of
archiveren. → Optioneel: een `ProgressBar`/overlay binden aan `IsBusy`.

### 3. [smaak] `Offertes` is `public` maar enkel intern gebruikt
De ongefilterde `Offertes`-collectie wordt alleen intern gebruikt als filterbron. Kan `private` zodat de
publieke API van de VM alleen `FilteredOffertes` toont. Puur netheid, geen functioneel effect.

## Wat NIET het geval is
- Geen ongebruikte/verweesde commando's of properties in dit scherm.
- Geen kapotte bindings (alle `{Binding ...}`-targets bestaan op de VM of het `Offerte`-model).
- Geen duplicaat-scherm voor de offertelijst gevonden.

### 4. [opgelost] Altijd-lege planningskolommen verwijderd
De kolommen **Gepland**, **Deadline** en **Tijd (min)** bonden aan `Offerte.GeplandeDatum`,
`Offerte.DeadlineDatum` en `Offerte.GeschatteMinuten`. Die offerte-*header*-velden worden **nergens
gevuld**: geen invoer in `OfferteView`, en geen service schrijft ze (enkel zelf-kopieën bij opslaan).
De echte planning zit op `WerkTaak` (GeplandVan/GeplandTot/DuurMinuten) en de afhaaldatum op
`OfferteRegel` (US-27). Het waren dus verlaten velden → de kolommen stonden altijd leeg.
**Opgelost:** de 3 lege kolommen + de dode trailing `Auto`-kolom verwijderd; lijst toont nu
Nr, Klant, Datum, Status, Totaal incl. De model-velden blijven staan (geen migratie nodig).

## Aanbeveling
Alleen **bevinding 1** is de moeite als losse fix (klein, verbetert de betrouwbaarheid: geen stille
fouten). 2 en 3 zijn optioneel/cosmetisch. Los deze bij voorkeur op als kleine `fix/`-branch ná de
release, tenzij je de stille laadfout nu al wil meenemen.
