using System;
using System.Collections.Generic;

namespace QuadroApp.Model.Snapshot
{
    /// <summary>
    /// Volledige JSON-snapshot van een geannuleerde WerkBon + bijhorende Offerte.
    /// Wordt opgeslagen in WerkBonArchief.Snapshot voor volledige recovery.
    /// </summary>
    public class WerkBonArchiefSnapshot
    {
        public SnapshotOfferte Offerte { get; set; } = new();
        public SnapshotWerkBon WerkBon { get; set; } = new();
        public SnapshotKlant? Klant { get; set; }
        public List<SnapshotOfferteRegel> Regels { get; set; } = new();
        public List<SnapshotWerkTaak> Taken { get; set; } = new();

        /// <summary>Tijdstip waarop de snapshot werd aangemaakt.</summary>
        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;

        /// <summary>Versie van het snapshot-formaat (voor toekomstige migraties).</summary>
        public int SchemaVersie { get; set; } = 1;
    }

    public class SnapshotOfferte
    {
        public int Id { get; set; }
        public DateTime Datum { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Opmerking { get; set; }
        public decimal SubtotaalExBtw { get; set; }
        public decimal BtwBedrag { get; set; }
        public decimal TotaalInclBtw { get; set; }
        public decimal KortingPct { get; set; }
        public decimal MeerPrijsIncl { get; set; }
        public bool IsVoorschotBetaald { get; set; }
        public decimal VoorschotBedrag { get; set; }
        public DateTime? GeplandeDatum { get; set; }
        public DateTime? DeadlineDatum { get; set; }
        public int? GeschatteMinuten { get; set; }
    }

    public class SnapshotWerkBon
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotaalPrijsIncl { get; set; }
        public DateTime AangemaaktOp { get; set; }
        public DateTime? AfhaalDatum { get; set; }
        public bool StockReservationProcessed { get; set; }
    }

    public class SnapshotKlant
    {
        public int Id { get; set; }
        public string Voornaam { get; set; } = string.Empty;
        public string Achternaam { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Telefoon { get; set; }
        public string? Straat { get; set; }
        public string? Nummer { get; set; }
        public string? Postcode { get; set; }
        public string? Gemeente { get; set; }
        public string? BtwNummer { get; set; }
        public string? Opmerking { get; set; }
    }

    public class SnapshotOfferteRegel
    {
        public int Id { get; set; }
        public string? Titel { get; set; }
        public string? Opmerking { get; set; }
        public int AantalStuks { get; set; }
        public decimal BreedteCm { get; set; }
        public decimal HoogteCm { get; set; }
        public decimal? InlegBreedteCm { get; set; }
        public decimal? InlegHoogteCm { get; set; }

        // TypeLijst (gedenormaliseerd zodat herstel niet afhankelijk is van ID nog aanwezig)
        public int? TypeLijstId { get; set; }
        /// <summary>Kopie van TypeLijst.Artikelnummer voor leesbaarheid in de archief-weergave.</summary>
        public string? TypeLijstNaam { get; set; }
        public string? TypeLijstArtikelnummer { get; set; }

        // Afwerkingen (id + label voor leesbaarheid)
        public int? GlasId { get; set; }
        public string? GlasNaam { get; set; }
        public int? PassePartout1Id { get; set; }
        public string? PassePartout1Naam { get; set; }
        public int? PassePartout2Id { get; set; }
        public string? PassePartout2Naam { get; set; }
        public int? DiepteKernId { get; set; }
        public string? DiepteKernNaam { get; set; }
        public int? OpklevenId { get; set; }
        public string? OpklevenNaam { get; set; }
        public int? RugId { get; set; }
        public string? RugNaam { get; set; }

        // Prijsdata
        public decimal TotaalExcl { get; set; }
        public decimal SubtotaalExBtw { get; set; }
        public decimal BtwBedrag { get; set; }
        public decimal TotaalInclBtw { get; set; }
        public decimal ExtraPrijs { get; set; }
        public decimal Korting { get; set; }
        public decimal? AfgesprokenPrijsExcl { get; set; }
        public int ExtraWerkMinuten { get; set; }
        public string? LegacyCode { get; set; }
    }

    public class SnapshotWerkTaak
    {
        public int Id { get; set; }
        public string Omschrijving { get; set; } = string.Empty;
        public DateTime GeplandVan { get; set; }
        public DateTime GeplandTot { get; set; }
        public int DuurMinuten { get; set; }
        public string? Resource { get; set; }
        public string? WeekNotitie { get; set; }
        public bool IsBesteld { get; set; }
        public DateTime? BestelDatum { get; set; }
        public bool IsOpVoorraad { get; set; }
        public string VoorraadStatus { get; set; } = string.Empty;
        public decimal BenodigdeMeter { get; set; }

        // Gekoppelde offerte-regel titel voor context
        public int? OfferteRegelId { get; set; }
        public string? OfferteRegelTitel { get; set; }

        // Leverancier bestel-lijn (als aanwezig)
        public int? LeverancierBestelLijnId { get; set; }
    }
}
