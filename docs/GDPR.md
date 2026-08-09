# QuadroApp — GDPR (US-37)

## Wat kan er

- **Inzage-export (art. 15):** alle persoonsgegevens van een klant plus gekoppelde offertes en facturen/bestelbonnen als JSON-bestand.
- **Anonimisering (art. 17):** naam, adres, e-mail, telefoon, BTW-nummer en opmerkingen van de klant worden onherstelbaar vervangen; ook oudere audit-records van die klant worden geschoond. De klant wordt gearchiveerd.
- **Retentiebeleid:** kandidatenlijst van klanten zonder activiteit ouder dan de bewaartermijn. Er wordt **nooit automatisch** geanonimiseerd — de zaakvoerder beslist per klant.

## Wat blijft bewaard (bewust)

Facturen en bestelbonnen zijn boekhoudkundige documenten met een wettelijke bewaarplicht (België: 7 jaar). Zij dragen hun eigen naam/adres-snapshot en blijven volledig ongewijzigd bij anonimisering. Dit is GDPR-conform: de wettelijke verplichting primeert op het recht op verwijdering.

## Configuratie

Bewaartermijn instellen (standaard 7 jaar) via de Instellingen-tabel: sleutel `Gdpr.RetentieJaren`.

## Rechten

Alle GDPR-acties vereisen de Admin-rol (`Permissie.GdprBeheer`).

## Gebruik

De functies zitten in `IGdprService` (`ExporteerKlantAsync`, `AnonimiseerKlantAsync`, `VindKandidatenVoorbijRetentieAsync`). Een beheerscherm in de app volgt in een aparte UI-story; tot dan zijn ze aanroepbaar vanuit code.

## Hosting & sub-processor (REL-04c — cloud)

De gedeelde database draait op een **managed PostgreSQL bij Scaleway** (zie `docs/backlog/REL04c_CloudPostgres_Plan.md`). Dit heeft de volgende GDPR-gevolgen:

- **Verwerker / sub-processor.** Scaleway SAS is een **verwerker** van de klantpersoonsgegevens. De **verwerkersovereenkomst (DPA)** met Scaleway moet getekend zijn en Scaleway staat als sub-processor in het **verwerkingsregister** (art. 28 & 30).
- **Dataresidentie.** De database staat in de **EU-regio Parijs (PAR)**; de data verlaat de EU niet. Scaleway is een **Frans bedrijf** en valt onder EU-recht (geen US CLOUD Act-blootstelling — bewuste keuze i.p.v. een US-provider met EU-datacenter).
- **Versleuteling in transit.** De app verbindt uitsluitend met **TLS** (`SSL Mode=Require` in de connection string). Onversleutelde verbindingen zijn niet toegestaan.
- **Versleuteling at rest.** Status bij de instance: _in te vullen na de beslissing_ (zie REL-04c). Staat deze uit, dan is dat een bewust aanvaard restrisico, gedekt door TLS-in-transit, EU-residentie en toegangsbeveiliging; staat deze aan, dan noteren als "encryptie-at-rest actief".
- **Toegangsbeveiliging.** Verbinding vereist het sterke wachtwoord van de databasegebruiker `quadro`; het wachtwoord wordt nooit in plaintext opgeslagen (US-33: env var / DPAPI-secret / `__SECRET__`-placeholder). De netwerktoegang (Scaleway Allowed IPs) leunt op wachtwoord + TLS.
- **Backups.** Scaleway maakt automatische backups (+ point-in-time recovery). Na anonimisering van een klant kan diens oude data nog in de backups aanwezig zijn tot de **backup-retentie** verlopen is — dit is GDPR-aanvaardbaar bij een redelijke, beperkte retentie (aanbevolen 7–30 dagen).

> **Actiepunten bij ingebruikname:** DPA tekenen · Scaleway in het verwerkingsregister · at-rest-encryptiestatus hier invullen · backup-retentie vastleggen.
