namespace QuadroApp.Model.Import;

public sealed class ImportRowIssue
{
    public int RowNumber { get; init; }
    public string ColumnName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Severity Severity { get; init; }
    public string? RawValue { get; init; }
}
