using System.Collections.Generic;
using System.Linq;

namespace QuadroApp.Model.Import;

public sealed class ImportRowResult<T>
{
    public int RowNumber { get; init; }
    public T? Parsed { get; set; }
    public List<ImportRowIssue> Issues { get; } = new();

    public bool IsValid => Issues.All(i => i.Severity != Severity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == Severity.Warning);
}
