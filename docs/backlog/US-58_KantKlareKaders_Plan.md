# US-58 · Kant-en-klare kaders in een offerte kunnen invoegen — PLAN

> **Status: plan, ontwerp bevestigd door Anthony — klaar om gebouwd te worden.**

---

## Story

**Als** gebruiker (Veerle, Kurt)
**Wil ik** een reeds afgewerkt, kant-en-klaar kader (dat ze zo bij een leverancier bestellen, niet
zelf op maat snijden) als regel in een offerte kunnen invoegen, gekozen uit een duidelijk aparte
lijst
**Zodat** ik ook dat soort verkoop correct kan beprijzen, zonder de normale snij-/
montageberekening die voor zelfgemaakte kaders geldt, en zonder dat het door elkaar loopt met de
gewone lijstprofielen

**Bron:** Anthony — "een standaard kader die al gemaakt is kan invoegen... ik wil wel dat die
duidelijk in een aparte lijst staan (tabel maakt niet uit), en dat het eigenlijk gewoon een prijs
per stuk en een naam is — niet heel veel extra, hou het simpel."

---

## 1. Waarom niet gewoon `TypeLijst` hergebruiken

`TypeLijst` (de bestaande lijstprofielen-catalogus) draait volledig rond **meter**: prijs per
meter, voorraad in meter, een omtrek-gebaseerde snijberekening (`PricingEngine.
CalculateLijstPrijsExcl`), leverancier-bestellingen in meter. Dat allemaal "even" hergebruiken voor
iets dat gewoon een vaste stukprijs heeft, zou meer velden/uitzonderingen vergen dan gewoon iets
nieuws en simpels neerzetten. Vandaar: **aparte, kleine tabel**, bevestigd door Anthony — geen
voorraad, geen leveranciers-koppeling, geen bestel-integratie. Puur naam + afmeting + prijs.

## 2. Bevestigd ontwerp

### Nieuwe tabel: `KantKlaarKader`

| Veld | Type | Nodig? |
|---|---|---|
| `Id` | int (PK) | — |
| `Naam` | string (bv. "Zwart kader 40×50") | verplicht |
| `BreedteCm` | decimal | verplicht — nodig zodat afwerkingen (glas, passe-partout…) op deze regel nog correct in m² berekend kunnen worden (die rekenen op `BreedteCm`/`HoogteCm` van de offerteregel) |
| `HoogteCm` | decimal | verplicht, zelfde reden |
| `PrijsPerStukExcl` | decimal(18,2) | verplicht — vaste verkoopprijs excl. btw per stuk |
| `IsGearchiveerd` | bool | voor "verwijderen" zonder bestaande offertes te breken (zelfde patroon als `TypeLijst.IsGearchiveerd`) |

Bewust **niet** toegevoegd (tenzij later expliciet gevraagd): leverancier-koppeling, voorraadaantal,
inkoopprijs/winstfactor-opsplitsing, Excel-import, herbestel-alerts. Als dat later toch nodig
blijkt, kan het per stuk toegevoegd worden zonder het basisontwerp om te gooien.

### Prijsberekening

Simpel: `PrijsPerStukExcl × AantalStuks` — geen omtrek, geen arbeidsminuten, geen afvalpercentage.
Afwerkingen (glas/passe-partout/rug/…) op diezelfde offerteregel blijven wél via de bestaande
`CalcOpt`-berekening lopen (die is al generiek en rekent op `BreedteCm`/`HoogteCm`, wat deze
kant-en-klare kaders dus ook moeten meegeven).

### Koppeling op `OfferteRegel`

Net zoals er nu al `TypeLijstId` (op-maat lijst) is, komt er een `KantKlaarKaderId` (nullable FK).
Een regel heeft **óf** een `TypeLijstId`, **óf** een `KantKlaarKaderId`, nooit beide — de
`PricingEngine` kijkt eerst of `KantKlaarKaderId` gezet is; zo ja, gebruik de simpele
stukprijs-berekening; zo nee, val terug op het bestaande `TypeLijst`-pad.

### UI

> **UI-structuur neemt inspiratie van de bestaande TypeLijsten-view** (`Views/LijstenWindow.axaml`
> + `ViewModels/Lijsten/LijstenViewModel.cs`, evt. `Views/LijstDialog.axaml` voor het
> toevoegen/bewerken-dialoogvenster): zelfde opbouw (lijst/grid + zoekbalk + toevoegen/bewerken/
> archiveren-knoppen, dialoog voor het invoerformulier), gewoon met veel minder velden. Zo voelt
> het scherm voor Veerle/Kurt meteen vertrouwd aan, en kan bestaande XAML/structuur als vertrekpunt
> gekopieerd en uitgekleed worden i.p.v. iets nieuws te verzinnen.

- **Aparte lijst/scherm** om kant-en-klare kaders te beheren (toevoegen/bewerken/archiveren) — een
  kleine, eenvoudige variant van `LijstenWindow` met maar 3 invoervelden (naam, afmeting, prijs).
- In de offerte-regel-editor komt een **duidelijk gescheiden** manier om te kiezen: geen combobox
  gemengd met de op-maat-lijsten, maar een apart tabblad of knop "Kant-en-klaar kader kiezen" naast
  de bestaande lijstkeuze. Bij keuze worden `BreedteCm`/`HoogteCm` van de regel automatisch
  ingevuld (en read-only, want de maat ligt vast) en wordt `KantKlaarKaderId` gezet.

---

## 3. Stappenplan

### Fase 1 — Model & migratie
- Nieuw model `QuadroApp.Data/Model/DB/KantKlaarKader.cs` (6 velden hierboven).
- `OfferteRegel`: nieuw `int? KantKlaarKaderId` + navigatie-property (zelfde patroon als
  `TypeLijstId`/`InlegTypeLijstId`).
- `AppDbContext.cs`: `DbSet<KantKlaarKader>` + FK-configuratie op `OfferteRegel`
  (`OnDelete: Restrict`, zodat een kader niet per ongeluk offertes stuk maakt bij verwijderen —
  vandaar ook het archiveer-veld i.p.v. hard verwijderen).
- EF-migratie voor zowel SQLite (`QuadroApp.Data/Migrations`) als PostgreSQL
  (`QuadroApp.Migrations.Npgsql/Migrations`) — zelfde tweeledige aanpak als bij eerdere migraties
  dit project.

### Fase 2 — Pricing
- `PricingEngine.cs`: in de regel-loop, vóór de bestaande `r.TypeLijst is not null`-check, een
  branch toevoegen: als `r.KantKlaarKaderId` gezet is, `lijstPrijs = r.KantKlaarKader.PrijsPerStukExcl`
  (geen omtrek/arbeid/afval). De rest (afwerkingen, extra werk, korting) blijft ongewijzigd
  optellen.
- Unit test toevoegen naar analogie van `PricingEngineTests.cs`.

### Fase 3 — Beheerscherm
- Nieuw, klein venster/view + ViewModel voor CRUD op `KantKlaarKader` (naam, afmeting, prijs,
  archiveren) — qua opzet een verkleinde kopie van `LijstenWindow`/`LijstenViewModel`.
- Toegang vanuit het hoofdmenu naast "Lijstenbeheer" (exacte plek te bepalen bij het bouwen).

### Fase 4 — Offerte-regel UI
- `Views/OfferteView.axaml`: knop/tabblad "Kant-en-klaar kader" naast de bestaande
  `TypeLijst`-combobox in de regel-editor.
- `ViewModels/Offerte/OfferteRegelViewModel.cs` / regelbeheer: property + auto-fill van
  `BreedteCm`/`HoogteCm` (read-only zodra een kant-en-klaar kader gekozen is).

### Fase 5 — Weergave op bestelbon & weeklijst
- Bestelbon-PDF (`PdfFactuurExporter.cs`): geen wijziging nodig, lijnen worden al generiek
  gerenderd.
- Weeklijst-print (`Service/PdfWeekLijstExporter.cs`): toont voor deze regels best iets als
  "kant-en-klaar — [naam kader]" i.p.v. de normale snij-instructies, zodat het atelier niet denkt
  dat er nog gesneden moet worden.

---

## 4. Nog even kort te bevestigen

- **Prijs excl. of incl. btw?** Plan gaat uit van **excl. btw** (consistent met hoe alle andere
  prijsvelden in de pricing-engine intern werken — bv. `AfgesprokenPrijsExcl`). Als jij liever
  incl. btw intypt (zoals bij `MeerPrijsIncl`), is dat een kleine aanpassing.
- Verder geen open vragen meer — dit kan zo gebouwd worden.

⏱ Ruwe schatting: 4-6 uur (veel kleiner dan de vorige versie van dit plan, dankzij de simpele
aparte tabel zonder voorraad/bestel-integratie) · Complexiteit: laag-gemiddeld
