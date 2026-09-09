namespace QuadroApp.Model.DB
{
    /// <summary>US-66 — betaalwijzes voor losse winkelverkopen. Bewust top-level (niet genest in
    /// <see cref="WinkelVerkoop"/>) zodat een latere story dit kan hergebruiken op
    /// <see cref="Factuur"/>/<see cref="Offerte"/>.</summary>
    public enum Betaalwijze
    {
        Kontant,
        Bancontact,
        Visa,
        Cheque,
        Proton,
        Storting
    }
}
