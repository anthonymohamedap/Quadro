# User Stories — Vervolgfuncties (na de release)

Losse functionele stories die niet in de 2-PC-release hoeven, maar wel op de backlog staan.
Elke story bevat acceptatiecriteria, een technische uitwerking en een kant-en-klare prompt.

---

## US-40 · Audit-leesscherm in de app (admin-only)

**Als** zaakvoerder/beheerder **wil ik** het audit-logboek rechtstreeks in de app kunnen inzien en
filteren **zodat** ik kan herleiden wie een offerte, prijs, factuur of leverancier heeft aangepast,
zonder een externe database-tool te gebruiken.

**Achtergrond:** de audit trail (US-36) wordt weggeschreven naar de tabel `AuditLogs` (kolommen
`Tijdstip` [UTC], `Gebruiker`, `EntiteitType`, `EntiteitId`, `Actie`, `Wijzigingen` [JSON oud→nieuw]).
Er is nu nog géén leesscherm; raadplegen kan alleen via bv. DB Browser for SQLite. Zie `docs/AUDIT.md`.

### Acceptatiecriteria
- Nieuw scherm/venster, alleen bereikbaar voor Admin (nieuwe of bestaande permissie — zie hieronder).
- Toont de audit-records in een tabel, nieuwste eerst, met kolommen: tijdstip (in **lokale** tijd via
  `UtcToLocalConverter`), gebruiker, entiteit-type, entiteit-id, actie.
- De `Wijzigingen`-JSON wordt leesbaar getoond (bv. per veld "oud → nieuw"), niet als ruwe JSON.
- Filterbaar op: gebruiker, entiteit-type, actie en datumbereik.
- Paginering of virtualisatie zodat het scherm ook met tienduizenden records vlot blijft.
- Alleen-lezen: geen bewerk-/verwijderknoppen (het auditlog is onveranderbaar).
- Bereikbaar via Instellingen → (nieuwe knop) "Auditlogboek", naast Gebruikersbeheer.

### Technische uitwerking
- **Permissie:** hergebruik `Permissie.GebruikersBeheren` óf voeg `Permissie.AuditInzien` toe
  (Admin-only in `RolPermissies`). Aanbeveling: aparte `AuditInzien` zodat het losstaat van
  accountbeheer; werk dan ook het rechten-overzicht in Gebruikersbeheer bij (dat leest `RolPermissies`).
- **Service:** `IAuditService.ZoekAsync(filter, skip, take)` met `VereisPermissie(...)`, die
  `AppDbContext.AuditLogs` bevraagt met `AsNoTracking()`, gefilterd + gepagineerd, `OrderByDescending(Tijdstip)`.
- **ViewModel:** `AuditLogViewModel` met filtervelden (gebruiker/type/actie/van-tot), een
  `ObservableCollection<AuditRegelWeergave>` en volgende/vorige-pagina. Parse de `Wijzigingen`-JSON
  naar een leesbare lijst "Veld: oud → nieuw".
- **View:** `AuditLogWindow.axaml` in de huisstijl (QuadroHeader, `Card`, tokens, `TextTrimming` +
  tooltips, DataGrid met `*`-kolommen + `MinWidth`, `ScrollViewer`). Volgt alle enterprise-UI-regels.
- **Weergave tijd:** `Tijdstip` staat in UTC → toon via `UtcToLocalConverter` (bestaat sinds REL-01).
- **Tests:** service-test op filtering/paginering/permissie-guard; een parser-test die JSON → leesbare
  regels omzet (incl. lege/verwijderd-record `{}`).

⏱ Schatting: 1–1,5 dag · medium.

**PROMPT:**
```
Voer US-40 uit volgens docs/backlog/UserStories_VervolgFuncties.md. Bouw een admin-only audit-
leesscherm: IAuditService.ZoekAsync (AsNoTracking, gefilterd + gepagineerd, VereisPermissie),
AuditLogViewModel met filters (gebruiker/type/actie/datumbereik) + paginering en JSON→leesbaar
"veld: oud → nieuw", en AuditLogWindow in de huisstijl (QuadroHeader, tokens, trimming/tooltips,
DataGrid met star-kolommen, ScrollViewer), bereikbaar via Instellingen. Toon Tijdstip lokaal via
UtcToLocalConverter. Voeg Permissie.AuditInzien toe (Admin-only) en werk het rechten-overzicht bij.
Tests: service (filter/paginering/permissie) + JSON-parser. Branch feature/us40-audit-leesscherm,
.\verify.ps1 groen.
```

### Status
| Story | Onderwerp | Prioriteit | Status |
|---|---|---|---|
| US-40 | Audit-leesscherm in de app | Medium (na release) | ⬜ |
