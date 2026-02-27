namespace QuadroApp.Model.Import
{
    public record ImportIssue(int RowNumber, string Field, string Message, string? RawValue = null);


}
