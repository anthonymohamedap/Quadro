namespace QuadroApp.Model.DB
{
    /// <summary>US-66 — betaalwijzes van het ontvangstenregister (<see cref="Ontvangst"/>), zelfde
    /// lijst als de kassa van Quadro: kontant, cheque, visa, bancontact, proton, storting.</summary>
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
