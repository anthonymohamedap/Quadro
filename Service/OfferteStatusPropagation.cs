using QuadroApp.Model.DB;

namespace QuadroApp.Service;

/// <summary>
/// US-42 — afgeleide statuspropagatie naar de offerte.
///
/// De offerte "volgt" de productie: werkbon- en bestelbon-overgangen sturen de
/// offertestatus. Deze helper is bewust <b>lenient en idempotent</b>:
/// hij zet de offerte alleen VOORUIT naar een productie-/facturatiestatus en
/// gooit nooit een fout — een onverwachte bronstatus mag de betaling of het
/// afwerken van een werkbon niet blokkeren.
///
/// De offertestatussen zijn lineair geordend
/// (Concept 0 → Verzonden 1 → Goedgekeurd 2 → InProductie 3 → Afgewerkt 4 →
///  Besteld 5 → Betaald 6, Geannuleerd 7). We propageren alleen naar
/// InProductie/Afgewerkt/Besteld/Betaald en nooit terug.
/// </summary>
public static class OfferteStatusPropagation
{
    /// <summary>
    /// Zet <paramref name="offerte"/> vooruit naar <paramref name="target"/> als dat een
    /// geldige afgeleide overgang is. Retourneert true als de status is gewijzigd.
    /// </summary>
    public static bool TryAdvanceTo(Offerte offerte, OfferteStatus target)
    {
        // Alleen productie-/facturatiestatussen zijn afgeleid propageerbaar.
        if (target is not (OfferteStatus.InProductie or OfferteStatus.Afgewerkt
            or OfferteStatus.Besteld or OfferteStatus.Betaald))
            return false;

        var current = offerte.Status;

        // Geannuleerd of nog vóór Goedgekeurd → niet propageren.
        if (current == OfferteStatus.Geannuleerd || current < OfferteStatus.Goedgekeurd)
            return false;

        // Alleen vooruit (idempotent, nooit terug).
        if ((int)current >= (int)target)
            return false;

        offerte.Status = target;
        return true;
    }
}
