namespace QuadroApp.Model.DB
{
    /// <summary>
    /// US-85 — gewone woorden voor statussen op het scherm, in plaats van de technische enumnamen
    /// ("InProductie", "KlaarVoorExport", "BelowMinimum"). Eén plek, zodat elk scherm hetzelfde zegt.
    /// </summary>
    public static class StatusLabels
    {
        /// <summary>Volgt de fasen van de stappenbalk (US-83).</summary>
        public static string Label(this OfferteStatus status) => status switch
        {
            OfferteStatus.Concept     => "Offerte (concept)",
            OfferteStatus.Verzonden   => "Offerte (verzonden)",
            OfferteStatus.Goedgekeurd => "Werkbon",
            OfferteStatus.InProductie => "In productie",
            OfferteStatus.Afgewerkt   => "Klaar om af te halen",
            OfferteStatus.Besteld     => "Klaar om af te halen",
            OfferteStatus.Betaald     => "Betaald",
            OfferteStatus.Geannuleerd => "Geannuleerd",
            _                         => status.ToString()
        };

        public static string Label(this WerkBonStatus status) => status switch
        {
            WerkBonStatus.Gepland      => "Gepland",
            WerkBonStatus.InUitvoering => "In uitvoering",
            WerkBonStatus.Afgewerkt    => "Klaar om af te halen",
            WerkBonStatus.Afgehaald    => "Afgehaald",
            _                          => status.ToString()
        };

        /// <summary>Draft, KlaarVoorExport en Geexporteerd zijn voor de gebruiker allemaal "Open".</summary>
        public static string Label(this FactuurStatus status) => status switch
        {
            FactuurStatus.Draft or FactuurStatus.KlaarVoorExport or FactuurStatus.Geexporteerd => "Open",
            FactuurStatus.Betaald     => "Betaald",
            FactuurStatus.Geannuleerd => "Geannuleerd",
            _                         => status.ToString()
        };

        public static string Label(this VoorraadAlertType type) => type switch
        {
            VoorraadAlertType.LowStock              => "Lage voorraad",
            VoorraadAlertType.BelowMinimum          => "Onder de minimumvoorraad",
            VoorraadAlertType.OpenShortage          => "Tekort",
            VoorraadAlertType.OrderOverdue          => "Leverancier te laat",
            VoorraadAlertType.PartialReceiptPending => "Deels ontvangen",
            _                                       => type.ToString()
        };

        public static string Label(this LeverancierBestellingStatus status) => status switch
        {
            LeverancierBestellingStatus.Concept           => "Concept",
            LeverancierBestellingStatus.Besteld           => "Besteld",
            LeverancierBestellingStatus.DeelsOntvangen    => "Deels ontvangen",
            LeverancierBestellingStatus.VolledigOntvangen => "Ontvangen",
            LeverancierBestellingStatus.Geannuleerd       => "Geannuleerd",
            _                                             => status.ToString()
        };
    }
}
