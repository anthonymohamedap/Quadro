> ⚠️ **ARCHIEF** — dit document is een momentopname en niet meer actueel. Zie de README en docs/ voor de huidige stand.

# Projectoverzicht — QuadroApp

**Project:** Quadro Inlijstatelier — desktop-app (.NET 10 / Avalonia / EF Core / PostgreSQL-ready)
**Uurtarief:** € 55,00 / uur
**Status-legenda:** ✅ afgerond · 📄 spec opgeleverd (nog te bouwen)

> De uren zijn een onderbouwde inschatting per opgeleverd onderdeel.

---

## 1. Functionele user stories — feedbackronde 1 (Veerle & Kurt)

| Story | Omschrijving | Status | Uren |
|---|---|---|---:|
| US-19 | Dagweergave in de planner vergroot (dagnamen 13 pt, getallen 15 pt) | ✅ | 0,75 |
| US-20 | Volgnummer verwijderd op de bestelbon (echt aantal stuks getoond) | ✅ | 0,5 |
| US-21 | Aparte afhaaldatums per inlijsting + "Plan per regel" | ✅ | 2,5 |
| US-22 | Afgesproken prijs vervangt de berekening (live recalc + factuur) | ✅ | 2,0 |
| US-23 | Korting expliciet op de bestelbon (regel + verlaagd totaal) | ✅ | 3,5 |
| US-24 | Opmerking op een eigen regel onder de titel | ✅ | 0,5 |
| US-25 | Klantadres prominent bovenaan; Quadro-gegevens enkel in footer | ✅ | 2,0 |

**Subtotaal:** 11,75 u

---

## 2. Build-, runtime- & datafixes

| Onderdeel | Status | Uren |
|---|---|---:|
| Analyse & inwerken in de codebase | ✅ | 1,5 |
| 3 bestaande build-fouten verholpen (corrupt `OfferteView.axaml.cs`, stray `;`, nullable FK) | ✅ | 1,0 |
| Soft-delete-kolommen (`IsGearchiveerd`) via raw-patch + `PendingModelChangesWarning` onderdrukt | ✅ | 1,5 |
| DataGrid-thema toegevoegd (alle lege DataGrids hersteld) | ✅ | 0,5 |
| Afgekapt `LeveranciersView.axaml` hersteld | ✅ | 0,25 |
| Afgesproken prijs correct op de factuur + 1-cent-afrondingsfix | ✅ | 1,0 |

**Subtotaal:** 5,75 u

---

## 3. Bestelbon-preview

| Onderdeel | Status | Uren |
|---|---|---:|
| Lege lijst hersteld (DataGrid → ItemsControl-tabel) | ✅ | 1,5 |
| Leesbare omschrijving (tag-tekst → nette regels via converter) | ✅ | 0,75 |
| Samenvatting rechts uitgelijnd + kortingregel | ✅ | 0,5 |

**Subtotaal:** 2,75 u

---

## 4. Leverancier-bestellingen & weekwerklijst

| Onderdeel | Status | Uren |
|---|---|---:|
| Bestel-UI herzien: duidelijke bestelwijze, eenheid-labels, statusbadges | ✅ | 2,5 |
| Eenheid-logica gecentraliseerd (enum → m/stuks), model-helpers | ✅ | 1,0 |
| Weekwerklijst: UX, "Vernieuwen", leesbare afwerkingen | ✅ | 1,0 |
| "Reeds besteld"-status/badge + `IsBesteld` afgeleid uit data | ✅ | 1,0 |
| Bestel-feedback in weekwerklijst (toast-overlay, foutafhandeling, leverancier-guard) | ✅ | 1,25 |
| Gemonteerd = leveranciersbestelling in stuks; voorraad overslaan (bestellen + ontvangen) | ✅ | 1,25 |

**Subtotaal:** 8,0 u

---

## 5. Feedbackronde 2 (Veerle & Kurt) — afhaaldatum

| Story | Omschrijving | Status | Uren |
|---|---|---|---:|
| US-27 | Plandatum = afhaaldatum (kalender zet afhaaldatum; offerte read-only; reload na planning) | ✅ | 1,5 |
| US-26 | Meerprijs niet afdrukken op de bestelbon | 📄 | — |
| US-28 | Korting aftrekken van het bedrag **incl.** BTW (nu op excl-basis) | 📄 | — |

**Subtotaal:** 1,5 u

---

## 6. Tests

| Onderdeel | Status | Uren |
|---|---|---:|
| Testproject hersteld: TFM net9 → net10, ontbrekende `IToastService`-stub, verouderde tests bijgewerkt naar incl.-BTW-gedrag | ✅ | 1,25 |

**Subtotaal:** 1,25 u

---

## 7. Documenten & specs opgeleverd

| Document | Status | Uren |
|---|---|---:|
| Vervolg-userstories US-26/27/28 (`UserStories_VeerleKurt_v2.md`) | ✅ | 0,75 |
| Enterprise-readiness user stories US-29–38 (`UserStories_EnterpriseReady.md`) | ✅ | 0,5 |
| Spec bestelvorm-eenheid & besteld-status (`Spec_Bestelvorm_en_BesteldStatus.md`) | ✅ | 0,25 |

**Subtotaal:** 1,5 u

---

## Urenoverzicht & bedrag

| Blok | Uren |
|---|---:|
| 1. Functionele user stories (ronde 1) | 11,75 |
| 2. Build-, runtime- & datafixes | 5,75 |
| 3. Bestelbon-preview | 2,75 |
| 4. Leverancier-bestellingen & weekwerklijst | 8,0 |
| 5. Feedbackronde 2 (US-27) | 1,5 |
| 6. Tests | 1,25 |
| 7. Documenten & specs | 1,5 |
| **Totaal uren** | **32,5** |

| | |
|---|---:|
| Subtotaal (32,5 u × € 55,00) | **€ 1.787,50** |
| BTW 21% (indien van toepassing) | € 375,38 |
| **Totaal incl. BTW** | **€ 2.162,88** |

---

## Nog openstaand / voorgesteld

**Feedback ronde 2 (nog te bouwen):**
- **US-26** — Meerprijs niet afdrukken op de bestelbon (~30 min).
- **US-28** — Korting van het bedrag incl. BTW aftrekken i.p.v. excl-basis (~1–2 u).

**Kleine vervolgpunten:**
- "Herplannen" (rechtsklik-menu in de kalender) laat de afhaaldatum nog niet meesyncen; alleen eerste inplannen en "Plan (per) regel" doen dat.
- Volledige "Bewerk bestelling"-flow (bestaande bestelling herladen + aanpassen) — nu is het "markeer als besteld" in één klik.

**Enterprise-readiness (US-29–38, ~12–18 dagen):** tests + CI, EF-migraties saneren, gestructureerde logging & crashrapportage, authenticatie & rollen, secrets-beheer, back-ups, security-hardening, audit trail, GDPR/dataretentie, concurrency/multi-user.
