using System;
using System.IO;

namespace QuadroApp.Service.Security;

/// <summary>
/// Onthoudt de laatst gebruikte <b>gebruikersnaam</b> tussen sessies, zodat die bij
/// het opstarten voor-ingevuld staat en de gebruiker enkel nog het wachtwoord hoeft te
/// typen.
///
/// Bewust alléén de gebruikersnaam — nooit het wachtwoord. Een gebruikersnaam is geen
/// geheim, dus platte opslag in de gebruikers-datamap (dezelfde map als db.secret) volstaat.
///   Windows → %LOCALAPPDATA%\QuadroApp\last-user.txt
///   macOS   → ~/Library/Application Support/QuadroApp/last-user.txt
///
/// Alle I/O is best-effort: faalt lezen of schrijven, dan gedraagt de app zich gewoon
/// alsof er niets onthouden is (leeg veld). Nooit een crash op deze randvoorziening.
/// </summary>
public static class LastLoginStore
{
    private const string FileName = "last-user.txt";

    private static string DataDir()
    {
        var dir = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                           "Library", "Application Support", "QuadroApp")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                           "QuadroApp");
        return dir;
    }

    private static string FilePath() => Path.Combine(DataDir(), FileName);

    /// <summary>Laatst onthouden gebruikersnaam, of leeg als er niets (bruikbaars) is.</summary>
    public static string Read()
    {
        try
        {
            var path = FilePath();
            if (!File.Exists(path)) return "";
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Bewaart de gebruikersnaam (best-effort). Lege waarde wist het bestand.</summary>
    public static void Save(string? gebruikersnaam)
    {
        try
        {
            var dir = DataDir();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, FileName);

            if (string.IsNullOrWhiteSpace(gebruikersnaam))
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            File.WriteAllText(path, gebruikersnaam.Trim());
        }
        catch
        {
            // Randvoorziening — nooit fataal.
        }
    }
}
