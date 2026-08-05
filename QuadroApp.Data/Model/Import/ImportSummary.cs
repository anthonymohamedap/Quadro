namespace QuadroApp.Model.Import;

public sealed class ImportSummary
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int WarningRows { get; set; }
    public int InsertCount { get; set; }
    public int UpdateCount { get; set; }
    public int SkippedCount { get; set; }
}
