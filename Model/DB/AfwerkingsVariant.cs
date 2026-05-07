using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuadroApp.Model.DB;

/// <summary>
/// Een kleur- of stijlvariant van een AfwerkingsOptie.
/// Voorbeeld: optie = "Mat Glas", varianten = "Zwart" / "Brons" / "Transparant".
/// Alle bedrijfslogica (prijs, marge, leverancier) zit op de optie.
/// De variant bevat enkel de visuele beschrijving.
/// </summary>
public class AfwerkingsVariant
{
    public int Id { get; set; }

    [Required]
    public int AfwerkingsOptieId { get; set; }
    public AfwerkingsOptie? Optie { get; set; }

    /// <summary>Zichtbare naam van de variant, bv. "Zwart", "Brons", "Transparant".</summary>
    [Required, MaxLength(80)]
    public string Beschrijving { get; set; } = string.Empty;

    /// <summary>Optionele hex kleurcode voor visuele weergave, bv. "#1a1a2e".</summary>
    [MaxLength(20)]
    public string? Kleur { get; set; }

    /// <summary>Korte code voor legacy gebruik, bv. "ZW", "BR".</summary>
    [MaxLength(10)]
    public string? VariantCode { get; set; }

    /// <summary>De standaardvariant die automatisch geselecteerd wordt bij nieuwe regels.</summary>
    public bool IsStandaard { get; set; }

    public bool IsActief { get; set; } = true;

    /// <summary>"Mat Glas — Zwart"</summary>
    [NotMapped]
    public string DisplayLabel =>
        Optie is not null ? $"{Optie.Naam} — {Beschrijving}" : Beschrijving;
}
