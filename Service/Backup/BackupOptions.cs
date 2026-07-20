namespace QuadroApp.Service.Backup;

/// <summary>
/// US-34 — backup configuration. Optionally set in appsettings.json:
///   { "Backup": { "Directory": "D:\\QuadroBackups", "RetentionDays": 30 } }
/// When Directory is null the default &lt;dataDir&gt;/Backups is used.
/// Point Directory at a NAS/OneDrive/second-disk folder so backups
/// survive a disk failure of the live database.
/// </summary>
public sealed record BackupOptions
{
    public string? Directory { get; init; }
    public int RetentionDays { get; init; } = 30;
}
