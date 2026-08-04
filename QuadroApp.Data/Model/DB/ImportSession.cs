using System;
using System.Collections.Generic;

namespace QuadroApp.Model.DB;

public sealed class ImportSession
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ErrorMessage { get; set; }

    public ICollection<ImportRowLog> RowLogs { get; set; } = new List<ImportRowLog>();
}
