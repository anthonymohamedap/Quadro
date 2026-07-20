using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-37 — GDPR: inzage-export, anonimisering en retentie.</summary>
public interface IGdprService
{
    /// <summary>
    /// Inzage-export (GDPR art. 15): alle persoonsgegevens en gekoppelde
    /// documenten van een klant als JSON.
    /// </summary>
    Task<string> ExporteerKlantAsync(int klantId);

    /// <summary>
    /// Definitieve anonimisering (GDPR art. 17): persoonsgegevens op de klant
    /// en in audit-records worden onherstelbaar vervangen. Boekhoudkundig
    /// verplichte documenten (facturen/bestelbonnen) blijven ongewijzigd —
    /// die vallen onder de wettelijke bewaarplicht.
    /// </summary>
    Task AnonimiseerKlantAsync(int klantId);

    /// <summary>
    /// Klanten waarvan de laatste activiteit (offerte/factuur) ouder is dan
    /// het retentiebeleid — kandidaten voor anonimisering. Beslissing blijft
    /// bij de zaakvoerder; er wordt nooit automatisch geanonimiseerd.
    /// </summary>
    Task<IReadOnlyList<(int KlantId, string Naam, DateTime? LaatsteActiviteit)>> VindKandidatenVoorbijRetentieAsync();
}
