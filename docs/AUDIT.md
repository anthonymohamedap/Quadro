# QuadroApp — Audit trail (US-36)

Elke wijziging aan belangrijke gegevens wordt automatisch vastgelegd in de tabel `AuditLogs`: wie (ingelogde gebruiker), wat (entiteit + Id), welke actie (Toegevoegd/Gewijzigd/Verwijderd), welke velden (oud → nieuw, als JSON) en wanneer.

**Gevolgde entiteiten:** offertes & regels, bestelbonnen & lijnen, lijsten, afwerkingen & varianten, leveranciers & bestellingen, klanten, gebruikers.

**Niet gelogd:** wachtwoord-hashes, RowVersion, instellingen en importlogs.

Het auditlog is alleen-schrijven: de app biedt geen functionaliteit om records te bewerken. Raadplegen kan in de app via Instellingen → Auditlog (enkel de beheerder).

**Bewaartermijn: 30 dagen** (afspraak 19/11/2026). Bij elke start van de app worden regels ouder dan 30 dagen verwijderd (`AuditService.RuimOudeRegelsOpAsync`), zodat de tabel niet blijft groeien. Er blijft dus altijd een venster van de laatste 30 dagen over.

**Startklaar maken** (Instellingen → Startklaar maken, enkel de beheerder) maakt het auditlog volledig leeg, samen met alle testgegevens; daarna staat er één regel die vermeldt wie het deed. Zie `Service/StartklaarService.cs`.

Technisch: `AppDbContext.SaveChangesAsync` bouwt de records vóór de save (zodat oud→nieuw bekend is) en schrijft ze er direct na (zodat nieuwe records hun echte Id krijgen). Een audit-fout kan een geslaagde hoofd-save nooit blokkeren.
