using System;
using System.Collections.Generic;

namespace QuadroApp.Service.Import.Enterprise;

public sealed class ExcelColumn<T>
{
    public string Key { get; init; } = string.Empty;
    public string Header { get; init; } = string.Empty;
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public bool Required { get; init; }
    public Func<string?, (bool ok, object? parsed, string? error)> Parser { get; init; } = _ => (true, null, null);
    public string? Example { get; init; }
}
