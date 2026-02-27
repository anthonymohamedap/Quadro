using QuadroApp.Model.DB;

namespace QuadroApp.Model.Import;

public sealed class KlantPreviewRow
{
    public int RowNumber { get; init; }
    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }

    // Preview fields
    public string Voornaam { get; set; } = "";
    public string Achternaam { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefoon { get; set; }
    public string? Straat { get; set; }
    public string? Nummer { get; set; }
    public string? Postcode { get; set; }
    public string? Gemeente { get; set; }
    public string? BtwNummer { get; set; }
    public string? Opmerking { get; set; }

    public Klant ToEntity()
    {
        return new Klant
        {
            Voornaam = Voornaam,
            Achternaam = Achternaam,
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
            Telefoon = Telefoon,
            Straat = Straat,
            Nummer = Nummer,
            Postcode = Postcode,
            Gemeente = Gemeente,
            BtwNummer = BtwNummer,
            Opmerking = Opmerking
        };
    }
}
