namespace QuadroApp.Model.DB;

/// <summary>
/// Het ene nummer dat Veerle per verkoop ziet (offerte, werkbon, bestelbon). Achter de schermen blijven
/// offertenummer (412) en bestelbonnummer (2026-19) apart bestaan; op het scherm en op papier staat:
/// het bestelbonnummer zodra de klant akkoord ging, daarvoor "voorlopig · 412".
/// </summary>
public static class VerkoopNummer
{
    /// <param name="offerteNummer">Het offertenummer (412).</param>
    /// <param name="definitiefBestelbonNummer">Het bestelbonnummer met volgnummer (2026-19), of leeg zolang er geen is.</param>
    public static string Tekst(int offerteNummer, string? definitiefBestelbonNummer)
        => !string.IsNullOrWhiteSpace(definitiefBestelbonNummer) ? definitiefBestelbonNummer!
            : offerteNummer > 0 ? $"voorlopig · {offerteNummer}"
            : "voorlopig";

    /// <summary>Zelfde, met de bon zelf: een voorlopige bon (zonder volgnummer) telt niet als nummer.</summary>
    public static string Tekst(int offerteNummer, Bestelbon? bon) => Tekst(offerteNummer, Definitief(bon));

    /// <summary>Het bestelbonnummer als de bon definitief is, anders null.</summary>
    public static string? Definitief(Bestelbon? bon) => bon?.VolgNr is null ? null : bon.BestelbonNummer;
}
