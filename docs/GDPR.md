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
