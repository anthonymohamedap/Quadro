namespace QuadroApp.Model.Import;

public sealed class TypeLijstPreviewRow
{
    public int RowNumber { get; set; }
    public string Artikelnummer { get; set; } = "";
    public string LeverancierCode { get; set; } = "";
    public int BreedteCm { get; set; }
    public string Type { get; set; } = "";
    public string Opmerking1 { get; set; } = "";
    public string Opmerking2 { get; set; } = "";
    public string LeverancierNaam { get; set; } = "";

    public decimal Stock { get; set; }
    public decimal Minstock { get; set; }
    public decimal Inventariskost { get; set; }

    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }

    // Optioneel voor UI
    public bool WillInsert { get; set; }
    public bool WillUpdate { get; set; }
}
