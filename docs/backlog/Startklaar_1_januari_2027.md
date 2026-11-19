# Startklaar maken op 1 januari 2027 + auditlog 30 dagen

Stand 19/11/2026. Afspraken met Anthony.

## Waarom
Tot eind 2026 wordt de app getest op de echte database (Scaleway). Op 1 januari 2027 begint het atelier echt.
Alles wat tijdens het testen ontstond, moet dan weg; alles wat nodig is om een offerte te maken, blijft.

## Wat er weggaat
- Klanten (het zijn allemaal testklanten)
- Offertes en inlijstingen, werkbonnen en planning, bestelbonnen
- Betalingen, voorschotten en winkelverkopen
- Archief (offertes en werkbonnen)
- Bestellingen bij leveranciers, voorraadgeschiedenis en voorraadmeldingen
- Importlogboek, foutmeldingen en het auditlog

## Wat blijft
- Lijsten **met hun voorraad** (de gereserveerde voorraad gaat naar 0: die hoorde bij de testwerkbonnen)
- Afwerkingen, standaardkaders, leveranciers, gebruikers, instellingen, geblokkeerde dagen

## Nummering
- Offertenummer begint opnieuw bij **1**.
- Het eerste bestelbonnummer van 2027 wordt **2027-1** (volgnummer per jaar, gebeurt vanzelf).
- De interne tellers (Id) van de leeggemaakte tabellen beginnen opnieuw bij 1.

## Hoe
Instellingen → **Startklaar maken…** (enkel de beheerder). Het venster toont eerst hoeveel er weggaat en wat blijft;
pas na het intypen van **STARTKLAAR** kan het. Alles gebeurt in één transactie (lukt één stap niet, dan blijft
alles zoals het was). Daarna staat er één auditregel "Startklaar gemaakt" met de naam van de beheerder.

Draaiboek voor 31/12 of 1/1:
1. Iedereen sluit Quadro af.
2. Scaleway → Managed Databases → Backups → **Create backup**.
3. Op één pc: Quadro openen als beheerder → Instellingen → Startklaar maken… → aantallen nakijken → STARTKLAAR → wissen.
4. Een testofferte maken: die moet nummer 1 krijgen. Daarna die offerte archiveren of laten staan als eerste echte.

## Auditlog: 30 dagen
Bij elke start van de app worden auditregels ouder dan **30 dagen** verwijderd (`AuditService.RuimOudeRegelsOpAsync`),
zodat de tabel niet blijft groeien. Er blijft altijd een venster van de laatste 30 dagen.

## Code en testen
- `Service/StartklaarService.cs`, `ViewModels/Gebruikers/StartklaarViewModel.cs`, `Views/StartklaarWindow.axaml`
- `WorkflowService.Tests/StartklaarServiceTests.cs`, `AuditOpruimenTests.cs`
- Nagekeken op PostgreSQL 16 met de echte migraties: alles weg, voorraad behouden, nieuwe klant en offerte krijgen Id 1 en nummer 1.
