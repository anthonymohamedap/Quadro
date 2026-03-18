using QuadroApp.Model.Import;
using System.Collections.Generic;

namespace QuadroApp.Service.Import.Enterprise;

public interface IExcelMap<T>
{
    string EntityName { get; }
    IReadOnlyList<ExcelColumn<T>> Columns { get; }
    T Create();
    void ApplyCell(T target, string columnKey, string? cellText, int rowNumber, List<ImportRowIssue> issues);
    string? GetCellText(T source, string columnKey);
    string GetKey(T source);
}
