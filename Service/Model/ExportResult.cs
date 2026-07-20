using System;

namespace QuadroApp.Service.Model;

public enum ExportFormaat
{
    Pdf = 0
}

public sealed class ExportResult
{
    public bool Success { get; init; }
    public string BestandPad { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;

    public static ExportResult Ok(string pad, string message = "Export gelukt.") =>
        new() { Success = true, BestandPad = pad, Message = message };

    public static ExportResult Fail(string message) =>
        new() { Success = false, Message = message };
}
