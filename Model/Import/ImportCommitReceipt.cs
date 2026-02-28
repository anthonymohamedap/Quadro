namespace QuadroApp.Model.Import;

public sealed class ImportCommitReceipt
{
    public int SessionId { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public string Status { get; init; } = "Completed";
}
