using QuadroApp.Model.DB;
using System.Collections.Generic;

namespace QuadroApp.Service.Verkoop
{
    /// <summary>US-83 — de vijf stappen van een verkoop, van offerte tot afhaling.</summary>
    public enum VerkoopStap
    {
        Offerte = 1,
        Werkbon = 2,
        InProductie = 3,
        KlaarOmAfTeHalen = 4,
        AfgehaaldEnBetaald = 5
    }

    public enum StapToestand
    {
        NogTeDoen,
        Huidig,
        Klaar
    }

    /// <summary>Resultaat van <see cref="VerkoopStapResolver.Bepaal"/>.</summary>
    /// <param name="Huidig">De stap waar de verkoop nu staat.</param>
    /// <param name="IsGeannuleerd">Offerte geannuleerd: de balk wordt grijs.</param>
    /// <param name="IsAfgerond">Afgehaald én betaald: alle stappen klaar.</param>
    /// <param name="NogTeBetalen">Openstaand bedrag (0 als onbekend of alles betaald).</param>
    public sealed record VerkoopStapInfo(VerkoopStap Huidig, bool IsGeannuleerd, bool IsAfgerond, decimal NogTeBetalen)
    {
        /// <summary>Afgehaald, maar er staat nog een rest open.</summary>
        public bool IsAfgehaaldNogTeBetalen => Huidig == VerkoopStap.AfgehaaldEnBetaald && !IsAfgerond && NogTeBetalen > 0m;

        public StapToestand ToestandVan(VerkoopStap stap)
        {
            if (IsAfgerond) return StapToestand.Klaar;
            if (stap < Huidig) return StapToestand.Klaar;
            return stap == Huidig ? StapToestand.Huidig : StapToestand.NogTeDoen;
        }
    }

    /// <summary>
    /// US-83 — leidt de stap van een verkoop af uit de bestaande statussen. Eén plek, puur en
    /// testbaar; dezelfde indeling als de offertestatus-reconciliatie (US-48), zodat stap en
    /// offertestatus niet uit elkaar lopen. Later ook de basis voor het verkoopdossier (US-87).
    /// </summary>
    public static class VerkoopStapResolver
    {
        public static VerkoopStapInfo Bepaal(
            OfferteStatus offerteStatus,
            WerkBonStatus? werkBonStatus,
            BestelbonStatus? bestelbonStatus,
            decimal nogTeBetalen)
        {
            var rest = nogTeBetalen < 0m ? 0m : nogTeBetalen;

            if (offerteStatus == OfferteStatus.Geannuleerd)
                return new VerkoopStapInfo(VerkoopStap.Offerte, IsGeannuleerd: true, IsAfgerond: false, rest);

            var stap = werkBonStatus switch
            {
                WerkBonStatus.Gepland      => VerkoopStap.Werkbon,
                WerkBonStatus.InUitvoering => VerkoopStap.InProductie,
                WerkBonStatus.Afgewerkt    => VerkoopStap.KlaarOmAfTeHalen,
                WerkBonStatus.Afgehaald    => VerkoopStap.AfgehaaldEnBetaald,
                // Geen werkbon (meer): volg de offertestatus (oude of gearchiveerde gegevens).
                _ => offerteStatus switch
                {
                    OfferteStatus.Goedgekeurd => VerkoopStap.Werkbon,
                    OfferteStatus.InProductie => VerkoopStap.InProductie,
                    OfferteStatus.Afgewerkt or OfferteStatus.Besteld => VerkoopStap.KlaarOmAfTeHalen,
                    OfferteStatus.Betaald => VerkoopStap.AfgehaaldEnBetaald,
                    _ => VerkoopStap.Offerte
                }
            };

            var betaald = bestelbonStatus == BestelbonStatus.Betaald
                || (stap == VerkoopStap.AfgehaaldEnBetaald && werkBonStatus is null && offerteStatus == OfferteStatus.Betaald);
            var afgerond = stap == VerkoopStap.AfgehaaldEnBetaald && betaald;

            return new VerkoopStapInfo(stap, IsGeannuleerd: false, afgerond, afgerond ? 0m : rest);
        }

        public static string Titel(VerkoopStap stap) => stap switch
        {
            VerkoopStap.Offerte            => "Offerte",
            VerkoopStap.Werkbon            => "Werkbon",
            VerkoopStap.InProductie        => "In productie",
            VerkoopStap.KlaarOmAfTeHalen   => "Klaar om af te halen",
            VerkoopStap.AfgehaaldEnBetaald => "Afgehaald & betaald",
            _                              => stap.ToString()
        };

        /// <summary>Uitleg per stap (tooltip, AC 5).</summary>
        public static string Uitleg(VerkoopStap stap) => stap switch
        {
            VerkoopStap.Offerte            => "Prijs opmaken en de bestelbon als prijsvoorstel aan de klant geven. Gaat de klant akkoord, maak dan de werkbon (Bevestigen → Werkbon).",
            VerkoopStap.Werkbon         => "De klant heeft bevestigd: er is een werkbon. Plan het werk in en bestel wat nodig is.",
            VerkoopStap.InProductie        => "Het werk is bezig in het atelier (werkbon in uitvoering).",
            VerkoopStap.KlaarOmAfTeHalen   => "Het werk is klaar. Druk de bestelbon af voor de klant.",
            VerkoopStap.AfgehaaldEnBetaald => "De klant haalt af en betaalt de rest.",
            _                              => string.Empty
        };

        public static IReadOnlyList<VerkoopStap> AlleStappen { get; } = new[]
        {
            VerkoopStap.Offerte, VerkoopStap.Werkbon, VerkoopStap.InProductie,
            VerkoopStap.KlaarOmAfTeHalen, VerkoopStap.AfgehaaldEnBetaald
        };
    }
}
