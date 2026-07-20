namespace QuadroApp.Data;

/// <summary>
/// US-36 — geeft de naam van de ingelogde gebruiker door aan AppDbContext
/// (die geen DI-scope heeft). Wordt gezet door AuthService bij in-/uitloggen.
/// </summary>
public static class AuditContext
{
    public const string Systeem = "(systeem)";

    private static string _currentUserName = Systeem;

    public static string CurrentUserName
    {
        get => _currentUserName;
        set => _currentUserName = string.IsNullOrWhiteSpace(value) ? Systeem : value;
    }

    public static void Reset() => _currentUserName = Systeem;
}
