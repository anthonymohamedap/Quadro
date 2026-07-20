# Refactor-spec — Bestelvorm-eenheid centraliseren & "Reeds besteld"-status

Doel: de bestel-logica robuuster en eenduidig maken. Drie onderdelen: (1) één enum als single source of truth voor de bestelvorm met afgeleide eenheid, (2) een afgeleide `IsBesteld`-status op de taak-VM met bijbehorende commands, en (3) consistente UI-binding in álle views.

> **Afstemnotities t.o.v. de huidige codebase (lees eerst):**
> - De enum `BestelVorm` bestaat al in `Model/DB/LeverancierBestelLijn.cs` met **vaste waarden** `Verstek = 0, InLengte = 1, Gemonteerd = 2`. Deze worden als `int` in de DB bewaard — **niet** hernoemen/herordenen, anders kloppen bestaande records niet meer. Hergebruik dezelfde enum (verplaats hem eventueel naar een neutrale namespace).
> - De eenheid-mapping hieronder (`Verstek → stuks`) **wijkt af** van de eerder afgesproken mapping (Verstek → m, In lengte → m, Gemonteerd → stuks). Bevestig welke correct is vóór implementatie; de code-switch hieronder volgt jouw laatste spec.
> - Er is nog **geen** directe `Bestelling`-`Taak`-relatie. Vandaag hangt de koppeling via `LeverancierBestelLijn.WerkBonId` (en `WerkTaak.IsBesteld`). De FK-suggestie in deel 2 moet daarop aangepast worden (zie note daar).

---

## 1. Eenheidslogica — centraliseer de bestelvorm

De drie losse booleans (`BestelVormIsInLengte` / `BestelVormIsVerstek` / `BestelVormIsGemonteerd`) zijn lastig als single source of truth. Werk in de ViewModel met één enum en leid de eenheid daarvan af:

```csharp
public enum BestelVorm { InLengte, Verstek, Gemonteerd }

// In de bestel-VM:
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(BestelEenheid))]
private BestelVorm geselecteerdeBestelVorm;

public string BestelEenheid => GeselecteerdeBestelVorm switch
{
    BestelVorm.InLengte => "m",
    BestelVorm.Verstek or BestelVorm.Gemonteerd => "stuks",
    _ => ""
};
```

De drie `IsChecked`-bindings kan je behouden door ze te koppelen aan deze ene property (bv. via een `RadioButton`-converter of een computed get/set per boolean die `GeselecteerdeBestelVorm` zet). `BestelEenheid` gebruik je dan in XAML voor het label naast het invoerveld én bij het wegschrijven naar de `Bestelling`.

**Toepassen in de huidige code:** `LeveranciersViewModel` (nieuwe bestelling) en `WeekWerkItem` hebben nu elk drie boolean-helpers + een `BestelVorm`-veld. Vervang die door bovenstaand patroon en bind het eenheid-label aan `BestelEenheid`.

---

## 2. "Reeds besteld"-status op de taak-VM

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(NieuweBestellingCommand))]
[NotifyCanExecuteChangedFor(nameof(BewerkBestellingCommand))]
private bool isBesteld;

[RelayCommand(CanExecute = nameof(KanNieuweBestellingMaken))]
private async Task NieuweBestelling() { /* ... */ }
private bool KanNieuweBestellingMaken() => !IsBesteld;

[RelayCommand(CanExecute = nameof(IsBesteld))]
private async Task BewerkBestelling() { /* bestaande bestelling laden + aanpassen */ }
```

`IsBesteld` leid je af uit de data: een taak is besteld als er een gekoppelde bestelling bestaat. Idealiter een FK op de bestel-entiteit + hydrateren bij het laden:

```csharp
isBesteld = taak.Bestelling != null; // of: await context.Bestellingen.AnyAsync(b => b.TaakId == taak.Id)
```

> **Note op de huidige datamodel-stand:** vandaag heeft `LeverancierBestelLijn` een `WerkBonId` (geen `TaakId`), en `WerkTaak` heeft al `IsBesteld` + een `LeverancierBestelLijn`-navigatie. Twee opties:
> - **Minimaal (geen migratie):** leid `IsBesteld` af uit de bestaande koppeling — `isBesteld = taak.LeverancierBestelLijn != null || taak.IsBesteld;`
> - **Netjes (kleine migratie):** voeg een `WerkTaakId` toe op `LeverancierBestelLijn` voor een directe 1-op-1 koppeling per inlijsting, en hydrateer `IsBesteld` daarop.

---

## 3. UI-binding — consistent in alle views

- **"Nieuwe bestelling"-knop** → `Command="{Binding NieuweBestellingCommand}"` (disabled zodra `IsBesteld`).
- **"Bewerk bestelling"-knop** → `Command="{Binding BewerkBestellingCommand}"` (enabled zodra `IsBesteld`).
- **Statusindicatie** → bind een badge/label `IsVisible="{Binding IsBesteld}"` met tekst "Reeds besteld".

Zorg dat dezelfde taak-VM (of dezelfde `IsBesteld`-logica) gedeeld wordt door **Werkbonnen**, de **weekwerklijst** én **WerkbonLijst**, zodat de status overal automatisch klopt zonder duplicatie.

> **Toepassen in de huidige code:** de relevante views zijn `WeekWerkLijstWindow.axaml` (`WeekWerkItem`), `WerkBonLijstView.axaml` en het leverancier-bestelscherm. Trek de `IsBesteld`-afleiding en het bestelvorm-/eenheid-patroon in één gedeelde plek (bv. een gemeenschappelijke taak-VM of helper) zodat de drie views identiek gedrag tonen.

---

## Samenvatting van de wijziging

| Onderdeel | Van | Naar |
|---|---|---|
| Bestelvorm | 3 losse booleans per VM | 1 enum `GeselecteerdeBestelVorm` + afgeleide `BestelEenheid` |
| Eenheid-label | hardcoded per plek | `BestelEenheid` (één bron) |
| Besteld-status | ad-hoc per view | afgeleide `IsBesteld` + `CanExecute` op commands, gedeeld over alle views |

Aanbevolen volgorde: eerst de enum/eenheid centraliseren (laag risico), dan `IsBesteld` + commands, dan de drie views op de gedeelde logica aansluiten.
