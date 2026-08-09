# REL-04c · Cloud-PostgreSQL uitrol (managed, GDPR-bewust)

> **Doel.** De gedeelde database niet op een werk-pc draaien, maar op een **managed
> cloud-PostgreSQL**. Reden (beslist met Anthony): klanten (Veerle, Kurt) zijn niet-technisch
> en mogen niks moeten onthouden of zelf beheren — alles moet zo veel mogelijk automatisch —
> en bij problemen moet Anthony het van op afstand kunnen vinden en aanpassen.
>
> **Kernvoordeel.** De app is er al klaar voor. Cloud is voor QuadroApp gewoon een andere
> `Host=` in de connection string. Het US-41-werk (`PostgresSchemaPatcher`) bouwt bij de
> eerste verbinding automatisch het schema + seed-data, en de US-43-retry vangt korte
> netwerk-hikjes op. Er is dus **geen** handmatige database-setup bij de klant.
>
> *Opgesteld: 9 augustus 2026. Prijzen gecontroleerd augustus 2026 — verifieer bij de provider vóór aankoop.*

---

## 1. De story

**Als** zaakvoerder **wil ik** dat de gedeelde database in een beheerde EU-cloud draait
**zodat** al mijn pc's er altijd bij kunnen zonder dat één werk-pc aan moet staan, mijn
niet-technische medewerkers niks hoeven te beheren, en ik problemen van op afstand kan oplossen.

**Acceptatiecriteria**

- De app verbindt met een managed PostgreSQL in een **EU-regio** over een **versleutelde**
  verbinding (`SSL Mode=Require` of strenger).
- **Eerste start bouwt zichzelf:** een verse cloud-database krijgt via de bestaande
  `PostgresSchemaPatcher` automatisch het volledige schema + de referentie-seed (afwerkingsgroepen).
  Geen handmatige SQL, geen `EnsureCreated`-vs-migratie-drift.
- **Zero-touch voor de klant:** elke pc krijgt dezelfde `appsettings.json` (zelfde host); het
  DB-secret wordt door de installer weggeschreven, zodat de gebruiker enkel de app hoeft te openen.
- **Geen LAN-afhankelijkheid meer:** geen PC1/PC2-onderscheid, geen vast LAN-IP, geen firewallpoort
  5432, geen "server-pc moet aan".
- **Backups** staan aan bij de provider (automatisch, EU) + optioneel een eigen `pg_dump`-export.
- **Monitoring/beheer op afstand:** Anthony kan via het providerdashboard (of pgAdmin/DBeaver)
  logs bekijken, queries draaien en data corrigeren zonder op een klant-pc te zitten.
- **GDPR:** EU-dataresidentie, getekende verwerkersovereenkomst (DPA), provider opgenomen in het
  verwerkingsregister, versleuteling in transit én at rest. Zie sectie 5.

---

## 2. Wat er in de app moet veranderen (klein)

| Wijziging | Bestand | Toelichting |
|---|---|---|
| `SSL Mode=Disable` → `SSL Mode=Require` (+ evt. `Trust Server Certificate=true` of CA-validatie) | `appsettings.json` / `appsettings.example.json` | Cloud eist TLS; localhost stond op Disable. |
| Connection-string-sjabloon voor cloud toevoegen (met `__SECRET__`-placeholder + Scaleway-poort) | `appsettings.example.json` | Zelfde string voor álle pc's — geen PC1/PC2-varianten meer. Scaleway gebruikt een toegewezen poort, niet 5432. |
| Installer/first-run schrijft het DB-secret weg | installer + `SecretStore.StorePassword` (bestaat al) | Zodat de klant niks hoeft te typen. Eén gedeelde cloud-credential. |
| (Al aanwezig) retry-on-failure | `App.axaml.cs` (`EnableRetryOnFailure`) | US-43 — vangt transiënte cloud-netwerkfouten op. Geen actie nodig. |
| (Al aanwezig) schema self-heal | `PostgresSchemaPatcher` | US-41 — bouwt schema bij eerste verbinding. Geen actie nodig. |

> De codewijziging is dus minimaal: vooral connection-string + TLS + het secret automatisch zetten.
> Het zwaartepunt van REL-04c is **operationeel** (provider kiezen, DPA, config uitrollen).

---

## 3. Stappenplan (operationeel)

1. **Provider + regio kiezen** (zie tabel sectie 4). Altijd een **EU-regio** (bv. Frankfurt,
   Amsterdam, Parijs).
2. **DPA tekenen** en de provider in het verwerkingsregister opnemen (sectie 5).
3. **Instance aanmaken**: kleinste productieplan volstaat; database `quadrodb` + gebruiker `quadro`
   met een sterk wachtwoord. Backups + PITR aanzetten. Versleuteling at rest verifiëren.
4. **Connection string** samenstellen: `Host=<cloud-host>;Port=5432;Database=quadrodb;Username=quadro;Password=__SECRET__;SSL Mode=Require`.
5. **Eerste start op Anthony's pc**: app openen → `PostgresSchemaPatcher` bouwt schema + seed
   automatisch. Controleer dat lijsten/afwerkingen via de Excel-import binnenkomen.
6. **Uitrol naar de klant-pc's**: dezelfde `appsettings.json` meeleveren; installer zet het secret.
   Klant opent enkel de app.
7. **Backups verifiëren**: providerbackup + geteste **restore** (proefherstel op een rustig moment,
   procedure in `docs/BACKUP_RESTORE.md` bijwerken voor cloud).
8. **Monitoring**: providerdashboard/alerts instellen; Anthony test remote toegang via pgAdmin/DBeaver.
9. **GDPR.md + verwerkingsregister** bijwerken met de nieuwe sub-processor (sectie 5).

---

## 4. Providervergelijking (managed PostgreSQL, EU)

> Prijzen = kleinste bruikbare productieplan, augustus 2026. "EU-soeverein" = het bedrijf zelf
> valt onder EU-recht (niet enkel het datacenter) → sterkste positie tegen US CLOUD Act / Schrems II.

| Provider | Vanafprijs (klein plan) | EU-regio | EU-soeverein | Backups/PITR | Pros | Cons |
|---|---|---|---|---|---|---|
| **Scaleway** (FR) | ~€11/mnd (DEV-S), prod ~€28–80 | Parijs, Amsterdam | ✅ Frans bedrijf | Automatisch + PITR | Goedkoop, **EU-soeverein**, sterkste GDPR-positie, voorspelbaar | Console/docs minder gepolijst, kleiner ecosysteem |
| **DigitalOcean** (US) | $15/mnd (1 node); HA $60/mnd | Frankfurt, Amsterdam | ❌ US-bedrijf | 7 dagen + PITR gratis | Vaste prijs, IPv4, doodsimpel, geen pooler-gedoe, PITR inbegrepen | US CLOUD Act (ook in EU-DC), dashboard basic |
| **Supabase** (US) | $25/mnd Pro (incl. $10 compute-credit) | Frankfurt (AWS) | ❌ US-bedrijf op AWS | Automatisch (Pro) | **Beste dashboard** (tabel-/SQL-editor, logs) → makkelijkst fixen op afstand; veel extra's | US CLOUD Act, pooler/IPv6-finesse, veel features die je niet nodig hebt |
| **Neon** (US) | Serverless: Launch ~$0,106/CU-uur + $0,35/GB; bruikbare gratis tier | Frankfurt e.a. | ❌ US (Databricks) | Restore-window 7 dagen | Goedkoopst bij laag gebruik, echte gratis tier | **Scale-to-zero** → trage eerste query na inactiviteit; metered = minder voorspelbaar |
| **Aiven** (FI) | Hobbyist $19; productie (Business) ~$200/mnd | Frankfurt e.a. | ✅ Fins bedrijf | Automatisch + PITR | EU-soeverein, enterprise-grade, multi-cloud | Duur op productie-tier |
| **AWS RDS** (US) | ~$120/mnd vergelijkbaar | Frankfurt | ❌ US-bedrijf | Automatisch + PITR | Zeer robuust, veel knoppen | Overkill + duur + complex voor deze schaal; US CLOUD Act |

**Bronnen (aug 2026):**
[Bytebase pricing-vergelijking](https://www.bytebase.com/blog/postgres-hosting-options-pricing-comparison/) ·
[HostStack — Managed PostgreSQL Europe](https://hoststack.dev/blog/managed-postgresql-europe-buyers-guide) ·
[Scaleway PostgreSQL pricing](https://hoststack.dev/blog/scaleway-postgresql-pricing-2026) ·
[DigitalOcean Managed Postgres deep-dive](https://infratally.com/articles/digitalocean-managed-postgres-deep-dive/) ·
[Supabase pricing](https://www.metacto.com/blogs/the-true-cost-of-supabase-a-comprehensive-guide-to-pricing-integration-and-maintenance) ·
[Supabase EU/GDPR-kanttekening](https://danubedata.ro/blog/supabase-alternatives-europe-gdpr-2026) ·
[Neon serverless pricing](https://vela.simplyblock.io/articles/neon-serverless-postgres-pricing-2026/) ·
[Neon EU/GDPR-alternatieven](https://danubedata.ro/blog/neon-alternatives-europe-serverless-postgres-2026)

### Aanbeveling

- **Weegt GDPR/EU-soevereiniteit het zwaarst (past bij jullie GDPR-dossier):** → **Scaleway.**
  EU-bedrijf, goedkoop, voorspelbaar, geen US-jurisdictie. Beste prijs/GDPR-verhouding.
- **Weegt operationeel gemak & voorspelbaarheid het zwaarst en is een DPA met een US-provider in
  EU-datacenter aanvaardbaar:** → **DigitalOcean** (saai, robuust, vaste prijs) of **Supabase**
  (als je het fijne dashboard wil om zelf snel data te bekijken/fixen).
- **Vermijd voor deze app:** Neon (cold-start op een altijd-verbonden desktop-app), Aiven/RDS
  (te duur/complex voor 2–3 gebruikers).

### Beslissing (9 augustus 2026) — Scaleway, HA

**Gekozen: Scaleway** (Frans bedrijf, EU-soeverein → sterkste GDPR-positie), regio **Parijs (PAR)**.

- **Plan:** `DB-DEV-S` (2 vCPU, 2 GB RAM), Cost Optimized, Block Storage 5K.
- **Hoge beschikbaarheid:** hoofdnode **+ standby** (automatische failover in seconden bij node-uitval;
  onderhoud zonder downtime). Bewuste keuze i.p.v. 1 node, omdat "alles moet altijd werken".
- **Kost:** ~**€23/mnd** = hoofdnode €0,0156/u + standby €0,0136/u (× ~730 u) + verwaarloosbare
  opslag (~€0,03) en backups (< €0,20). Het verschil met 1 node (~€13) koopt **uptime**, geen data-
  veiligheid — backups + PITR zitten er in beide gevallen bij.
- **Waarom dit tier ruim volstaat:** de database blijft naar schatting **~250 MB over jaren** (tekst/
  getallen, geen PDF's/foto's in de DB). Dat past volledig in het RAM → alles gecachet, razendsnel;
  opslag kost centen; de 2 vCPU staan bij 2–3 gebruikers vrijwel stil. Opschalen kan later met één klik.

**Scaleway-specifieke aandachtspunten (voor de config):**

- **Poort is niet 5432** maar een door Scaleway toegewezen poort — overnemen uit de console.
- **SSL verplicht** met Scaleway's CA-certificaat → `SSL Mode=Require` (of `VerifyFull` met hun cert).
  Dit is meteen de GDPR-versleuteling-in-transit.
- In de console: database `quadrodb` + gebruiker `quadro` aanmaken; backups + PITR aanzetten;
  at-rest-encryptie verifiëren.
- **DPA van Scaleway tekenen** en Scaleway opnemen in het verwerkingsregister (sectie 5).

---

## 5. GDPR — waarom dit hier belangrijk is

QuadroApp bewaart **persoonsgegevens van klanten**: naam, adres, e-mail, telefoon, BTW-nummer en
opmerkingen, plus offertes en facturen/bestelbonnen (`docs/GDPR.md`, US-37). Zodra die data in de
cloud staat i.p.v. lokaal, verandert de GDPR-positie op een paar punten:

**Wat de app al goed doet (blijft gelden):** inzage-export (art. 15), onherstelbare anonimisering
(art. 17), retentiebeleid (standaard 7 jaar, `Gdpr.RetentieJaren`), admin-only GDPR-acties,
wachtwoord-hashing, DB-secret nooit in plaintext, boekhoudkundige bewaarplicht (BE 7 jaar) bewust
gescheiden van het recht op verwijdering.

**Wat cloud toevoegt (te regelen in REL-04c):**

1. **Sub-processor / verwerker.** De cloudprovider wordt een **verwerker** van jouw klantdata.
   Je moet hun **verwerkersovereenkomst (DPA)** tekenen en de provider opnemen in je
   **verwerkingsregister** (art. 28 & 30). `docs/GDPR.md` uitbreiden met deze sub-processor.
2. **EU-dataresidentie.** Kies een **EU-regio** zodat de data de EU niet verlaat.
3. **US CLOUD Act / Schrems II.** Ook mét EU-datacenter valt een **US-bedrijf** (DigitalOcean,
   Supabase, Neon, AWS) onder de Amerikaanse CLOUD Act — een theoretisch toegangsrisico voor
   Amerikaanse autoriteiten. Een **EU-bedrijf** (Scaleway, Aiven) heeft die blootstelling niet.
   Voor een Belgische kadermakerij is het praktische risico klein, maar omdat jij GDPR belangrijk
   vindt, is dit precies de reden om **Scaleway** (of Aiven) te overwegen.
4. **Versleuteling in transit.** `SSL Mode=Require` afdwingen in de connection string (nu staat er
   voor localhost `Disable`). Geen onversleutelde verbinding over internet.
5. **Versleuteling at rest.** Bij de provider aanzetten/verifiëren (bij de meeste standaard aan).
6. **Toegangsbeheer.** Sterk DB-wachtwoord (via de bestaande secret-mechaniek), en waar mogelijk
   netwerktoegang beperken (IP-allowlist). Let op: klanten met een dynamisch thuis-/zaak-IP maken
   een strikte allowlist lastig → dan leunen op sterke credentials + TLS.
7. **Backups & het recht op verwijdering.** Na anonimisering van een klant blijft de oude data nog
   in de provider-backups zitten tot de **backup-retentie** verlopen is. Documenteer dit (het is
   GDPR-aanvaardbaar mits redelijke retentie), en houd de backup-retentie beperkt (bv. 7–30 dagen).

**Concreet te doen voor GDPR in REL-04c:** EU-regio kiezen · DPA tekenen · provider in
verwerkingsregister · `SSL Mode=Require` · at-rest-encryptie verifiëren · backup-retentie vastleggen ·
`docs/GDPR.md` uitbreiden met sub-processor + dataresidentie.

---

## 6. De eerlijke tegenzet

Cloud maakt de werkplaats **afhankelijk van internet**: ligt de internetverbinding plat, dan kan de
app de database niet bereiken. Dat is de nieuwe kwetsbaarheid, in ruil voor het wegvallen van
"welke pc moet aan staan". Voor de meeste zaken is internet betrouwbaarder dan dat, dus per saldo
win je stabiliteit. Voor een echte noodstop kan de app terugvallen op lokale SQLite (single-PC),
met de kanttekening dat die noodwerk-data daarna niet vanzelf terug samenvloeit met de cloud.

---

## 7. Statusregel (voor de backlogtabel)

| Stap | Onderwerp | Type | Insp. | Status |
|---|---|---|---|---|
| REL-04c | Cloud-PostgreSQL uitrol (managed, EU, GDPR) | operationeel + kleine config | M | 🟢 **verbinding getest & werkend** op Scaleway `DB-DEV-S` HA (Parijs) — TLS + login OK, schema auto-gebouwd. Rest: backups/PITR verifiëren, encryptie-at-rest + DPA (GDPR), uitrol naar klant-pc's, lijsten/afwerkingen importeren |
