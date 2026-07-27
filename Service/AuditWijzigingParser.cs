using System.Collections.Generic;
using System.Text.Json;

namespace QuadroApp.Service;

/// <summary>
/// US-40 — zet de <c>AuditLog.Wijzigingen</c>-JSON om naar leesbare regels
/// "Veld: oud → nieuw". De JSON heeft de vorm
/// <c>{ "Veld": { "oud": ..., "nieuw": ... }, ... }</c> (bij toevoegen ontbreekt "oud";
/// een verwijdering is <c>{}</c>).
/// </summary>
public static class AuditWijzigingParser
{
    public static IReadOnlyList<string> Beschrijf(string? json)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return result; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                string? oud = null, nieuw = null;
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (prop.Value.TryGetProperty("oud", out var o)) oud = Tekst(o);
                    if (prop.Value.TryGetProperty("nieuw", out var n)) nieuw = Tekst(n);
                }

                if (oud is null && nieuw is not null)
                    result.Add($"{prop.Name}: — → {nieuw}");
                else if (oud is not null && nieuw is null)
                    result.Add($"{prop.Name}: {oud} → —");
                else if (oud is not null && nieuw is not null)
                    result.Add($"{prop.Name}: {oud} → {nieuw}");
            }
        }

        return result;
    }

    private static string Tekst(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => "—",
        JsonValueKind.String => e.GetString() ?? "",
        _ => e.GetRawText()
    };
}
