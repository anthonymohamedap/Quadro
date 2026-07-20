using System;
using System.Collections.Generic;
using System.Linq;   // ✅ DIT ONTBRAK

namespace QuadroApp.Validation;

public enum ValidationSeverity { Error, Warning }

public sealed record ValidationItem(string Field, string Message, ValidationSeverity Severity);

public sealed class ValidationResult
{
    public List<ValidationItem> Items { get; } = new();

    public bool IsValid => Items.All(x => x.Severity != ValidationSeverity.Error);

    public void Error(string field, string msg) => Items.Add(new(field, msg, ValidationSeverity.Error));
    public void Warn(string field, string msg) => Items.Add(new(field, msg, ValidationSeverity.Warning));

    public string ErrorText() =>
        string.Join(Environment.NewLine,
            Items.Where(i => i.Severity == ValidationSeverity.Error)
                 .Select(i => $"• {i.Field}: {i.Message}"));

    public string WarningText() =>
        string.Join(Environment.NewLine,
            Items.Where(i => i.Severity == ValidationSeverity.Warning)
                 .Select(i => $"• {i.Field}: {i.Message}"));

    public override string ToString() =>
        string.Join(Environment.NewLine, Items.Select(i => $"• {i.Field}: {i.Message}"));
}