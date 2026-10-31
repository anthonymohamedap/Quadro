# US-105 — Beginscherm overzichtelijk en aanpasbaar aan de schermbreedte

Opgesteld 31/10/2026 na US-86 (blokken "Vandaag af te halen" / "Nog te betalen"). Bij een drukke dag
(10 verkopen per blok, 7 voorraadwaarschuwingen) werd het beginscherm te lang: de tegels en het
voorraadoverzicht vielen buiten beeld. Mock goedgekeurd door Anthony, met één wijziging: in de blokken
**scrollen** in plaats van "Nog 5 tonen".

**Als** medewerker **wil ik** op het beginscherm in één oogopslag zien wat vandaag moet gebeuren,
**zodat** ik niet hoef te scrollen of te zoeken.

Acceptatiecriteria:
1. Kop met begroeting (Goedemorgen / Goedemiddag / Goedenavond) en datum, rechts de knoppen
   **＋ Nieuwe offerte**, Werkbonnen en Planning.
2. "Vandaag af te halen" en "Nog te betalen": één regel per verkoop (klant, offerte/bestelbon, stap of
   afhaaldatum, bedrag), aantal en totaal in de titel. Vaste hoogte (± 6 regels), daarboven **scrollen
   in het blok**. Klik → offerte openen.
3. "Voorraad & leveringen" is één balk die **dichtgeklapt** begint: alleen tellers (lage voorraad,
   tekorten, leveringen te laat, deels ontvangen). Openklappen toont alle waarschuwingen, één regel elk,
   met scrollen. Geen waarschuwingen → "Alles in orde". De keuze open/dicht blijft bewaard zolang de app
   open is.
4. "Snel naar": kleine tegels (Offertes, Planning, Winkelverkopen, Overzicht betalingen, Klanten,
   Lijsten, Standaardkaders, Leveranciers). De rest staat in het menu.
5. Menu links in groepen (Verkoop, Atelier, Beheer) met "Beginscherm" bovenaan.
6. Smaller venster (< 1180 px): menu enkel iconen (met tooltip), blokken onder elkaar, minder kolommen.
   < 820 px: twee tegels per rij.

Technisch: `HomeView.axaml` met Avalonia **container queries** (`Container.Name="home"`), teksten in
`ViewModels/Beginscherm.cs` (getest in `BeginschermTests`). Scenario's in `Tools/SchermAfbeeldingen`:
`home`, `home-leeg`, `home-druk`, `home-druk-open`, `home-vol`, `home-klein` (1024×768).
