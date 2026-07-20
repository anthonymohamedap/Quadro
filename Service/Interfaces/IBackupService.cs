using System.Threading.Tasks;

namespace QuadroApp.Service.Interfaces;

/// <summary>US-34 — automatic database backups.</summary>
public interface IBackupService
{
    /// <summary>
    /// Creates today's backup if it doesn't exist yet and prunes expired ones.
    /// Returns the backup file path, or null when skipped (already done today,
    /// or the database is not SQLite). Never throws — errors are logged.
    /// </summary>
    Task<string?> RunDailyBackupAsync();
}
