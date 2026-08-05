using System.Collections.Generic;

namespace QuadroApp.Model.Import;

public sealed class ImportResult<T>
{
    public ImportSummary Summary { get; init; } = new();
    public IReadOnlyList<ImportRowResult<T>> Rows { get; init; } = [];
    public IReadOnlyList<ImportRowIssue> GlobalIssues { get; init; } = [];
}
