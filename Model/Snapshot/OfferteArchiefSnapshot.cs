using System;
using System.Collections.Generic;

namespace QuadroApp.Model.Snapshot
{
    /// <summary>
    /// Volledige JSON-snapshot van een gearchiveerde offerte.
    /// Bevat alles: offerte, klant, regels (met TypeLijst/afwerking namen), werkbon en taken.
    /// </summary>
    public class OfferteArchiefSnapshot
    {
        public int SchemaVersie { get; set; } = 1;
        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;

        public SnapshotOfferte Offerte { get; set; } = new();
        public SnapshotKlant? Klant { get; set; }
        public List<SnapshotOfferteRegel> Regels { get; set; } = new();
        public SnapshotWerkBon? WerkBon { get; set; }
        public List<SnapshotWerkTaak> Taken { get; set; } = new();
    }
}
