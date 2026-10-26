namespace QuadroApp.Model.DB
{
    /// <summary>
    /// US-79 — de betaalwijzen die het atelier echt gebruikt. Wordt als tekst opgeslagen;
    /// de migratie <c>BetaalwijzenVereenvoudigen</c> zette Kontant/Cheque/Proton om naar Cash,
    /// Visa naar CreditCard en Storting naar Overschrijving.
    /// </summary>
    public enum Betaalwijze
    {
        Bancontact,
        Cash,
        CreditCard,
        Overschrijving
    }

    public static class BetaalwijzeExtensions
    {
        /// <summary>Leesbare naam voor schermen en afdrukken ("Credit card" i.p.v. "CreditCard").</summary>
        public static string Label(this Betaalwijze betaalwijze) => betaalwijze switch
        {
            Betaalwijze.Bancontact     => "Bancontact",
            Betaalwijze.Cash           => "Cash",
            Betaalwijze.CreditCard     => "Credit card",
            Betaalwijze.Overschrijving => "Overschrijving",
            _                          => betaalwijze.ToString()
        };
    }
}
