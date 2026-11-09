using System;
using System.Text.RegularExpressions;

namespace QuadroApp.Service.Foutlog;

/// <summary>
/// Maakt tekst veilig om in de database te bewaren: wachtwoorden, gebruikersnamen, e-mailadressen,
/// bedragen, IBAN en telefoonnummers worden vervangen; velden worden ingekort. Stackframes
/// (<c>at X.Y(Int32 id) in C:\..\File.cs:line 123</c>) blijven intact.
/// </summary>
public static class FoutLogFilter
{
    public const int MaxMelding = 1000;
    public const int MaxExceptionType = 300;
    public const int MaxStacktrace = 16000;
    public const int MaxScherm = 200;
    public const int MaxLaatsteKlikken = 4000;
    public const int MaxGebruiker = 50;
    public const int MaxComputernaam = 100;
    public const int MaxAppVersie = 50;
    public const int MaxBron = 60;

    private const RegexOptions Opties = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Time = TimeSpan.FromSeconds(2);

    // Password=...; Pwd=...  (tot de volgende ';' of regeleinde)
    // Ook tussen aanhalingstekens (Password='a;b', Password="x", "password":"x").
    private static readonly Regex Wachtwoord = new(@"(?<k>(?<![A-Za-z])(?:new|old|current|nieuw|oud|huidig)?(?:password|pwd)""?)(?<s>\s*[=:]\s*)(?:""[^""\r\n]*""|'[^'\r\n]*'|[^;\r\n]*)", Opties, Time);
    // wachtwoord: x  /  wachtwoord=x
    private static readonly Regex WachtwoordNl = new(@"(?<k>wachtwoord\w*""?)(?<s>\s*[:=]\s*)(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s;,]+)", Opties, Time);
    private static readonly Regex Gebruikersnaam = new(@"(?<k>\b(?:username|user\s?id)""?)(?<s>\s*[=:]\s*)(?:""[^""\r\n]*""|'[^'\r\n]*'|[^;\r\n]*)", Opties, Time);
    // PostgreSQL/EF: Key (Naam)=(Jansen) already exists.  /  Detail: ...
    private static readonly Regex SleutelWaarde = new(@"(?<k>\bKey\s*\([^)\r\n]*\)\s*=\s*)\([^)\r\n]*\)", Opties, Time);
    private static readonly Regex Detail = new(@"(?<k>\bDetail:\s*)[^\r\n]+", Opties, Time);
    // Bedrag zonder € of EUR: precies 2 decimalen, niet middenin een versie (1.1.0), IP of tijd.
    private static readonly Regex BedragLos = new(@"(?<![\w.,:])\d+(?:[.,]\d{3})*[.,]\d{2}(?![\w]|[.,]\d)", Opties, Time);
    private static readonly Regex IbanAlgemeen = new(@"(?<!\w)[A-Z]{2}\d{2}(?:\s?[A-Z0-9]{4}){2,7}(?:\s?[A-Z0-9]{1,3})?(?!\w)", RegexOptions.CultureInvariant, Time);
    private static readonly Regex Email = new(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", Opties, Time);
    private static readonly Regex Iban = new(@"(?<![\w])BE\d{2}(?:\s?\d{4}){3}(?!\d)", Opties, Time);
    private static readonly Regex BedragVoor = new(@"€\s?\d+(?:[.,]\d+)*", Opties, Time);
    private static readonly Regex BedragNa = new(@"(?<![\w.,])\d+(?:[.,]\d+)*\s?(?:€|EUR\b)", Opties, Time);
    private static readonly Regex TelInternationaal = new(@"(?<![\w.])\+32(?:[\s./-]?\d){8,9}(?!\d)", Opties, Time);
    private static readonly Regex TelNationaal = new(@"(?<![\w.:])0\d{2,3}(?:[\s./-]?\d{2}){3}(?![\w])", Opties, Time);

    /// <summary>Vervangt gevoelige tekst. Faalt nooit; bij een regex-timeout komt een vervangtekst terug.</summary>
    public static string Filter(string? tekst)
    {
        if (string.IsNullOrEmpty(tekst)) return "";
        try
        {
            var t = tekst.Replace("\u0000", "");
            t = Wachtwoord.Replace(t, "${k}${s}***");
            t = WachtwoordNl.Replace(t, "${k}${s}***");
            t = Gebruikersnaam.Replace(t, "${k}${s}***");
            t = SleutelWaarde.Replace(t, "${k}(***)");
            t = Detail.Replace(t, "${k}***");
            t = Email.Replace(t, "[e-mail]");
            t = IbanAlgemeen.Replace(t, "[iban]");
            t = Iban.Replace(t, "[iban]");
            t = BedragVoor.Replace(t, "[bedrag]");
            t = BedragNa.Replace(t, "[bedrag]");
            t = TelInternationaal.Replace(t, "[tel]");
            t = TelNationaal.Replace(t, "[tel]");
            t = BedragLos.Replace(t, "[bedrag]");
            return t;
        }
        catch (RegexMatchTimeoutException)
        {
            return "[gefilterd: te lange tekst]";
        }
    }

    /// <summary>Kort de tekst in tot <paramref name="max"/> tekens (inclusief de slot-"…").</summary>
    public static string Kort(string? tekst, int max)
    {
        if (string.IsNullOrEmpty(tekst) || max <= 0) return "";
        if (tekst.Length <= max) return tekst;
        var einde = max - 1;
        if (einde > 0 && char.IsHighSurrogate(tekst[einde - 1])) einde--;
        return tekst[..einde] + "…";
    }

    /// <summary>Filter en kort in.</summary>
    public static string FilterKort(string? tekst, int max) => Kort(Filter(tekst), max);
}
