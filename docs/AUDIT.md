# QuadroApp — Audit trail (US-36)

Elke wijziging aan belangrijke gegevens wordt automatisch vastgelegd in de tabel `AuditLogs`: wie (ingelogde gebruiker), wat (entiteit + Id), welke actie (Toegevoegd/Gewijzigd/Verwijderd), welke velden (oud → nieuw, als JSON) en wanneer.

**Gevolgde entiteiten:** offertes & regels, facturen & lijnen, lijsten, afwerkingen & varianten, leveranciers & bestellingen, klanten, gebruikers.

**Niet gelogd:** wachtwoord-hashes, RowVersion, instellingen en importlogs.

Het auditlog is alleen-schrijven: de app biedt geen functionaliteit om records te bewerken of te verwijderen. Raadplegen kan momenteel via een database-tool (bv. DB Browser for SQLite); een leesscherm in de app kan later toegevoegd worden.

Technisch: `AppDbContext.SaveChangesAsync` bouwt de records vóór de save (zodat oud→nieuw bekend is) en schrijft ze er direct na (zodat nieuwe records hun echte Id krijgen). Een audit-fout kan een geslaagde hoofd-save nooit blokkeren.
