public sealed class AfwerkingsOptiePreviewRow
{
    public int RowNumber { get; init; }
    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }

    public string AfwerkingsGroep { get; set; } = "";
    public string Naam { get; set; } = "";

    public string VolgnummerRaw { get; set; } = "";
    public char Volgnummer { get; set; }

    public decimal KostprijsPerM2 { get; set; }
    public decimal WinstMarge { get; set; }
    public decimal AfvalPercentage { get; set; }
    public decimal VasteKost { get; set; }
    public int WerkMinuten { get; set; }

    public string? LeverancierCode { get; set; }

    // alleen voor groep-resolve
    public int AfwerkingsGroepId { get; set; }
}