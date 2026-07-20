using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using QuadroApp.Model.DB;

namespace QuadroApp.Data;

/// <summary>
/// US-36 — bouwt AuditLog-records uit de EF ChangeTracker.
/// Alleen de belangrijke entiteiten worden gevolgd; wachtwoord-hashes en
/// andere gevoelige velden komen nooit in het auditlog terecht.
/// </summary>
public static class AuditTrailWriter
{
    /// <summary>Entiteiten die geauditeerd worden (US-36 scope).</summary>
    private static readonly HashSet<Type> AuditedTypes = new()
    {
        typeof(Offerte), typeof(OfferteRegel),
        typeof(Factuur), typeof(FactuurLijn),
        typeof(TypeLijst), typeof(AfwerkingsOptie), typeof(AfwerkingsVariant),
        typeof(Leverancier), typeof(LeverancierBestelling), typeof(LeverancierBestelLijn),
        typeof(Gebruiker), typeof(Klant)
    };

    /// <summary>Velden die nooit gelogd mogen worden.</summary>
    private static readonly HashSet<string> ExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Gebruiker.WachtwoordHash),
        "RowVersion"
    };

    /// <summary>Audit-record + de bijbehorende entry (om Id's van Added entiteiten ná de save in te vullen).</summary>
    public sealed record PendingAudit(AuditLog Log, EntityEntry Entry, bool WasAdded);

    /// <summary>
    /// Maakt audit-records voor alle relevante wijzigingen in de tracker.
    /// Aanroepen VÓÓR base.SaveChangesAsync; ná de save Id's invullen via
    /// <see cref="DescribeKey"/> en de records opslaan.
    /// </summary>
    public static List<PendingAudit> BuildEntries(ChangeTracker tracker)
    {
        var result = new List<PendingAudit>();
        var nu = DateTime.Now;
        var gebruiker = AuditContext.CurrentUserName;

        // Materialize first: we voegen zelf entries toe aan de context.
        var entries = tracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => AuditedTypes.Contains(e.Entity.GetType()))
            .ToList();

        foreach (var entry in entries)
        {
            var wijzigingen = entry.State switch
            {
                EntityState.Added => SerializeAdded(entry),
                EntityState.Modified => SerializeModified(entry),
                EntityState.Deleted => "{}",
                _ => null
            };

            // Modified zonder echte veldwijzigingen (bv. alleen genegeerde velden) → overslaan
            if (wijzigingen is null || (entry.State == EntityState.Modified && wijzigingen == "{}"))
                continue;

            result.Add(new PendingAudit(new AuditLog
            {
                Tijdstip = nu,
                Gebruiker = gebruiker,
                EntiteitType = entry.Entity.GetType().Name,
                EntiteitId = DescribeKey(entry),
                Actie = entry.State switch
                {
                    EntityState.Added => "Toegevoegd",
                    EntityState.Modified => "Gewijzigd",
                    _ => "Verwijderd"
                },
                Wijzigingen = wijzigingen
            }, entry, entry.State == EntityState.Added));
        }

        return result;
    }

    private static string SerializeAdded(EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (ExcludedProperties.Contains(prop.Metadata.Name)) continue;
            if (prop.CurrentValue is null) continue;
            dict[prop.Metadata.Name] = new { nieuw = Beschrijf(prop.CurrentValue) };
        }
        return JsonSerializer.Serialize(dict);
    }

    private static string SerializeModified(EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            if (ExcludedProperties.Contains(prop.Metadata.Name)) continue;
            if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;
            dict[prop.Metadata.Name] = new { oud = Beschrijf(prop.OriginalValue), nieuw = Beschrijf(prop.CurrentValue) };
        }
        return JsonSerializer.Serialize(dict);
    }

    public static string DescribeKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return "?";
        return string.Join("/", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "?"));
    }

    private static object? Beschrijf(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
        decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        byte[] => "(binair)",
        _ => value.ToString()
    };
}
