namespace QuadroApp.Service.Foutlog;

/// <summary>De codes voor <c>FoutLog.Bron</c>: waar de fout opkwam.</summary>
public static class FoutLogBron
{
    /// <summary>Task zonder await; komt pas bij een latere garbage collection boven.</summary>
    public const string UnobservedTask = "UnobservedTask";
    public const string Achtergrondthread = "Achtergrondthread";
    public const string UiThread = "UI-thread";
    public const string Opstarten = "Opstarten";
    /// <summary>Uit crash.log ingelezen (zie CrashLogImporter).</summary>
    public const string Geimporteerd = "Geïmporteerd";
}
