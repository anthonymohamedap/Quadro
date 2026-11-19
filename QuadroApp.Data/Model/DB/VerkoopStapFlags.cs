using System;

namespace QuadroApp.Model.DB
{
    /// <summary>US-87 — stappen van een verkoop die overgeslagen kunnen worden (opgeslagen als getal).</summary>
    [Flags]
    public enum VerkoopStapFlags
    {
        Geen = 0,
        /// <summary>Stap 2 Werkbon (inplannen).</summary>
        Werkbon = 1,
        /// <summary>Stap 3 In productie.</summary>
        Productie = 2
    }
}
