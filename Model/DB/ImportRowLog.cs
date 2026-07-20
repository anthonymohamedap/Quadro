namespace QuadroApp.Model.DB;

public sealed class ImportRowLog
{
    public int Id { get; set; }
    public int ImportSessionId { get; set; }
    public int RowNumber { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? IssuesJson { get; set; }

    public ImportSession? ImportSession { get; set; }
}
