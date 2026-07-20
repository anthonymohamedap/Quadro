using System.Collections.Generic;

namespace QuadroApp.Model.Import;

public record ImportResult(
    int Added,
    int Updated,
    int Skipped
);

public record ImportPreviewResult(
    List<TypeLijstPreviewRow> Rows,
    List<ImportIssue> Issues
);

public sealed record ImportPreviewResult<T>(
    IReadOnlyList<T> Rows,
    IReadOnlyList<ImportIssue> Issues
);
